using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Common;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class DisplayIdReconciliationService
{
    private readonly StudentTrackerDbContext _context;
    private readonly AuditService _audit;

    public DisplayIdReconciliationService(StudentTrackerDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<ReconciliationReport> CheckAsync()
    {
        var report = new ReconciliationReport();

        var budget = await _context.BudgetTransactions
            .Where(x => x.DisplayId != null)
            .Select(x => new { x.Id, x.DisplayId })
            .ToListAsync();
        report.BudgetTransactionDuplicates = FindDuplicates(budget.Select(x => (x.Id, x.DisplayId!)).ToList());

        var credit = await _context.CertificateCreditTransactions
            .Where(x => x.DisplayId != null)
            .Select(x => new { x.Id, x.DisplayId })
            .ToListAsync();
        report.CertificateCreditTransactionDuplicates = FindDuplicates(credit.Select(x => (x.Id, x.DisplayId!)).ToList());

        return report;
    }

    public async Task<int> ResequenceAsync<T>(string prefix) where T : EntityBase, IDisplayId
    {
        var records = await _context.Set<T>()
            .Where(x => x.DisplayId != null)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        int changed = 0;
        for (int i = 0; i < records.Count; i++)
        {
            var expected = $"{prefix}-{(i + 1):D4}";
            if (records[i].DisplayId != expected)
            {
                records[i].DisplayId = expected;
                changed++;
            }
        }

        if (changed > 0)
        {
            await _context.SaveChangesAsync();
            _audit.Record("Resequenced", typeof(T).Name, Guid.NewGuid(), null, null, new { Count = records.Count, Changed = changed });
        }

        return changed;
    }

    private static List<DuplicateGroup> FindDuplicates(List<(Guid Id, string DisplayId)> items)
    {
        return items
            .GroupBy(x => x.DisplayId)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateGroup
            {
                DisplayId = g.Key,
                Count = g.Count(),
                Ids = g.Select(x => x.Id).ToList()
            })
            .ToList();
    }
}

public class ReconciliationReport
{
    public List<DuplicateGroup> BudgetTransactionDuplicates { get; set; } = new();
    public List<DuplicateGroup> CertificateCreditTransactionDuplicates { get; set; } = new();
}

public class DuplicateGroup
{
    public string DisplayId { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<Guid> Ids { get; set; } = new();
}
