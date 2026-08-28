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

    public AllocationService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
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
        var defaultCost = delivery.CourseDefinition?.DefaultCertificateCost ?? certificateCost;
        var allocation = new Allocation
        {
            DisplayId = _idGenerator.NextDisplayId<Allocation>("ALL"),
            CourseDeliveryId = deliveryId,
            StudentId = studentId,
            CertificateCost = certificateCost ?? delivery.CourseDefinition?.DefaultCertificateCost,
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
        if (createCashCommitment && budgetPoolId.HasValue)
            allocation.CashCommitmentStatus = CashCommitmentStatus.Pending;

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
            var commitment = -await _context.BudgetTransactions
                .Where(t => t.AllocationId == allocation.Id && (t.TransactionType == BudgetTransactionType.CommitmentCreated || t.TransactionType == BudgetTransactionType.CommitmentReleased))
                .SumAsync(t => t.Amount);
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
}
