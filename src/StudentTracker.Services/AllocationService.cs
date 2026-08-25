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
    private readonly CreditService _credits;
    private readonly BudgetService _budgets;

    public AllocationService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit, CreditService credits, BudgetService budgets)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
        _credits = credits;
        _budgets = budgets;
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

    /// <summary>
    /// Allocates a student to a delivery, optionally reserving certificate credit and committing
    /// cash. The allocation and both ledger entries are written as one unit: if the credit
    /// reservation or budget commitment fails, the allocation is not created either.
    /// </summary>
    public Task<Allocation> AllocateStudentAsync(Guid deliveryId, Guid studentId, decimal? certificateCost = null, Guid? budgetPoolId = null, Guid? creditPoolId = null, bool reserveCredit = false, bool createCashCommitment = false)
        => DbTransactionScope.RunAsync(_context, async () =>
        {
            var existing = await _context.Allocations
                .AnyAsync(a => a.CourseDeliveryId == deliveryId && a.StudentId == studentId && a.AllocationStatus != AllocationStatus.Cancelled);
            if (existing) throw new InvalidOperationException("Student is already allocated to this delivery.");

            var delivery = await _context.CourseDeliveries
                .Include(d => d.CourseDefinition)
                .FirstOrDefaultAsync(d => d.Id == deliveryId) ?? throw new ArgumentException("Delivery not found");

            var cost = certificateCost ?? delivery.CourseDefinition?.DefaultCertificateCost;
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

            _context.Allocations.Add(allocation);
            await _context.SaveChangesAsync();
            _audit.Record("Created", "Allocation", allocation.Id, allocation.DisplayId);
            await _context.SaveChangesAsync();

            if (reserveCredit)
            {
                if (!creditPoolId.HasValue) throw new InvalidOperationException("A credit pool is required to reserve credit.");
                var unit = await _credits.GetUnitAmountAsync(creditPoolId.Value, cost);
                await _credits.AllocateAsync(creditPoolId.Value, allocation.Id, unit, 1);
            }

            if (createCashCommitment)
            {
                if (!budgetPoolId.HasValue) throw new InvalidOperationException("A budget pool is required to commit cash.");
                await _budgets.CreateCommitmentAsync(budgetPoolId.Value, allocation.Id, cost ?? 0m);
            }

            return allocation;
        });

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

    /// <summary>
    /// Records a completion: sets the outcome and, when the settings expense trigger is
    /// <c>Completion</c>, converts any pending cash commitment into actual expenditure.
    /// Ordering a certificate remains a separate, explicit step (design section 12.5).
    /// </summary>
    public Task<Allocation> MarkCompletedAsync(Guid allocationId, DateTime? completionDate = null, string? notes = null)
        => DbTransactionScope.RunAsync(_context, async () =>
        {
            var allocation = await MarkOutcomeAsync(allocationId, OutcomeStatus.Completed, null, notes, completionDate);

            var settings = await _context.AppSettings.FirstOrDefaultAsync();
            if (settings?.ExpenseTrigger == "Completion" && allocation.BudgetPoolId.HasValue)
            {
                var outstanding = await _budgets.GetOutstandingCommitmentAsync(allocation.BudgetPoolId.Value, allocation.Id);
                if (outstanding > 0m)
                    await _budgets.RecogniseExpenseAsync(allocation.BudgetPoolId.Value, allocation.Id, outstanding, "Course completed");
            }

            return allocation;
        });

    /// <summary>
    /// Withdraws an allocation (design sections 8.4, 12.6 and 12.7). A structured reason is
    /// mandatory, and the caller must state explicitly whether the reserved credit can be reused
    /// and whether the cash commitment is released. When the credit is not reusable it is recorded
    /// as a credit loss rather than silently disappearing.
    /// </summary>
    public Task<Allocation> WithdrawAsync(Guid allocationId, Guid reasonId, bool creditReusable, bool releaseCashCommitment, DateTime? withdrawalDate = null, string? notes = null)
        => DbTransactionScope.RunAsync(_context, async () =>
        {
            var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
            var reason = await _context.OutcomeReasons.FindAsync(reasonId) ?? throw new ArgumentException("Withdrawal reason not found");
            if (reason.RequiresNotes && string.IsNullOrWhiteSpace(notes))
                throw new InvalidOperationException($"The reason '{reason.Name}' requires explanatory notes.");

            if (allocation.CreditPoolId.HasValue && allocation.CreditStatus == CreditStatus.Allocated)
            {
                var reserved = await _credits.GetReservedForAllocationAsync(allocation.CreditPoolId.Value, allocationId);
                if (reserved > 0m)
                {
                    if (creditReusable)
                        await _credits.ReleaseAsync(allocation.CreditPoolId.Value, allocationId, reserved, $"Withdrawn: {reason.Name}");
                    else
                        await _credits.MarkUnavailableAsync(allocation.CreditPoolId.Value, allocationId, reserved, $"Withdrawn: {reason.Name}");
                }
            }

            if (releaseCashCommitment && allocation.BudgetPoolId.HasValue)
            {
                var outstanding = await _budgets.GetOutstandingCommitmentAsync(allocation.BudgetPoolId.Value, allocationId);
                if (outstanding > 0m)
                    await _budgets.ReleaseCommitmentAsync(allocation.BudgetPoolId.Value, allocationId, outstanding, $"Withdrawn: {reason.Name}");
            }

            var updated = await MarkOutcomeAsync(allocationId, OutcomeStatus.Withdrawn, reasonId, notes, withdrawalDate);
            updated.CertificateOrderStatus = CertificateOrderStatus.NotRequired;
            updated.CertificateDeliveryStatus = CertificateDeliveryStatus.NotApplicable;
            await _context.SaveChangesAsync();
            return updated;
        });

    /// <summary>
    /// Records a non-completion (design section 8.5). A reason is mandatory. Non-completion is not
    /// a withdrawal: the certificate credit may still be consumed, and consuming credit never
    /// changes the cash position on its own.
    /// </summary>
    public Task<Allocation> MarkNonCompletedAsync(Guid allocationId, Guid reasonId, bool consumeCredit, DateTime? outcomeDate = null, string? notes = null)
        => DbTransactionScope.RunAsync(_context, async () =>
        {
            var allocation = await _context.Allocations.FindAsync(allocationId) ?? throw new ArgumentException("Allocation not found");
            var reason = await _context.OutcomeReasons.FindAsync(reasonId) ?? throw new ArgumentException("Non-completion reason not found");
            if (reason.RequiresNotes && string.IsNullOrWhiteSpace(notes))
                throw new InvalidOperationException($"The reason '{reason.Name}' requires explanatory notes.");

            if (allocation.CreditPoolId.HasValue && allocation.CreditStatus == CreditStatus.Allocated)
            {
                var reserved = await _credits.GetReservedForAllocationAsync(allocation.CreditPoolId.Value, allocationId);
                if (reserved > 0m)
                {
                    if (consumeCredit)
                        await _credits.ConsumeAsync(allocation.CreditPoolId.Value, allocationId, reserved, 1, $"Not completed: {reason.Name}", CreditTransactionType.ManualConsume);
                    else
                        await _credits.ReleaseAsync(allocation.CreditPoolId.Value, allocationId, reserved, $"Not completed: {reason.Name}");
                }
            }

            return await MarkOutcomeAsync(allocationId, OutcomeStatus.NotCompleted, reasonId, notes, outcomeDate);
        });

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
