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

    public async Task<List<Allocation>> GetCertificatesAwaitingOrderAsync(bool includeArchived = false) => await _context.Allocations
        .Where(a => (a.OutcomeStatus == OutcomeStatus.Completed && a.CertificateOrderStatus == CertificateOrderStatus.NotReady || a.CertificateOrderStatus == CertificateOrderStatus.Ready) && (includeArchived || a.Student == null || !a.Student.IsArchived))
        .Include(a => a.Student)
        .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
        .ToListAsync();

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
