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

    public ReportService(StudentTrackerDbContext context)
    {
        _context = context;
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
        if (withCosts)
            list = list.Where(a => a.CertificateCost > 0 && a.CashCommitmentStatus == CashCommitmentStatus.Spent).ToList();
        else
            list = list.Where(a => a.CertificateCost == 0 || a.CashCommitmentStatus != CashCommitmentStatus.Spent).ToList();
        return list;
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
}
