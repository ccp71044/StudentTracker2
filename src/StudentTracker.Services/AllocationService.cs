using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class AllocationService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;
    private readonly BudgetService _budgetService;

    public AllocationService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit, BudgetService budgetService)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
        _budgetService = budgetService;
    }

    public async Task<List<Allocation>> GetAllocationsAsync()
    {
        return await _context.Allocations
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .Include(a => a.OutcomeReason)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Allocation>> GetByDeliveryAsync(Guid deliveryId)
    {
        return await _context.Allocations
            .Where(a => a.CourseDeliveryId == deliveryId)
            .Include(a => a.Student)
            .Include(a => a.OutcomeReason)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Allocation>> GetByStudentAsync(Guid studentId)
    {
        return await _context.Allocations
            .Where(a => a.StudentId == studentId)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .Include(a => a.OutcomeReason)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<Allocation> AllocateStudentAsync(Guid deliveryId, Guid studentId, decimal? certificateCost = null, Guid? budgetPoolId = null, Guid? creditPoolId = null, bool reserveCredit = false, bool createCashCommitment = false)
    {
        var existing = await _context.Allocations
            .AnyAsync(a => a.CourseDeliveryId == deliveryId && a.StudentId == studentId && a.AllocationStatus != AllocationStatus.Cancelled);
        if (existing) throw new InvalidOperationException("Student is already allocated to this delivery.");

        var delivery = await _context.CourseDeliveries.FindAsync(deliveryId) ?? throw new ArgumentException("Delivery not found");
        var defaultCost = delivery.CourseDefinition?.DefaultCertificateCost;
        var cost = certificateCost ?? defaultCost;

        if (createCashCommitment && budgetPoolId.HasValue && cost.HasValue && cost.Value > 0)
        {
            var available = await _budgetService.GetForecastAvailableAsync(budgetPoolId.Value);
            if (available < cost.Value)
                throw new InvalidOperationException($"Insufficient budget funds. Available: {available:C}, requested: {cost.Value:C}.");
        }

        var allocation = new Allocation
        {
            DisplayId = _idGenerator.NextDisplayId<Allocation>("ALL"),
            CourseDeliveryId = deliveryId,
            StudentId = studentId,
            CertificateCost = cost,
            BudgetPoolId = budgetPoolId,
            CreditPoolId = creditPoolId,
            AllocationStatus = AllocationStatus.Enrolled,
            AttendanceStatus = AttendanceStatus.NotRecorded,
            OutcomeStatus = OutcomeStatus.Pending,
            CreditStatus = CreditStatus.None,
            CertificateOrderStatus = CertificateOrderStatus.NotReady,
            CertificateDeliveryStatus = CertificateDeliveryStatus.NotApplicable,
            CashCommitmentStatus = CashCommitmentStatus.None
        };

        if (reserveCredit && creditPoolId.HasValue)
            allocation.CreditStatus = CreditStatus.Allocated;
        if (createCashCommitment && budgetPoolId.HasValue && cost.HasValue && cost.Value > 0)
        {
            allocation.CashCommitmentStatus = CashCommitmentStatus.Pending;
            _context.BudgetTransactions.Add(new BudgetTransaction
            {
                DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
                PoolId = budgetPoolId.Value,
                AllocationId = allocation.Id,
                TransactionType = BudgetTransactionType.CommitmentCreated,
                Amount = -cost.Value,
                Reason = "Cash commitment created on allocation",
                TransactionDate = DateTime.UtcNow
            });
        }

        _context.Allocations.Add(allocation);
        await _context.SaveChangesAsync();
        _audit.Record("Created", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
        return allocation;
    }

    public async Task<Allocation> CreatePlaceholderAsync(Guid deliveryId, string placeholderName, string? legacyReference = null)
    {
        var allocation = new Allocation
        {
            DisplayId = _idGenerator.NextDisplayId<Allocation>("ALL"),
            CourseDeliveryId = deliveryId,
            PlaceholderName = placeholderName,
            LegacyReference = legacyReference,
            AllocationStatus = AllocationStatus.Reserved,
            AttendanceStatus = AttendanceStatus.NotRecorded,
            OutcomeStatus = OutcomeStatus.Pending,
            CreditStatus = CreditStatus.None,
            CertificateOrderStatus = CertificateOrderStatus.NotReady,
            CertificateDeliveryStatus = CertificateDeliveryStatus.NotApplicable,
            CashCommitmentStatus = CashCommitmentStatus.None
        };
        _context.Allocations.Add(allocation);
        await _context.SaveChangesAsync();
        _audit.Record("Created", "Allocation", allocation.Id, allocation.DisplayId, null, new { Placeholder = placeholderName });
        await _context.SaveChangesAsync();
        return allocation;
    }

    public async Task<List<Allocation>> CreatePlaceholderAllocationsAsync(Guid deliveryId, string placeholderName, int quantity, decimal? certificateCost = null, Guid? budgetPoolId = null, string? legacyReference = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        if (string.IsNullOrWhiteSpace(placeholderName))
            throw new ArgumentException("Placeholder name is required.", nameof(placeholderName));

        var delivery = await _context.CourseDeliveries.FindAsync(deliveryId) ?? throw new ArgumentException("Delivery not found");
        var cost = certificateCost ?? delivery.CourseDefinition?.DefaultCertificateCost;
        var totalCost = cost.GetValueOrDefault() * quantity;

        if (budgetPoolId.HasValue && totalCost > 0)
        {
            var available = await _budgetService.GetForecastAvailableAsync(budgetPoolId.Value);
            if (available < totalCost)
            {
                _audit.Record("PlaceholderCommitmentBlocked", "Allocation", Guid.Empty, null, null, new { Requested = totalCost, Available = available, PoolId = budgetPoolId.Value });
                await _context.SaveChangesAsync();
                throw new InvalidOperationException($"Insufficient budget funds for {quantity} placeholders. Available: {available:C}, required: {totalCost:C}.");
            }
        }

        var allocations = new List<Allocation>();
        for (int i = 0; i < quantity; i++)
        {
            var indexedName = quantity == 1 ? placeholderName : $"{placeholderName} ({i + 1}/{quantity})";
            var allocation = new Allocation
            {
                DisplayId = _idGenerator.NextDisplayId<Allocation>("ALL"),
                CourseDeliveryId = deliveryId,
                PlaceholderName = indexedName,
                LegacyReference = legacyReference,
                AllocationStatus = AllocationStatus.Reserved,
                AttendanceStatus = AttendanceStatus.NotRecorded,
                OutcomeStatus = OutcomeStatus.Pending,
                CreditStatus = CreditStatus.None,
                CertificateOrderStatus = CertificateOrderStatus.NotReady,
                CertificateDeliveryStatus = CertificateDeliveryStatus.NotApplicable,
                CashCommitmentStatus = CashCommitmentStatus.None,
                BudgetPoolId = budgetPoolId,
                CertificateCost = cost
            };
            _context.Allocations.Add(allocation);
            allocations.Add(allocation);

            if (budgetPoolId.HasValue && cost.HasValue && cost.Value > 0)
            {
                _context.BudgetTransactions.Add(new BudgetTransaction
                {
                    DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
                    PoolId = budgetPoolId.Value,
                    AllocationId = allocation.Id,
                    TransactionType = BudgetTransactionType.CommitmentCreated,
                    Amount = -cost.Value,
                    Reason = $"Placeholder commitment for {indexedName}",
                    TransactionDate = DateTime.UtcNow
                });
                allocation.CashCommitmentStatus = CashCommitmentStatus.Pending;
            }
        }

        await _context.SaveChangesAsync();
        _audit.Record("CreatedPlaceholders", "Allocation", allocations.First().Id, null, null, new { Count = quantity, DeliveryId = deliveryId, BudgetPoolId = budgetPoolId, TotalCost = totalCost });
        await _context.SaveChangesAsync();
        return allocations;
    }

    public async Task<Allocation> ReplacePlaceholderAsync(Guid allocationId, Guid studentId)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        if (!string.IsNullOrEmpty(allocation.PlaceholderName))
        {
            allocation.StudentId = studentId;
            allocation.PlaceholderName = null;
            allocation.AllocationStatus = AllocationStatus.Enrolled;
            allocation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _audit.Record("ReplacedPlaceholder", "Allocation", allocation.Id, allocation.DisplayId);
            await _context.SaveChangesAsync();
        }
        return allocation;
    }

    public async Task<Allocation> MarkAttendanceAsync(Guid allocationId, AttendanceStatus status, string? notes = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        var old = allocation.AttendanceStatus;
        allocation.AttendanceStatus = status;
        if (!string.IsNullOrWhiteSpace(notes))
            allocation.OutcomeNotes = (allocation.OutcomeNotes + "\n" + notes).Trim();
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Attendance", "Allocation", allocation.Id, allocation.DisplayId, new { Attendance = old.ToString() }, new { Attendance = status.ToString() });
        await _context.SaveChangesAsync();
        return allocation;
    }

    public async Task<Allocation> MarkOutcomeAsync(Guid allocationId, OutcomeStatus outcome, Guid? reasonId = null, string? notes = null, DateTime? outcomeDate = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        var old = allocation.OutcomeStatus;
        allocation.OutcomeStatus = outcome;
        allocation.OutcomeDate = outcomeDate ?? DateTime.UtcNow;
        allocation.OutcomeReasonId = reasonId;
        if (!string.IsNullOrWhiteSpace(notes))
            allocation.OutcomeNotes = (allocation.OutcomeNotes + "\n" + notes).Trim();

        if (outcome == OutcomeStatus.Completed)
        {
            allocation.AllocationStatus = AllocationStatus.Finalised;
            allocation.CertificateOrderStatus = allocation.CreditStatus == CreditStatus.Allocated ? CertificateOrderStatus.Ready : CertificateOrderStatus.NotReady;
        }
        else if (outcome == OutcomeStatus.Withdrawn || outcome == OutcomeStatus.Transferred || outcome == OutcomeStatus.Cancelled)
        {
            allocation.AllocationStatus = outcome switch
            {
                OutcomeStatus.Withdrawn => AllocationStatus.Withdrawn,
                OutcomeStatus.Transferred => AllocationStatus.Transferred,
                OutcomeStatus.Cancelled => AllocationStatus.Cancelled,
                _ => allocation.AllocationStatus
            };
        }
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Outcome", "Allocation", allocation.Id, allocation.DisplayId, new { Outcome = old.ToString() }, new { Outcome = outcome.ToString() });
        await _context.SaveChangesAsync();
        return allocation;
    }

    public async Task CancelAsync(Guid allocationId, string? reason = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        var certificateOrdered = await _context.CertificateOrders.AnyAsync(o => o.AllocationId == allocationId);
        if (certificateOrdered || allocation.CreditStatus == CreditStatus.Consumed)
        {
            _audit.Record("CancellationBlocked", "Allocation", allocation.Id, allocation.DisplayId, null, new { Reason = "Certificate already ordered or credit consumed" });
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("Allocation cannot be cancelled after a certificate has been ordered or credit consumed.");
        }

        if (allocation.CashCommitmentStatus == CashCommitmentStatus.Pending && allocation.BudgetPoolId.HasValue)
        {
            var commitment = await _budgetService.GetAllocationCommitmentAsync(allocation.Id);
            _context.BudgetTransactions.Add(new BudgetTransaction
            {
                DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
                PoolId = allocation.BudgetPoolId.Value,
                AllocationId = allocation.Id,
                TransactionType = BudgetTransactionType.CommitmentReleased,
                Amount = Math.Max(0m, commitment),
                Reason = reason ?? "Allocation cancelled",
                TransactionDate = DateTime.UtcNow
            });
            allocation.CashCommitmentStatus = CashCommitmentStatus.Released;
        }

        if (allocation.CreditStatus == CreditStatus.Allocated && allocation.CreditPoolId.HasValue)
        {
            var allocatedCredit = await _context.CertificateCreditTransactions
                .Where(t => t.AllocationId == allocation.Id && (t.TransactionType == CreditTransactionType.Allocate || t.TransactionType == CreditTransactionType.Reserve || t.TransactionType == CreditTransactionType.Release))
                .SumAsync(t => t.Amount);
            _context.CertificateCreditTransactions.Add(new CertificateCreditTransaction
            {
                DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
                PoolId = allocation.CreditPoolId.Value,
                AllocationId = allocation.Id,
                TransactionType = CreditTransactionType.Release,
                Amount = -Math.Max(0m, allocatedCredit),
                Reason = reason ?? "Allocation cancelled",
                TransactionDateTime = DateTime.UtcNow
            });
            allocation.CreditStatus = CreditStatus.Released;
        }

        allocation.AllocationStatus = AllocationStatus.Cancelled;
        allocation.OutcomeStatus = OutcomeStatus.Cancelled;
        allocation.OutcomeDate = DateTime.UtcNow;
        allocation.OutcomeNotes = string.Join(Environment.NewLine, new[] { allocation.OutcomeNotes, reason }.Where(n => !string.IsNullOrWhiteSpace(n)));
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("Cancelled", "Allocation", allocation.Id, allocation.DisplayId, null, new { Reason = reason });
        await _context.SaveChangesAsync();
    }

    public async Task<Allocation> TransferAsync(Guid sourceAllocationId, Guid targetStudentId, Guid targetDeliveryId)
    {
        var source = await _context.Allocations.FindAsync(sourceAllocationId) ?? throw new ArgumentException("Source allocation not found");
        source.OutcomeStatus = OutcomeStatus.Transferred;
        source.AllocationStatus = AllocationStatus.Transferred;
        source.UpdatedAt = DateTime.UtcNow;
        _audit.Record("Transferred", "Allocation", source.Id, source.DisplayId);

        var target = new Allocation
        {
            DisplayId = _idGenerator.NextDisplayId<Allocation>("ALL"),
            CourseDeliveryId = targetDeliveryId,
            StudentId = targetStudentId,
            CertificateCost = source.CertificateCost,
            BudgetPoolId = source.BudgetPoolId,
            CreditPoolId = source.CreditPoolId,
            AllocationStatus = AllocationStatus.Enrolled,
            AttendanceStatus = AttendanceStatus.NotRecorded,
            OutcomeStatus = OutcomeStatus.Pending,
            CreditStatus = source.CreditStatus,
            CertificateOrderStatus = CertificateOrderStatus.NotReady,
            CertificateDeliveryStatus = CertificateDeliveryStatus.NotApplicable,
            CashCommitmentStatus = source.CashCommitmentStatus
        };
        _context.Allocations.Add(target);
        await _context.SaveChangesAsync();
        _audit.Record("Created", "Allocation", target.Id, target.DisplayId, null, new { TransferredFrom = source.DisplayId });
        await _context.SaveChangesAsync();
        return target;
    }

    public async Task CreateOrRestoreCommitmentAsync(Guid allocationId, decimal? amount = null, string? reason = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        if (!allocation.BudgetPoolId.HasValue)
            throw new InvalidOperationException("Allocation is not linked to a budget pool.");
        if (allocation.CashCommitmentStatus == CashCommitmentStatus.Pending)
            throw new InvalidOperationException("A commitment is already pending for this allocation.");
        if (allocation.CashCommitmentStatus != CashCommitmentStatus.None && allocation.CashCommitmentStatus != CashCommitmentStatus.Released)
            throw new InvalidOperationException("Commitment can only be created or restored when the allocation has no active commitment.");

        var commitmentAmount = amount ?? allocation.CertificateCost ?? 0m;
        if (commitmentAmount <= 0)
            throw new InvalidOperationException("A positive commitment amount is required.");

        var available = await _budgetService.GetForecastAvailableAsync(allocation.BudgetPoolId.Value);
        if (available < commitmentAmount)
        {
            _audit.Record("CommitmentBlocked", "Allocation", allocation.Id, allocation.DisplayId, null, new { Requested = commitmentAmount, Available = available });
            await _context.SaveChangesAsync();
            throw new InvalidOperationException($"Insufficient budget funds. Available: {available:C}, requested: {commitmentAmount:C}.");
        }

        _context.BudgetTransactions.Add(new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = allocation.BudgetPoolId.Value,
            AllocationId = allocation.Id,
            TransactionType = BudgetTransactionType.CommitmentCreated,
            Amount = -commitmentAmount,
            Reason = reason ?? "Commitment created/restored",
            TransactionDate = DateTime.UtcNow
        });
        allocation.CashCommitmentStatus = CashCommitmentStatus.Pending;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("CommitmentCreated", "Allocation", allocation.Id, allocation.DisplayId, null, new { Amount = commitmentAmount, PoolId = allocation.BudgetPoolId });
        await _context.SaveChangesAsync();
    }

    public async Task ReleaseCommitmentAsync(Guid allocationId, string? reason = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        if (!allocation.BudgetPoolId.HasValue)
            throw new InvalidOperationException("Allocation is not linked to a budget pool.");
        if (allocation.CashCommitmentStatus != CashCommitmentStatus.Pending)
            throw new InvalidOperationException("Only a pending commitment can be released.");

        var committed = await _budgetService.GetAllocationCommitmentAsync(allocation.Id);
        if (committed <= 0)
            throw new InvalidOperationException("No outstanding commitment amount to release.");

        _context.BudgetTransactions.Add(new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = allocation.BudgetPoolId.Value,
            AllocationId = allocation.Id,
            TransactionType = BudgetTransactionType.CommitmentReleased,
            Amount = committed,
            Reason = reason ?? "Commitment released",
            TransactionDate = DateTime.UtcNow
        });
        allocation.CashCommitmentStatus = CashCommitmentStatus.Released;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("CommitmentReleased", "Allocation", allocation.Id, allocation.DisplayId);
        await _context.SaveChangesAsync();
    }

    public async Task MarkCostSpentAsync(Guid allocationId, bool force = false, string? reason = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        if (!allocation.BudgetPoolId.HasValue)
            throw new InvalidOperationException("Allocation is not linked to a budget pool.");
        if (allocation.CashCommitmentStatus != CashCommitmentStatus.Pending)
            throw new InvalidOperationException("Cost can only be marked as spent when a commitment is pending.");
        if (!force && allocation.OutcomeStatus != OutcomeStatus.Completed)
            throw new InvalidOperationException("Cost can only be marked as spent after the allocation is completed. Use the override to continue.");

        var committed = await _budgetService.GetAllocationCommitmentAsync(allocation.Id);
        if (committed <= 0)
            throw new InvalidOperationException("No outstanding commitment amount to recognise.");

        var poolId = allocation.BudgetPoolId.Value;
        _context.BudgetTransactions.Add(new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            AllocationId = allocation.Id,
            TransactionType = BudgetTransactionType.CommitmentReleased,
            Amount = committed,
            Reason = reason ?? "Commitment released for expense recognition",
            TransactionDate = DateTime.UtcNow
        });
        _context.BudgetTransactions.Add(new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = poolId,
            AllocationId = allocation.Id,
            TransactionType = BudgetTransactionType.ExpenseRecognised,
            Amount = -committed,
            Reason = reason ?? "Expense recognised",
            TransactionDate = DateTime.UtcNow
        });
        allocation.CashCommitmentStatus = CashCommitmentStatus.Spent;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("ExpenseRecognised", "Allocation", allocation.Id, allocation.DisplayId, null, new { Amount = committed, PoolId = poolId, Forced = force });
        await _context.SaveChangesAsync();
    }

    public async Task ReverseSpentCostAsync(Guid allocationId, string? reason = null)
    {
        var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
        if (!allocation.BudgetPoolId.HasValue)
            throw new InvalidOperationException("Allocation is not linked to a budget pool.");
        if (allocation.CashCommitmentStatus != CashCommitmentStatus.Spent)
            throw new InvalidOperationException("Only a spent cost can be reversed.");

        var expense = await _budgetService.GetAllocationExpenseAsync(allocation.Id);
        if (expense <= 0)
            throw new InvalidOperationException("No recognised expense to reverse.");

        _context.BudgetTransactions.Add(new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = allocation.BudgetPoolId.Value,
            AllocationId = allocation.Id,
            TransactionType = BudgetTransactionType.ExpenseReversed,
            Amount = expense,
            Reason = reason ?? "Spent cost reversed",
            TransactionDate = DateTime.UtcNow
        });
        allocation.CashCommitmentStatus = CashCommitmentStatus.Released;
        allocation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record("ExpenseReversed", "Allocation", allocation.Id, allocation.DisplayId, null, new { Amount = expense, PoolId = allocation.BudgetPoolId });
        await _context.SaveChangesAsync();
    }

    public async Task<Allocation> CarryForwardPlaceholderAsync(Guid sourceAllocationId, Guid targetDeliveryId, string? reason = null)
    {
        var source = await _context.Allocations
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .FirstOrDefaultAsync(a => a.Id == sourceAllocationId)
            ?? throw new ArgumentException("Source allocation not found");

        if (string.IsNullOrWhiteSpace(source.PlaceholderName) || source.StudentId.HasValue)
            throw new InvalidOperationException("Only unassigned placeholder allocations can be carried forward.");

        var target = await _context.CourseDeliveries
            .Include(d => d.CourseDefinition)
            .FirstOrDefaultAsync(d => d.Id == targetDeliveryId)
            ?? throw new ArgumentException("Target delivery not found");

        if (source.CourseDelivery?.CourseDefinitionId != target.CourseDefinition?.Id)
            throw new InvalidOperationException("Carry-forward is only allowed between deliveries of the same course.");

        var oldDeliveryId = source.CourseDeliveryId;
        source.CourseDeliveryId = targetDeliveryId;
        source.UpdatedAt = DateTime.UtcNow;
        source.PlaceholderName = $"{source.PlaceholderName} (carried)";

        await _context.SaveChangesAsync();
        _audit.Record(
            "CarriedForward",
            "Allocation",
            source.Id,
            source.DisplayId,
            new { CourseDeliveryId = oldDeliveryId },
            new { CourseDeliveryId = targetDeliveryId, Reason = reason });
        await _context.SaveChangesAsync();

        return source;
    }
}
