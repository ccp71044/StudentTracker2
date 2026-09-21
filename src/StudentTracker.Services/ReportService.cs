using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class ReportService
{
    private readonly StudentTrackerDbContext _context;
    private readonly BudgetSummaryService _budgetSummary;
    private readonly PricingService _pricing;
    private readonly CreditService _credits;
    private readonly BudgetService _budgets;
    private readonly DocumentService _documents;

    public ReportService(StudentTrackerDbContext context, BudgetSummaryService budgetSummary, PricingService pricing, CreditService credits, BudgetService budgets, DocumentService documents)
    {
        _context = context;
        _budgetSummary = budgetSummary;
        _pricing = pricing;
        _credits = credits;
        _budgets = budgets;
        _documents = documents;
    }

    #region Legacy allocation reports
    public async Task<List<Allocation>> GetCompletedStudentsAsync(DateTime? from = null, DateTime? to = null, bool includeArchived = false)
    {
        var q = _context.Allocations
            .Where(a => a.OutcomeStatus == OutcomeStatus.Completed)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsQueryable();
        if (from.HasValue) q = q.Where(a => a.OutcomeDate >= from);
        if (to.HasValue) q = q.Where(a => a.OutcomeDate < to.Value.Date.AddDays(1));
        if (!includeArchived) q = q.Where(a => a.Student == null || !a.Student.IsArchived);
        return await q.ToListAsync();
    }

    public async Task<List<Allocation>> GetWithdrawnStudentsAsync(bool withCosts, DateTime? from = null, DateTime? to = null, bool includeArchived = false)
    {
        var q = _context.Allocations
            .Where(a => a.OutcomeStatus == OutcomeStatus.Withdrawn)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsQueryable();
        if (from.HasValue) q = q.Where(a => a.OutcomeDate >= from);
        if (to.HasValue) q = q.Where(a => a.OutcomeDate < to.Value.Date.AddDays(1));
        if (!includeArchived) q = q.Where(a => a.Student == null || !a.Student.IsArchived);
        var list = await q.ToListAsync();
        var ids = list.Select(a => a.Id).ToList();
        var lossAllocationIds = await _context.CertificateCreditTransactions
            .Where(t => t.IsCreditLoss && t.AllocationId.HasValue && ids.Contains(t.AllocationId.Value))
            .Select(t => t.AllocationId!.Value)
            .Distinct()
            .ToListAsync();

        bool HasCost(Allocation a) =>
            (a.CertificateCost > 0 && a.CashCommitmentStatus == CashCommitmentStatus.Spent) ||
            lossAllocationIds.Contains(a.Id);

        return list.Where(a => HasCost(a) == withCosts).ToList();
    }

    public async Task<List<Allocation>> GetNonCompletionsAsync(DateTime? from = null, DateTime? to = null, bool includeArchived = false)
    {
        var q = _context.Allocations
            .Where(a => a.OutcomeStatus == OutcomeStatus.NotCompleted)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsQueryable();
        if (from.HasValue) q = q.Where(a => a.OutcomeDate >= from);
        if (to.HasValue) q = q.Where(a => a.OutcomeDate < to.Value.Date.AddDays(1));
        if (!includeArchived) q = q.Where(a => a.Student == null || !a.Student.IsArchived);
        return await q.ToListAsync();
    }

    public async Task<List<Allocation>> GetCertificatesAwaitingDeliveryAsync(bool includeArchived = false) => await _context.Allocations
        .Where(a => a.CertificateOrderStatus == CertificateOrderStatus.Ordered && a.CertificateDeliveryStatus == CertificateDeliveryStatus.Awaiting && (includeArchived || a.Student == null || !a.Student.IsArchived))
        .Include(a => a.Student)
        .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
        .ToListAsync();

    public async Task<List<Allocation>> GetCertificatesDeliveredAsync(DateTime? from = null, DateTime? to = null, bool includeArchived = false)
    {
        var q = _context.Allocations
            .Where(a => a.CertificateDeliveryStatus == CertificateDeliveryStatus.Delivered)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsQueryable();
        if (from.HasValue) q = q.Where(a => a.BillableDate >= from);
        if (to.HasValue) q = q.Where(a => a.BillableDate < to.Value.Date.AddDays(1));
        if (!includeArchived) q = q.Where(a => a.Student == null || !a.Student.IsArchived);
        return await q.ToListAsync();
    }
    #endregion

    #region Awaiting order
    public async Task<List<AwaitingOrderReportItem>> GetAwaitingOrderReportAsync(bool includeArchived = false)
    {
        var q = _context.Allocations
            .Where(a => (a.OutcomeStatus == OutcomeStatus.Completed && (a.CertificateOrderStatus == CertificateOrderStatus.NotReady || a.CertificateOrderStatus == CertificateOrderStatus.Ready)) || a.CertificateOrderStatus == CertificateOrderStatus.Ready)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsQueryable();
        if (!includeArchived) q = q.Where(a => a.Student == null || !a.Student.IsArchived);

        var list = await q.ToListAsync();
        return list.Select(a => new AwaitingOrderReportItem
        {
            StudentName = a.Student?.FullName ?? a.PlaceholderName ?? "",
            CourseCode = a.CourseDelivery?.CourseDefinition?.CourseCode ?? "",
            OutcomeDate = a.OutcomeDate,
            CertificateCost = a.CertificateCost,
            CertificateOrderStatus = a.CertificateOrderStatus.ToString(),
            CashCommitmentStatus = a.CashCommitmentStatus.ToString()
        }).ToList();
    }
    #endregion

    #region Deliveries
    public async Task<List<DeliveryReportItem>> GetUpcomingCourseDeliveriesAsync(DateTime? from = null)
    {
        var threshold = from ?? DateTime.UtcNow.Date;
        var deliveries = await _context.CourseDeliveries
            .Where(d => d.DeliveryStatus != "Cancelled" && d.DeliveryStatus != "Completed" && (d.StartDate == null || d.StartDate >= threshold))
            .Include(d => d.CourseDefinition)
            .ToListAsync();
        return await EnrichDeliveryItemsAsync(deliveries);
    }

    public async Task<List<DeliveryReportItem>> GetCancelledCourseDeliveriesAsync() =>
        await EnrichDeliveryItemsAsync(await _context.CourseDeliveries
            .Where(d => d.DeliveryStatus == "Cancelled")
            .Include(d => d.CourseDefinition)
            .ToListAsync());

    public async Task<List<DeliveryReportItem>> GetCompletedCourseDeliveriesAsync() =>
        await EnrichDeliveryItemsAsync(await _context.CourseDeliveries
            .Where(d => d.DeliveryStatus == "Completed")
            .Include(d => d.CourseDefinition)
            .ToListAsync());

    public async Task<List<DeliveryReportItem>> GetCapacityReportAsync()
    {
        var deliveries = await _context.CourseDeliveries
            .Where(d => d.DeliveryStatus != "Cancelled")
            .Include(d => d.CourseDefinition)
            .ToListAsync();
        return await EnrichDeliveryItemsAsync(deliveries);
    }

    private async Task<List<DeliveryReportItem>> EnrichDeliveryItemsAsync(List<CourseDelivery> deliveries)
    {
        var counts = await _context.Allocations
            .GroupBy(a => a.CourseDeliveryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return deliveries.Select(d => new DeliveryReportItem
        {
            CourseCode = d.CourseDefinition?.CourseCode ?? "",
            CourseTitle = d.CourseDefinition?.CourseTitle ?? "",
            StartDate = d.StartDate,
            EndDate = d.EndDate,
            Location = d.Location,
            TrainerName = d.TrainerName,
            Capacity = d.Capacity,
            DeliveryStatus = d.DeliveryStatus ?? "",
            EnrolledCount = counts.GetValueOrDefault(d.Id)
        }).ToList();
    }
    #endregion

    #region Allocations
    private async Task<List<AllocationReportItem>> GetAllocationStatusReportAsync(Func<Allocation, bool> predicate, bool includeArchived = false)
    {
        var q = _context.Allocations
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsQueryable();
        if (!includeArchived) q = q.Where(a => a.Student == null || !a.Student.IsArchived);
        var list = await q.ToListAsync();
        return list.Where(predicate).Select(a => new AllocationReportItem
        {
            StudentOrPlaceholder = a.Student?.FullName ?? a.PlaceholderName ?? "",
            CourseCode = a.CourseDelivery?.CourseDefinition?.CourseCode ?? "",
            AllocationStatus = a.AllocationStatus.ToString(),
            AttendanceStatus = a.AttendanceStatus.ToString(),
            OutcomeStatus = a.OutcomeStatus.ToString(),
            AllocatedAt = a.AllocatedAt,
            PlaceholderName = a.PlaceholderName
        }).ToList();
    }

    public async Task<List<AllocationReportItem>> GetActiveAllocationsAsync(bool includeArchived = false) =>
        await GetAllocationStatusReportAsync(a => a.AllocationStatus == AllocationStatus.Active, includeArchived);

    public async Task<List<AllocationReportItem>> GetTransferredAllocationsAsync(bool includeArchived = false) =>
        await GetAllocationStatusReportAsync(a => a.AllocationStatus == AllocationStatus.Transferred || a.OutcomeStatus == OutcomeStatus.Transferred, includeArchived);

    public async Task<List<AllocationReportItem>> GetCancelledAllocationsAsync(bool includeArchived = false) =>
        await GetAllocationStatusReportAsync(a => a.AllocationStatus == AllocationStatus.Cancelled || a.OutcomeStatus == OutcomeStatus.Cancelled, includeArchived);

    public async Task<List<AllocationReportItem>> GetPlaceholderAllocationsAsync(bool includeArchived = false) =>
        await GetAllocationStatusReportAsync(a => !string.IsNullOrEmpty(a.PlaceholderName), includeArchived);

    public async Task<List<AllocationReportItem>> GetAttendanceReportAsync(bool includeArchived = false)
    {
        var q = _context.Allocations
            .Where(a => a.AttendanceStatus != AttendanceStatus.NotRecorded)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsQueryable();
        if (!includeArchived) q = q.Where(a => a.Student == null || !a.Student.IsArchived);
        var list = await q.ToListAsync();
        return list.Select(a => new AllocationReportItem
        {
            StudentOrPlaceholder = a.Student?.FullName ?? a.PlaceholderName ?? "",
            CourseCode = a.CourseDelivery?.CourseDefinition?.CourseCode ?? "",
            AllocationStatus = a.AllocationStatus.ToString(),
            AttendanceStatus = a.AttendanceStatus.ToString(),
            OutcomeStatus = a.OutcomeStatus.ToString(),
            AllocatedAt = a.AllocatedAt,
            PlaceholderName = a.PlaceholderName
        }).ToList();
    }
    #endregion

    #region Course utilization
    public async Task<List<CourseUtilizationReportItem>> GetCourseUtilizationReportAsync()
    {
        var courses = await _context.CourseDefinitions.ToListAsync();
        var allocations = await _context.Allocations
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .Include(a => a.BudgetPool)
            .ToListAsync();

        var budgetTx = await _context.BudgetTransactions
            .Where(t => t.AllocationId != null)
            .ToListAsync();

        return courses.Select(c =>
        {
            var courseAllocs = allocations.Where(a => a.CourseDelivery?.CourseDefinitionId == c.Id).ToList();
            var budgetAmount = budgetTx
                .Where(t => courseAllocs.Any(a => a.Id == t.AllocationId))
                .Sum(t => t.Amount);
            return new CourseUtilizationReportItem
            {
                CourseCode = c.CourseCode,
                CourseTitle = c.CourseTitle,
                TotalAllocations = courseAllocs.Count,
                Active = courseAllocs.Count(a => a.AllocationStatus == AllocationStatus.Active),
                Completed = courseAllocs.Count(a => a.OutcomeStatus == OutcomeStatus.Completed),
                Withdrawn = courseAllocs.Count(a => a.OutcomeStatus == OutcomeStatus.Withdrawn),
                NotCompleted = courseAllocs.Count(a => a.OutcomeStatus == OutcomeStatus.NotCompleted),
                Cancelled = courseAllocs.Count(a => a.AllocationStatus == AllocationStatus.Cancelled || a.OutcomeStatus == OutcomeStatus.Cancelled),
                Transferred = courseAllocs.Count(a => a.AllocationStatus == AllocationStatus.Transferred || a.OutcomeStatus == OutcomeStatus.Transferred),
                Placeholders = courseAllocs.Count(a => !string.IsNullOrEmpty(a.PlaceholderName)),
                TotalCertificateCost = courseAllocs.Sum(a => a.CertificateCost ?? 0m),
                TotalBudgetSpent = budgetAmount
            };
        }).ToList();
    }
    #endregion

    #region Budget
    public async Task<List<BudgetTransactionSummaryItem>> GetBudgetTransactionSummaryAsync()
    {
        var query = await _context.BudgetTransactions
            .Include(t => t.Pool)
            .ToListAsync();
        return query
            .GroupBy(t => new { Pool = t.Pool?.Name ?? "Unknown", t.TransactionType })
            .Select(g => new BudgetTransactionSummaryItem
            {
                PoolName = g.Key.Pool,
                TransactionType = g.Key.TransactionType.ToString(),
                Count = g.Count(),
                TotalAmount = g.Sum(t => t.Amount)
            })
            .ToList();
    }

    public async Task<List<BudgetTransactionHistoryItem>> GetBudgetTransactionHistoryAsync(DateTime? from = null, DateTime? to = null)
    {
        var q = _context.BudgetTransactions
            .Include(t => t.Pool)
            .Include(t => t.FundingSource)
            .Include(t => t.Allocation)
            .OrderByDescending(t => t.TransactionDate)
            .AsQueryable();
        if (from.HasValue) q = q.Where(t => t.TransactionDate >= from);
        if (to.HasValue) q = q.Where(t => t.TransactionDate < to.Value.Date.AddDays(1));
        var list = await q.ToListAsync();
        return list.Select(t => new BudgetTransactionHistoryItem
        {
            PoolName = t.Pool?.Name ?? "",
            TransactionType = t.TransactionType.ToString(),
            TransactionDate = t.TransactionDate,
            Amount = t.Amount,
            FundingSource = t.FundingSource?.Name,
            Reason = t.Reason,
            AllocationDisplayId = t.Allocation?.DisplayId
        }).ToList();
    }
    #endregion

    #region Credit
    public async Task<List<CreditTransactionSummaryItem>> GetCreditTransactionSummaryAsync()
    {
        var query = await _context.CertificateCreditTransactions
            .Include(t => t.Pool)
            .ToListAsync();
        return query
            .GroupBy(t => new { Pool = t.Pool?.Name ?? "Unknown", t.TransactionType })
            .Select(g => new CreditTransactionSummaryItem
            {
                PoolName = g.Key.Pool,
                TransactionType = g.Key.TransactionType.ToString(),
                Count = g.Count(),
                TotalAmount = g.Sum(t => t.Amount),
                TotalQuantity = g.Sum(t => t.Quantity ?? 0m)
            })
            .ToList();
    }

    public async Task<List<CreditTransactionHistoryItem>> GetCreditTransactionHistoryAsync(DateTime? from = null, DateTime? to = null)
    {
        var q = _context.CertificateCreditTransactions
            .Include(t => t.Pool)
            .OrderByDescending(t => t.TransactionDateTime)
            .AsQueryable();
        if (from.HasValue) q = q.Where(t => t.TransactionDateTime >= from);
        if (to.HasValue) q = q.Where(t => t.TransactionDateTime < to.Value.Date.AddDays(1));
        var list = await q.ToListAsync();
        return list.Select(t => new CreditTransactionHistoryItem
        {
            PoolName = t.Pool?.Name ?? "",
            TransactionType = t.TransactionType.ToString(),
            TransactionDateTime = t.TransactionDateTime,
            Amount = t.Amount,
            Quantity = t.Quantity,
            SourceType = t.SourceType.ToString(),
            ExternalReference = t.ExternalTransactionId ?? t.ExternalPurchaseReference,
            Reason = t.Reason,
            IsReconciled = t.IsReconciled
        }).ToList();
    }
    #endregion

    #region Audit & import
    public async Task<List<AuditLogReportItem>> GetAuditActivityReportAsync(DateTime? from = null, DateTime? to = null)
    {
        var q = _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .AsQueryable();
        if (from.HasValue) q = q.Where(a => a.Timestamp >= from);
        if (to.HasValue) q = q.Where(a => a.Timestamp < to.Value.Date.AddDays(1));
        var list = await q.ToListAsync();
        return list.Select(a => new AuditLogReportItem
        {
            Timestamp = a.Timestamp,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityDisplayId = a.EntityDisplayId,
            Reason = a.Reason
        }).ToList();
    }

    public async Task<List<ImportReviewQueueReportItem>> GetImportReviewQueueReportAsync(string? status = null)
    {
        var q = _context.ImportReviewQueues.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(i => i.Status == status);
        else q = q.Where(i => i.Status == "Pending");
        var list = await q.OrderByDescending(i => i.CreatedAt).ToListAsync();
        return list.Select(i => new ImportReviewQueueReportItem
        {
            SourceFileName = i.SourceFileName,
            SourceSheet = i.SourceSheet,
            SourceRow = i.SourceRow,
            EntityType = i.EntityType,
            ProposedAction = i.ProposedAction,
            Issue = i.Issue,
            Status = i.Status,
            ReviewedAt = i.ReviewedAt
        }).ToList();
    }
    #endregion

    #region Certificate orders
    public async Task<List<CertificateOrderReportItem>> GetCertificateOrderReportAsync(bool? replacementsOnly = null)
    {
        var q = _context.CertificateOrders
            .Include(o => o.Allocation).ThenInclude(a => a!.Student)
            .Include(o => o.Allocation).ThenInclude(a => a!.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsQueryable();
        if (replacementsOnly == true) q = q.Where(o => o.IsReplacement);
        var list = await q.ToListAsync();

        var deliveries = await _context.CertificateDeliveries
            .Where(d => list.Select(o => o.Id).Contains(d.CertificateOrderId))
            .ToListAsync();

        return list.Select(o =>
        {
            var delivery = deliveries.FirstOrDefault(d => d.CertificateOrderId == o.Id);
            double? turnaround = null;
            if (delivery?.DeliveredDate != null && o.OrderedDate != null)
                turnaround = (delivery.DeliveredDate.Value - o.OrderedDate.Value).TotalDays;

            return new CertificateOrderReportItem
            {
                StudentName = o.Allocation?.Student?.FullName,
                CourseCode = o.Allocation?.CourseDelivery?.CourseDefinition?.CourseCode ?? "",
                Provider = o.Provider,
                OrderedDate = o.OrderedDate,
                DeliveredDate = delivery?.DeliveredDate,
                Status = o.Status.ToString(),
                IsReplacement = o.IsReplacement,
                ReplacementReason = o.ReplacementReason,
                TurnaroundDays = turnaround,
                ExternalReference = o.ExternalReference
            };
        }).ToList();
    }
    #endregion

    #region Prepaid position
    public async Task<List<PrepaidPositionReportItem>> GetPrepaidPositionByDeliveryAsync()
    {
        var pools = await _context.BudgetPools
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
        var poolSummaries = (await _budgetSummary.GetPoolSummariesAsync()).ToDictionary(s => s.PoolId);
        var prices = await _pricing.GetCurrentPricesAsync();

        var allocations = await _context.Allocations
            .Where(a => a.BudgetPoolId != null)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .Include(a => a.Student)
            .AsNoTracking()
            .ToListAsync();

        var rows = new List<PrepaidPositionReportItem>();
        foreach (var pool in pools)
        {
            var poolAllocs = allocations.Where(a => a.BudgetPoolId == pool.Id).ToList();
            var summary = poolSummaries.GetValueOrDefault(pool.Id);
            var poolFunds = summary?.FundsAdded ?? 0m;
            var poolAvailable = summary?.Free ?? 0m;
            var poolCommitted = summary?.Committed ?? 0m;
            var poolSpent = summary?.Spent ?? 0m;

            var deliveryGroups = poolAllocs
                .Where(a => a.CourseDelivery != null)
                .GroupBy(a => a.CourseDelivery!)
                .OrderBy(g => g.Key.StartDate)
                .ToList();

            if (deliveryGroups.Count == 0)
            {
                rows.Add(new PrepaidPositionReportItem
                {
                    PoolDisplayId = pool.DisplayId ?? pool.Name,
                    PoolName = pool.Name,
                    FinancialPeriod = pool.FinancialPeriod,
                    FundsAdded = poolFunds,
                    Available = poolAvailable,
                    Committed = poolCommitted,
                    Spent = poolSpent,
                    CompletionsRemaining = 0
                });
            }

            foreach (var group in deliveryGroups)
            {
                var delivery = group.Key;
                var course = delivery.CourseDefinition;
                var price = course != null && prices.TryGetValue(course.Id, out var p) ? p : course?.DefaultCertificateCost;

                var reserved = group.Count(a => !string.IsNullOrEmpty(a.PlaceholderName) && a.AllocationStatus == AllocationStatus.Reserved);
                var pending = group.Count(a => a.StudentId.HasValue && a.OutcomeStatus == OutcomeStatus.Pending);
                var completedAwaiting = group.Count(a => a.OutcomeStatus == OutcomeStatus.Completed && a.CashCommitmentStatus == CashCommitmentStatus.Pending);
                var toBill = group.Count(a => a.IsBillable && a.ExportedInBatchId == null);
                var committed = group.Where(a => a.CashCommitmentStatus == CashCommitmentStatus.Pending).Sum(a => a.CertificateCost ?? price ?? 0m);
                var spent = group.Where(a => a.CashCommitmentStatus == CashCommitmentStatus.Spent).Sum(a => a.CertificateCost ?? price ?? 0m);
                var completionsRemaining = price.HasValue && price.Value > 0 && poolAvailable > 0
                    ? (int)Math.Floor(poolAvailable / price.Value)
                    : 0;

                rows.Add(new PrepaidPositionReportItem
                {
                    PoolDisplayId = pool.DisplayId ?? pool.Name,
                    PoolName = pool.Name,
                    FinancialPeriod = pool.FinancialPeriod,
                    DeliveryDisplayId = delivery.DisplayId,
                    DeliveryDate = delivery.StartDate,
                    CourseCode = course?.CourseCode ?? "",
                    CourseTitle = course?.CourseTitle ?? "",
                    Provider = course?.Provider,
                    FundsAdded = poolFunds,
                    Available = poolAvailable,
                    Committed = committed,
                    Spent = spent,
                    ReservedPlaces = reserved,
                    AssignedPending = pending,
                    CompletedAwaitingSpend = completedAwaiting,
                    CompletionsRemaining = completionsRemaining,
                    TotalAllocations = group.Count(),
                    BillableUnexported = toBill,
                    AllenCost = price
                });
            }
        }

        return rows;
    }

    public async Task<int> GetUnbilledCountAsync(Guid poolId, Guid? courseDefinitionId = null)
    {
        var q = _context.Allocations
            .AsNoTracking()
            .Where(a => a.BudgetPoolId == poolId && a.IsBillable && a.ExportedInBatchId == null);

        if (courseDefinitionId.HasValue)
            q = q.Where(a => a.CourseDelivery != null && a.CourseDelivery.CourseDefinitionId == courseDefinitionId.Value);

        return await q.CountAsync();
    }
    #endregion

    #region Invoicer / documents
    public async Task<List<BillableCertificateReportItem>> GetBillableCertificatesForInvoicerAsync(bool includeArchived = false)
    {
        var q = _context.Allocations
            .Where(a => a.IsBillable)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsNoTracking();

        if (!includeArchived) q = q.Where(a => a.Student == null || !a.Student.IsArchived);

        var list = await q.ToListAsync();
        return list.Select(a => new BillableCertificateReportItem
        {
            AllocationDisplayId = a.DisplayId,
            StudentName = a.Student?.FullName ?? a.PlaceholderName ?? "",
            CourseCode = a.CourseDelivery?.CourseDefinition?.CourseCode ?? "",
            OutcomeDate = a.OutcomeDate,
            CertificateCost = a.CertificateCost,
            IsExported = a.ExportedInBatchId.HasValue,
            ExportBatchId = a.ExportedInBatchId?.ToString("N")[..8]
        }).ToList();
    }

    public async Task<List<TbcDeliveryReportItem>> GetTbcCourseDeliveriesAsync()
    {
        var deliveries = await _context.CourseDeliveries
            .Where(d => d.DeliveryStatus == "TBC")
            .Include(d => d.CourseDefinition)
            .AsNoTracking()
            .ToListAsync();

        return deliveries.Select(d => new TbcDeliveryReportItem
        {
            CourseCode = d.CourseDefinition?.CourseCode ?? "",
            CourseTitle = d.CourseDefinition?.CourseTitle ?? "",
            Location = d.Location,
            TrainerName = d.TrainerName,
            DeliveryStatus = d.DeliveryStatus ?? "TBC"
        }).ToList();
    }
    #endregion

    #region Additional reports
    public async Task<List<FundingSourceReportItem>> GetFundingSourcesAsync(DateTime? from = null, DateTime? to = null)
    {
        var fromDate = from ?? DateTime.MinValue;
        var toDate = to.HasValue ? to.Value.Date.AddDays(1) : DateTime.MaxValue;

        var prepaid = await _context.ClientPrepaidEntitlementTransactions
            .Where(t => t.TransactionType == ClientPrepaidEntitlementTransactionType.PrepaidPlacesAdded && t.TransactionDate >= fromDate && t.TransactionDate < toDate)
            .Include(t => t.Invoice)
            .AsNoTracking()
            .ToListAsync();

        var invoices = await _context.Invoices
            .Where(i => i.AmountAssignedToStudentTracker > 0 && i.InvoiceDate >= fromDate && i.InvoiceDate < toDate)
            .AsNoTracking()
            .ToListAsync();

        var budgetFunds = await _context.BudgetTransactions
            .Where(t => t.TransactionType == BudgetTransactionType.FundsAdded && t.TransactionDate >= fromDate && t.TransactionDate < toDate)
            .Include(t => t.FundingSource)
            .AsNoTracking()
            .ToListAsync();

        var bySource = new Dictionary<string, FundingSourceReportItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in prepaid)
        {
            var name = p.Invoice?.Customer ?? p.Reason ?? "Prepaid";
            if (!bySource.TryGetValue(name, out var item))
            {
                item = new FundingSourceReportItem { SourceName = name };
                bySource[name] = item;
            }
            item.PrepaidPlacesAdded += p.Quantity;
            item.PrepaidValue += p.MonetaryReferenceValue ?? 0m;
        }

        foreach (var inv in invoices)
        {
            var name = string.IsNullOrWhiteSpace(inv.Customer) ? $"Invoice {inv.InvoiceNumber}" : inv.Customer;
            if (!bySource.TryGetValue(name, out var item))
            {
                item = new FundingSourceReportItem { SourceName = name };
                bySource[name] = item;
            }
            item.InvoiceAmountAssigned += inv.AmountAssignedToStudentTracker ?? 0m;
        }

        foreach (var b in budgetFunds)
        {
            var name = b.FundingSource?.Name ?? b.Reason ?? "Budget Top-up";
            if (!bySource.TryGetValue(name, out var item))
            {
                item = new FundingSourceReportItem { SourceName = name };
                bySource[name] = item;
            }
            item.BudgetFundsAdded += b.Amount;
        }

        foreach (var item in bySource.Values)
            item.NetFunding = item.PrepaidValue + item.InvoiceAmountAssigned + item.BudgetFundsAdded;

        return bySource.Values.OrderByDescending(x => x.NetFunding).ToList();
    }

    public async Task<List<MissingDocumentReportItem>> GetMissingDocumentsAsync()
    {
        var completed = await _context.Allocations
            .Where(a => a.OutcomeStatus == OutcomeStatus.Completed)
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsNoTracking()
            .ToListAsync();

        var linkedIds = (await _context.DocumentLinks
            .Where(l => l.EntityType == nameof(Allocation))
            .Select(l => l.EntityId)
            .ToListAsync())
            .ToHashSet();

        var result = new List<MissingDocumentReportItem>();
        foreach (var a in completed.Where(a => !linkedIds.Contains(a.Id)))
        {
            result.Add(new MissingDocumentReportItem
            {
                AllocationDisplayId = a.DisplayId,
                StudentName = a.Student?.FullName,
                CourseCode = a.CourseDelivery?.CourseDefinition?.CourseCode ?? "",
                DeliveryDisplayId = a.CourseDelivery?.DisplayId,
                MissingDocumentType = "Completion Evidence"
            });
        }

        var orderedWithoutDelivery = await _context.CertificateOrders
            .Where(o => o.Status == CertificateOrderStatus.Ordered)
            .Include(o => o.Allocation).ThenInclude(a => a!.Student)
            .Include(o => o.Allocation).ThenInclude(a => a!.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsNoTracking()
            .ToListAsync();

        foreach (var o in orderedWithoutDelivery.Where(o => o.Allocation != null))
        {
            var a = o.Allocation!;
            result.Add(new MissingDocumentReportItem
            {
                AllocationDisplayId = a.DisplayId,
                StudentName = a.Student?.FullName,
                CourseCode = a.CourseDelivery?.CourseDefinition?.CourseCode ?? "",
                DeliveryDisplayId = a.CourseDelivery?.DisplayId,
                MissingDocumentType = "Certificate Delivery Evidence"
            });
        }

        return result;
    }

    public async Task<List<CreditReallocationReportItem>> GetCreditReallocationHistoryAsync(DateTime? from = null, DateTime? to = null)
    {
        var q = _context.CertificateCreditTransactions
            .Where(t => t.TransactionType == CreditTransactionType.ReallocateOut || t.TransactionType == CreditTransactionType.ReallocateIn)
            .Include(t => t.Pool)
            .AsNoTracking()
            .AsQueryable();
        if (from.HasValue) q = q.Where(t => t.TransactionDateTime >= from);
        if (to.HasValue) q = q.Where(t => t.TransactionDateTime < to.Value.Date.AddDays(1));

        var list = await q.ToListAsync();
        return list.Select(t => new CreditReallocationReportItem
        {
            PoolName = t.Pool?.Name ?? "Unknown",
            TransactionType = t.TransactionType.ToString(),
            TransactionDateTime = t.TransactionDateTime,
            Amount = t.Amount,
            Quantity = t.Quantity,
            Reason = t.Reason,
            AllocationDisplayId = t.Allocation?.DisplayId
        }).ToList();
    }

    public async Task<List<CertificateCreditPoolSummaryReportItem>> GetCertificateCreditPoolSummaryAsync()
    {
        var pools = await _context.CertificateCreditPools
            .AsNoTracking()
            .ToListAsync();

        var tx = await _context.CertificateCreditTransactions
            .AsNoTracking()
            .ToListAsync();

        var allocated = await _context.Allocations
            .Where(a => a.CreditStatus == CreditStatus.Allocated)
            .AsNoTracking()
            .ToListAsync();

        return pools.Select(p =>
        {
            var poolTx = tx.Where(t => t.PoolId == p.Id).ToList();
            var topUp = poolTx.Where(t => t.TransactionType == CreditTransactionType.TopUp).Sum(t => Math.Abs(t.Amount));
            var consumed = poolTx.Where(t => t.TransactionType == CreditTransactionType.OrderConsume || t.TransactionType == CreditTransactionType.ManualConsume).Sum(t => Math.Abs(t.Amount));
            var allocatedOut = poolTx.Where(t => t.TransactionType == CreditTransactionType.Allocate).Sum(t => Math.Abs(t.Amount));
            return new CertificateCreditPoolSummaryReportItem
            {
                PoolDisplayId = p.DisplayId ?? string.Empty,
                PoolName = p.Name ?? string.Empty,
                TopUp = topUp,
                Allocated = allocatedOut,
                Consumed = consumed,
                Available = topUp - allocatedOut - consumed,
                AllocationCount = allocated.Count(a => a.CreditPoolId == p.Id)
            };
        }).ToList();
    }

    public async Task<List<CreditConsumedWithoutCompletionReportItem>> GetCreditsConsumedWithoutCompletionAsync()
    {
        var q = _context.CertificateCreditTransactions
            .Where(t => (t.TransactionType == CreditTransactionType.OrderConsume || t.TransactionType == CreditTransactionType.ManualConsume) && t.Allocation != null)
            .Include(t => t.Allocation).ThenInclude(a => a!.Student)
            .Include(t => t.Allocation).ThenInclude(a => a!.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .AsNoTracking();

        var list = await q.ToListAsync();
        return list
            .Where(t => t.Allocation!.OutcomeStatus != OutcomeStatus.Completed)
            .Select(t => new CreditConsumedWithoutCompletionReportItem
            {
                AllocationDisplayId = t.Allocation!.DisplayId,
                StudentName = t.Allocation.Student?.FullName,
                CourseCode = t.Allocation.CourseDelivery?.CourseDefinition?.CourseCode ?? "",
                AmountConsumed = Math.Abs(t.Amount),
                OutcomeStatus = t.Allocation.OutcomeStatus.ToString(),
                ConsumedAt = t.TransactionDateTime
            })
            .ToList();
    }
    #endregion

    #region CSV export
    public async Task<byte[]> ExportCsvAsync<T>(IEnumerable<T> records) where T : class
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Encoding = Encoding.UTF8 });
        await csv.WriteRecordsAsync(records);
        await writer.FlushAsync();
        return ms.ToArray();
    }
    #endregion

    #region Design-section reports retained from the ledger work

    /// <summary>
    /// 21. Documents whose managed file is absent from disk (distinct from
    /// <see cref="GetMissingDocumentsAsync"/>, which lists completed allocations with no paperwork).
    /// </summary>
    public async Task<List<Document>> GetMissingDocumentFilesAsync()
    {
        var documents = await _context.Documents
            .Where(d => d.Status != DocumentStatus.Archived)
            .ToListAsync();
        return documents
            .Where(d => d.Status == DocumentStatus.Missing || !File.Exists(_documents.GetFullPath(d)))
            .ToList();
    }

    // 25. Audit Activity
    public async Task<List<AuditLog>> GetAuditActivityAsync(DateTime? from = null, DateTime? to = null, string? entityType = null)
    {
        var query = _context.AuditLogs.AsQueryable();
        if (from.HasValue) query = query.Where(l => l.Timestamp >= from);
        if (to.HasValue) query = query.Where(l => l.Timestamp <= to);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(l => l.EntityType == entityType);
        return await query.OrderByDescending(l => l.Timestamp).ToListAsync();
    }

    // 24. Billable Certificates for Invoicer
    public Task<List<Allocation>> GetBillableCertificatesAsync(bool includeExported = false)
    {
        var query = Allocations().Where(a => a.IsBillable);
        if (!includeExported) query = query.Where(a => a.ExportedInBatchId == null);
        return query.OrderBy(a => a.BillableDate).ToListAsync();
    }

    // 17 & 19. Budget Summary / Actual vs Forecast
    public async Task<List<BudgetSummaryRow>> GetBudgetSummaryAsync()
    {
        var pools = await _context.BudgetPools.OrderBy(p => p.Name).ToListAsync();
        var rows = new List<BudgetSummaryRow>();
        foreach (var pool in pools)
        {
            var balance = await _budgets.GetBalanceAsync(pool.Id);
            rows.Add(new BudgetSummaryRow(pool.Name, balance.FundsAdded, balance.ActualExpenditure,
                balance.PendingCommitments, balance.ActualAvailable, balance.ForecastAvailable));
        }
        return rows;
    }

    // 10. Certificates Awaiting Order
    public Task<List<Allocation>> GetCertificatesAwaitingOrderAsync() =>
        Allocations()
            .Where(a => a.OutcomeStatus == OutcomeStatus.Completed
                        && (a.CertificateOrderStatus == CertificateOrderStatus.NotReady
                            || a.CertificateOrderStatus == CertificateOrderStatus.Ready))
            .ToListAsync();

    // 11. Certificates Ordered
    public async Task<List<CertificateOrder>> GetCertificatesOrderedAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _context.CertificateOrders
            .Where(o => o.Status == CertificateOrderStatus.Ordered)
            .AsQueryable();
        if (from.HasValue) query = query.Where(o => o.OrderedDate >= from);
        if (to.HasValue) query = query.Where(o => o.OrderedDate <= to);
        return await query.OrderByDescending(o => o.OrderedDate).ToListAsync();
    }

    // 14. Certificate Credit Pool Summary
    public async Task<List<CreditPoolSummaryRow>> GetCreditPoolSummaryAsync()
    {
        var pools = await _context.CertificateCreditPools.OrderBy(p => p.Name).ToListAsync();
        var rows = new List<CreditPoolSummaryRow>();
        foreach (var pool in pools)
        {
            var balance = await _credits.GetBalanceAsync(pool.Id);
            rows.Add(new CreditPoolSummaryRow(pool.Name, pool.Provider, balance.Loaded, balance.Allocated,
                balance.Consumed, balance.Expired, balance.Unavailable, balance.Available));
        }
        return rows;
    }

    // 3. Course Delivery Outcomes
    public Task<List<Allocation>> GetDeliveryOutcomesAsync(Guid deliveryId) =>
        Allocations()
            .Where(a => a.CourseDeliveryId == deliveryId && a.OutcomeStatus != OutcomeStatus.Pending)
            .OrderBy(a => a.OutcomeStatus)
            .ToListAsync();

    // 21. Invoice Reconciliation
    public async Task<List<InvoiceReconciliationRow>> GetInvoiceReconciliationAsync()
    {
        var invoices = await _context.Invoices.OrderByDescending(i => i.InvoiceDate).ToListAsync();
        var creditByInvoice = (await _context.CertificateCreditTransactions
                .Where(t => t.InvoiceId != null)
                .Select(t => new { t.InvoiceId, t.Amount })
                .ToListAsync())
            .GroupBy(t => t.InvoiceId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Abs(x.Amount)));

        return invoices.Select(i =>
        {
            var matched = creditByInvoice.TryGetValue(i.Id, out var value) ? value : 0m;
            return new InvoiceReconciliationRow(
                i.InvoiceNumber,
                i.Customer,
                i.InvoiceDate,
                i.TotalAmount ?? 0m,
                i.AmountAssignedToStudentTracker ?? 0m,
                matched,
                (i.AmountAssignedToStudentTracker ?? 0m) - matched);
        }).ToList();
    }

    // 2. Course Delivery Participant List
    public Task<List<Allocation>> GetParticipantListAsync(Guid deliveryId) =>
        Allocations().Where(a => a.CourseDeliveryId == deliveryId).OrderBy(a => a.AllocatedAt).ToListAsync();

    // 18. Pending Commitments
    public Task<List<BudgetTransaction>> GetPendingCommitmentsAsync(Guid? poolId = null)
    {
        var query = _context.BudgetTransactions
            .Where(t => t.TransactionType == BudgetTransactionType.CommitmentCreated);
        if (poolId.HasValue) query = query.Where(t => t.PoolId == poolId);
        return query.OrderByDescending(t => t.TransactionDate).ToListAsync();
    }

    // 1. Student Course History
    public Task<List<Allocation>> GetStudentCourseHistoryAsync(Guid studentId) =>
        Allocations().Where(a => a.StudentId == studentId).OrderByDescending(a => a.AllocatedAt).ToListAsync();

    // 23. TBC Course Deliveries
    public Task<List<CourseDelivery>> GetTbcDeliveriesAsync() =>
        _context.CourseDeliveries
            .Include(d => d.CourseDefinition)
            .Where(d => d.DateStatus == DeliveryDateStatus.TBC
                        || d.DateStatus == DeliveryDateStatus.Blank
                        || d.StartDate == null)
            .OrderBy(d => d.DisplayId)
            .ToListAsync();

    private IQueryable<Allocation> Allocations() => _context.Allocations
        .Include(a => a.Student)
        .Include(a => a.OutcomeReason)
        .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition);

    private static IQueryable<Allocation> ApplyOutcomeDates(IQueryable<Allocation> query, DateTime? from, DateTime? to)
    {
        if (from.HasValue) query = query.Where(a => a.OutcomeDate >= from);
        if (to.HasValue) query = query.Where(a => a.OutcomeDate <= to);
        return query;
    }

    #endregion
}

public record CreditPoolSummaryRow(
    string PoolName,
    string? Provider,
    decimal Loaded,
    decimal Allocated,
    decimal Consumed,
    decimal Expired,
    decimal Unavailable,
    decimal Available);

public record BudgetSummaryRow(
    string PoolName,
    decimal FundsAdded,
    decimal ActualExpenditure,
    decimal PendingCommitments,
    decimal ActualAvailable,
    decimal ForecastAvailable);

public record InvoiceReconciliationRow(
    string? InvoiceNumber,
    string? Customer,
    DateTime? InvoiceDate,
    decimal TotalAmount,
    decimal AssignedToStudentTracker,
    decimal MatchedToCredits,
    decimal Unmatched);
