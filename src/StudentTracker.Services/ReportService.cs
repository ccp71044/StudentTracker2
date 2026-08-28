using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

/// <summary>
/// The 25 mandatory reports from design section 14. Every figure is derived from stored
/// transactions and records; nothing is cached or recalculated from a stored balance.
/// </summary>
public class ReportService
{
    private readonly StudentTrackerDbContext _context;
    private readonly CreditService _credits;
    private readonly BudgetService _budgets;
    private readonly DocumentService _documents;

    public ReportService(StudentTrackerDbContext context, CreditService credits, BudgetService budgets, DocumentService documents)
    {
        _context = context;
        _credits = credits;
        _budgets = budgets;
        _documents = documents;
    }

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

    // 1. Student Course History
    public Task<List<Allocation>> GetStudentCourseHistoryAsync(Guid studentId) =>
        Allocations().Where(a => a.StudentId == studentId).OrderByDescending(a => a.AllocatedAt).ToListAsync();

    // 2. Course Delivery Participant List
    public Task<List<Allocation>> GetParticipantListAsync(Guid deliveryId) =>
        Allocations().Where(a => a.CourseDeliveryId == deliveryId).OrderBy(a => a.AllocatedAt).ToListAsync();

    // 3. Course Delivery Outcomes
    public Task<List<Allocation>> GetDeliveryOutcomesAsync(Guid deliveryId) =>
        Allocations()
            .Where(a => a.CourseDeliveryId == deliveryId && a.OutcomeStatus != OutcomeStatus.Pending)
            .OrderBy(a => a.OutcomeStatus)
            .ToListAsync();

    // 4. Completed Students
    public Task<List<Allocation>> GetCompletedStudentsAsync(DateTime? from = null, DateTime? to = null) =>
        ApplyOutcomeDates(Allocations().Where(a => a.OutcomeStatus == OutcomeStatus.Completed), from, to).ToListAsync();

    // 5. Withdrawn Students
    public Task<List<Allocation>> GetWithdrawnStudentsAsync(DateTime? from = null, DateTime? to = null) =>
        ApplyOutcomeDates(Allocations().Where(a => a.OutcomeStatus == OutcomeStatus.Withdrawn), from, to).ToListAsync();

    // 6 & 7. Withdrawn Students with / without costs.
    // "With costs" means the withdrawal left real money spent or credit that could not be reused.
    public async Task<List<Allocation>> GetWithdrawnStudentsAsync(bool withCosts, DateTime? from = null, DateTime? to = null)
    {
        var withdrawn = await GetWithdrawnStudentsAsync(from, to);
        var ids = withdrawn.Select(a => a.Id).ToList();
        var lossAllocationIds = await _context.CertificateCreditTransactions
            .Where(t => t.IsCreditLoss && t.AllocationId.HasValue && ids.Contains(t.AllocationId.Value))
            .Select(t => t.AllocationId!.Value)
            .Distinct()
            .ToListAsync();

        bool HasCost(Allocation a) =>
            a.CashCommitmentStatus == CashCommitmentStatus.Spent || lossAllocationIds.Contains(a.Id);

        return withdrawn.Where(a => HasCost(a) == withCosts).ToList();
    }

    // 8. Non-Completions
    public Task<List<Allocation>> GetNonCompletionsAsync(DateTime? from = null, DateTime? to = null) =>
        ApplyOutcomeDates(Allocations().Where(a => a.OutcomeStatus == OutcomeStatus.NotCompleted), from, to).ToListAsync();

    // 9. Credits Consumed Without Completion
    public Task<List<CertificateCreditTransaction>> GetCreditsConsumedWithoutCompletionAsync() =>
        _credits.GetConsumedWithoutCompletionAsync();

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

    // 12. Certificates Awaiting Delivery
    public Task<List<Allocation>> GetCertificatesAwaitingDeliveryAsync() =>
        Allocations()
            .Where(a => a.CertificateOrderStatus == CertificateOrderStatus.Ordered
                        && a.CertificateDeliveryStatus == CertificateDeliveryStatus.Awaiting)
            .ToListAsync();

    // 13. Certificates Delivered
    public async Task<List<Allocation>> GetCertificatesDeliveredAsync(DateTime? from = null, DateTime? to = null)
    {
        var deliveredIds = _context.CertificateDeliveries.AsQueryable();
        if (from.HasValue) deliveredIds = deliveredIds.Where(d => d.DeliveredDate >= from);
        if (to.HasValue) deliveredIds = deliveredIds.Where(d => d.DeliveredDate <= to);
        var orderIds = await deliveredIds.Select(d => d.CertificateOrderId).Distinct().ToListAsync();
        var allocationIds = await _context.CertificateOrders
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => o.AllocationId)
            .ToListAsync();
        return await Allocations().Where(a => allocationIds.Contains(a.Id)).ToListAsync();
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

    // 15. Credit Transaction History
    public async Task<List<CertificateCreditTransaction>> GetCreditTransactionHistoryAsync(Guid? poolId = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.CertificateCreditTransactions.AsQueryable();
        if (poolId.HasValue) query = query.Where(t => t.PoolId == poolId);
        if (from.HasValue) query = query.Where(t => t.TransactionDateTime >= from);
        if (to.HasValue) query = query.Where(t => t.TransactionDateTime <= to);
        return await query.OrderByDescending(t => t.TransactionDateTime).ToListAsync();
    }

    // 16. Credit Reallocation History
    public Task<List<CertificateCreditTransaction>> GetCreditReallocationHistoryAsync() =>
        _context.CertificateCreditTransactions
            .Where(t => t.TransactionType == CreditTransactionType.ReallocateOut
                        || t.TransactionType == CreditTransactionType.ReallocateIn)
            .OrderByDescending(t => t.TransactionDateTime)
            .ToListAsync();

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

    // 18. Pending Commitments
    public Task<List<BudgetTransaction>> GetPendingCommitmentsAsync(Guid? poolId = null)
    {
        var query = _context.BudgetTransactions
            .Where(t => t.TransactionType == BudgetTransactionType.CommitmentCreated);
        if (poolId.HasValue) query = query.Where(t => t.PoolId == poolId);
        return query.OrderByDescending(t => t.TransactionDate).ToListAsync();
    }

    // 20. Funding Sources
    public Task<List<FundingSource>> GetFundingSourcesAsync() =>
        _context.FundingSources.OrderBy(f => f.Name).ToListAsync();

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

    // 22. Missing Documents: rows whose file is no longer on disk.
    public async Task<List<Document>> GetMissingDocumentsAsync()
    {
        var documents = await _context.Documents
            .Where(d => d.Status != DocumentStatus.Archived)
            .ToListAsync();
        return documents
            .Where(d => d.Status == DocumentStatus.Missing || !File.Exists(_documents.GetFullPath(d)))
            .ToList();
    }

    // 23. TBC Course Deliveries
    public Task<List<CourseDelivery>> GetTbcDeliveriesAsync() =>
        _context.CourseDeliveries
            .Include(d => d.CourseDefinition)
            .Where(d => d.DateStatus == DeliveryDateStatus.TBC
                        || d.DateStatus == DeliveryDateStatus.Blank
                        || d.StartDate == null)
            .OrderBy(d => d.DisplayId)
            .ToListAsync();

    // 24. Billable Certificates for Invoicer
    public Task<List<Allocation>> GetBillableCertificatesAsync(bool includeExported = false)
    {
        var query = Allocations().Where(a => a.IsBillable);
        if (!includeExported) query = query.Where(a => a.ExportedInBatchId == null);
        return query.OrderBy(a => a.BillableDate).ToListAsync();
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

    public async Task<byte[]> ExportCsvAsync<T>(IEnumerable<T> records) where T : class
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Encoding = Encoding.UTF8 });
        await csv.WriteRecordsAsync(records);
        await writer.FlushAsync();
        return ms.ToArray();
    }
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
