using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

/// <summary>
/// Produces read-only, file-based Invoice Manager reference snapshots of budget-pool cost position.
/// Student Tracker remains the source of truth for completions, pending commitments and budget pool
/// balances; this exporter does not grant invoice/payment authority and does not modify allocations.
/// </summary>
public class InvoicerReferenceExportService
{
    public const string SchemaVersion = "1.0";

    private readonly StudentTrackerDbContext _context;
    private readonly DataLocationService _dataLocation;
    private readonly PricingService _pricing;
    private readonly AuditService _audit;

    public InvoicerReferenceExportService(StudentTrackerDbContext context, DataLocationService dataLocation, PricingService pricing, AuditService audit)
    {
        _context = context;
        _dataLocation = dataLocation;
        _pricing = pricing;
        _audit = audit;
    }

    /// <summary>
    /// Builds and writes a versioned JSON and CSV snapshot of current budget-pool cost position to
    /// Integration/InvoicerExport. File names include a UTC timestamp and are duplicate-safe.
    /// </summary>
    public async Task<InvoicerCostPositionExportResult> ExportCostPositionSnapshotAsync(string? notes = null)
    {
        var snapshot = await BuildSnapshotAsync(notes);
        var exportDir = Path.Combine(_dataLocation.IntegrationPath, "InvoicerExport");
        Directory.CreateDirectory(exportDir);

        var timestamp = snapshot.GeneratedAt;
        var baseName = $"invoicer-cost-position-{timestamp:yyyyMMdd-HHmmss}";
        var jsonPath = GetUniquePath(exportDir, baseName, ".json");
        var csvPath = Path.ChangeExtension(jsonPath, ".csv");

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(snapshot, jsonOptions), Encoding.UTF8);

        var rows = Flatten(snapshot);
        await using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
        await using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Encoding = Encoding.UTF8 }))
        {
            await csv.WriteRecordsAsync(rows);
        }

        _audit.Record(
            "Exported",
            "InvoicerCostPositionSnapshot",
            snapshot.SnapshotId,
            snapshot.SnapshotId.ToString("N")[..8],
            null,
            new
            {
                JsonPath = jsonPath,
                CsvPath = csvPath,
                PoolCount = snapshot.Pools.Count,
                CourseCount = snapshot.Pools.Sum(p => p.Courses.Count)
            },
            notes);
        await _context.SaveChangesAsync();

        return new InvoicerCostPositionExportResult
        {
            SnapshotId = snapshot.SnapshotId,
            JsonPath = jsonPath,
            CsvPath = csvPath,
            GeneratedAt = snapshot.GeneratedAt,
            PoolCount = snapshot.Pools.Count,
            CourseCount = snapshot.Pools.Sum(p => p.Courses.Count)
        };
    }

    private static string GetUniquePath(string directory, string baseName, string extension)
    {
        var path = Path.Combine(directory, $"{baseName}{extension}");
        if (!File.Exists(path))
            return path;

        var counter = 1;
        while (true)
        {
            path = Path.Combine(directory, $"{baseName}_{counter}{extension}");
            if (!File.Exists(path))
                return path;
            counter++;
        }
    }

    public async Task<InvoicerCostPositionSnapshot> BuildSnapshotAsync(string? notes)
    {
        var pools = await _context.BudgetPools
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var transactions = await _context.BudgetTransactions.ToListAsync();
        var allocations = await _context.Allocations
            .Include(a => a.Student)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .ToListAsync();

        var courseDefinitions = await _context.CourseDefinitions
            .Where(c => c.IsActive)
            .OrderBy(c => c.CourseCode)
            .ToListAsync();
        var prices = await _pricing.GetCurrentPricesAsync();

        var snapshot = new InvoicerCostPositionSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            GeneratedAt = DateTime.UtcNow,
            Notes = notes,
            Pools = new List<PoolCostPosition>()
        };

        foreach (var pool in pools)
        {
            var poolTx = transactions.Where(t => t.PoolId == pool.Id).ToList();
            var added = poolTx.Where(t => t.TransactionType == BudgetTransactionType.FundsAdded).Sum(t => t.Amount);
            var spent = -poolTx.Where(t => t.TransactionType == BudgetTransactionType.ExpenseRecognised).Sum(t => t.Amount);
            var committed = -poolTx
                .Where(t => t.TransactionType == BudgetTransactionType.CommitmentCreated || t.TransactionType == BudgetTransactionType.CommitmentReleased)
                .Sum(t => t.Amount);
            var adjustments = poolTx
                .Where(t => t.TransactionType is BudgetTransactionType.Adjustment or BudgetTransactionType.Reimbursement or BudgetTransactionType.Reversal)
                .Sum(t => t.Amount);

            var funds = added + adjustments;
            var available = funds - spent - committed;

            var poolAllocs = allocations.Where(a => a.BudgetPoolId == pool.Id).ToList();

            var poolPosition = new PoolCostPosition
            {
                PoolId = pool.Id,
                PoolDisplayId = pool.DisplayId,
                PoolName = pool.Name ?? string.Empty,
                FinancialPeriod = pool.FinancialPeriod,
                IsActive = pool.IsActive,
                FundsAdded = funds,
                Committed = committed,
                Spent = spent,
                Available = available,
                AnonymousReservedPlaces = poolAllocs.Count(a => !string.IsNullOrEmpty(a.PlaceholderName) && a.AllocationStatus == AllocationStatus.Reserved),
                AssignedPending = poolAllocs.Count(a => a.StudentId.HasValue && a.OutcomeStatus == OutcomeStatus.Pending),
                CompletedAwaitingManualSpend = poolAllocs.Count(a => a.OutcomeStatus == OutcomeStatus.Completed && a.CashCommitmentStatus == CashCommitmentStatus.Pending),
                Courses = new List<CourseCostPosition>()
            };

            var courseIds = poolAllocs
                .Select(a => a.CourseDelivery?.CourseDefinitionId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            foreach (var courseId in courseIds)
            {
                var course = courseDefinitions.FirstOrDefault(c => c.Id == courseId);
                if (course == null)
                    continue;

                var courseAllocs = poolAllocs.Where(a => a.CourseDelivery?.CourseDefinitionId == course.Id).ToList();
                var providerCost = prices.TryGetValue(course.Id, out var price) ? price : course.DefaultCertificateCost;

                var committedAmount = courseAllocs
                    .Where(a => a.CashCommitmentStatus == CashCommitmentStatus.Pending)
                    .Sum(a => a.CertificateCost ?? providerCost ?? 0m);
                var spentAmount = courseAllocs
                    .Where(a => a.CashCommitmentStatus == CashCommitmentStatus.Spent)
                    .Sum(a => a.CertificateCost ?? providerCost ?? 0m);

                var completionsRemaining = providerCost.HasValue && providerCost.Value > 0 && available > 0
                    ? (int)Math.Floor(available / providerCost.Value)
                    : 0;

                poolPosition.Courses.Add(new CourseCostPosition
                {
                    CourseId = course.Id,
                    CourseDisplayId = course.DisplayId,
                    CourseCode = course.CourseCode,
                    CourseTitle = course.CourseTitle,
                    Provider = course.Provider,
                    MatchKey = course.MatchKey,
                    Funds = funds,
                    Committed = committedAmount,
                    Spent = spentAmount,
                    Available = available,
                    AnonymousReservedPlaces = courseAllocs.Count(a => !string.IsNullOrEmpty(a.PlaceholderName) && a.AllocationStatus == AllocationStatus.Reserved),
                    AssignedPending = courseAllocs.Count(a => a.StudentId.HasValue && a.OutcomeStatus == OutcomeStatus.Pending),
                    CompletedAwaitingManualSpend = courseAllocs.Count(a => a.OutcomeStatus == OutcomeStatus.Completed && a.CashCommitmentStatus == CashCommitmentStatus.Pending),
                    CompletionsRemaining = completionsRemaining,
                    ProviderCost = providerCost,
                    TotalAllocations = courseAllocs.Count
                });
            }

            poolPosition.CompletionsRemaining = poolPosition.Courses.Sum(c => c.CompletionsRemaining);
            snapshot.Pools.Add(poolPosition);
        }

        return snapshot;
    }

    private static List<InvoicerCostPositionCsvRow> Flatten(InvoicerCostPositionSnapshot snapshot)
    {
        var rows = new List<InvoicerCostPositionCsvRow>();

        foreach (var pool in snapshot.Pools)
        {
            if (pool.Courses.Count == 0)
            {
                rows.Add(new InvoicerCostPositionCsvRow
                {
                    SnapshotId = snapshot.SnapshotId,
                    GeneratedAt = snapshot.GeneratedAt,
                    SchemaVersion = snapshot.SchemaVersion,
                    PoolId = pool.PoolId,
                    PoolDisplayId = pool.PoolDisplayId,
                    PoolName = pool.PoolName,
                    PoolFunds = pool.FundsAdded,
                    PoolCommitted = pool.Committed,
                    PoolSpent = pool.Spent,
                    PoolAvailable = pool.Available,
                    PoolAnonymousReservedPlaces = pool.AnonymousReservedPlaces,
                    PoolAssignedPending = pool.AssignedPending,
                    PoolCompletedAwaitingManualSpend = pool.CompletedAwaitingManualSpend,
                    PoolCompletionsRemaining = pool.CompletionsRemaining
                });
            }
            else
            {
                foreach (var course in pool.Courses)
                {
                    rows.Add(new InvoicerCostPositionCsvRow
                    {
                        SnapshotId = snapshot.SnapshotId,
                        GeneratedAt = snapshot.GeneratedAt,
                        SchemaVersion = snapshot.SchemaVersion,
                        PoolId = pool.PoolId,
                        PoolDisplayId = pool.PoolDisplayId,
                        PoolName = pool.PoolName,
                        PoolFunds = pool.FundsAdded,
                        PoolCommitted = pool.Committed,
                        PoolSpent = pool.Spent,
                        PoolAvailable = pool.Available,
                        PoolAnonymousReservedPlaces = pool.AnonymousReservedPlaces,
                        PoolAssignedPending = pool.AssignedPending,
                        PoolCompletedAwaitingManualSpend = pool.CompletedAwaitingManualSpend,
                        PoolCompletionsRemaining = pool.CompletionsRemaining,
                        CourseId = course.CourseId,
                        CourseDisplayId = course.CourseDisplayId,
                        CourseCode = course.CourseCode,
                        CourseTitle = course.CourseTitle,
                        Provider = course.Provider,
                        MatchKey = course.MatchKey,
                        CourseFunds = course.Funds,
                        CourseCommitted = course.Committed,
                        CourseSpent = course.Spent,
                        CourseAvailable = course.Available,
                        CourseAnonymousReservedPlaces = course.AnonymousReservedPlaces,
                        CourseAssignedPending = course.AssignedPending,
                        CourseCompletedAwaitingManualSpend = course.CompletedAwaitingManualSpend,
                        CourseCompletionsRemaining = course.CompletionsRemaining,
                        ProviderCost = course.ProviderCost,
                        TotalAllocations = course.TotalAllocations
                    });
                }
            }
        }

        return rows;
    }
}

public class InvoicerCostPositionSnapshot
{
    public string SchemaVersion { get; set; } = InvoicerReferenceExportService.SchemaVersion;
    public Guid SnapshotId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string SourceApplication { get; set; } = "StudentTracker";
    public string? Notes { get; set; }
    public List<PoolCostPosition> Pools { get; set; } = new();
}

public class PoolCostPosition
{
    public Guid PoolId { get; set; }
    public string? PoolDisplayId { get; set; }
    public string PoolName { get; set; } = string.Empty;
    public string? FinancialPeriod { get; set; }
    public bool IsActive { get; set; }
    public decimal FundsAdded { get; set; }
    public decimal Committed { get; set; }
    public decimal Spent { get; set; }
    public decimal Available { get; set; }
    public int AnonymousReservedPlaces { get; set; }
    public int AssignedPending { get; set; }
    public int CompletedAwaitingManualSpend { get; set; }
    public int CompletionsRemaining { get; set; }
    public List<CourseCostPosition> Courses { get; set; } = new();
}

public class CourseCostPosition
{
    public Guid CourseId { get; set; }
    public string? CourseDisplayId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? MatchKey { get; set; }
    public decimal Funds { get; set; }
    public decimal Committed { get; set; }
    public decimal Spent { get; set; }
    public decimal Available { get; set; }
    public int AnonymousReservedPlaces { get; set; }
    public int AssignedPending { get; set; }
    public int CompletedAwaitingManualSpend { get; set; }
    public int CompletionsRemaining { get; set; }
    public decimal? ProviderCost { get; set; }
    public int TotalAllocations { get; set; }
}

public class InvoicerCostPositionCsvRow
{
    public Guid SnapshotId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public Guid PoolId { get; set; }
    public string? PoolDisplayId { get; set; }
    public string PoolName { get; set; } = string.Empty;
    public decimal PoolFunds { get; set; }
    public decimal PoolCommitted { get; set; }
    public decimal PoolSpent { get; set; }
    public decimal PoolAvailable { get; set; }
    public int PoolAnonymousReservedPlaces { get; set; }
    public int PoolAssignedPending { get; set; }
    public int PoolCompletedAwaitingManualSpend { get; set; }
    public int PoolCompletionsRemaining { get; set; }
    public Guid? CourseId { get; set; }
    public string? CourseDisplayId { get; set; }
    public string? CourseCode { get; set; }
    public string? CourseTitle { get; set; }
    public string? Provider { get; set; }
    public string? MatchKey { get; set; }
    public decimal CourseFunds { get; set; }
    public decimal CourseCommitted { get; set; }
    public decimal CourseSpent { get; set; }
    public decimal CourseAvailable { get; set; }
    public int CourseAnonymousReservedPlaces { get; set; }
    public int CourseAssignedPending { get; set; }
    public int CourseCompletedAwaitingManualSpend { get; set; }
    public int CourseCompletionsRemaining { get; set; }
    public decimal? ProviderCost { get; set; }
    public int TotalAllocations { get; set; }
}

public class InvoicerCostPositionExportResult
{
    public Guid SnapshotId { get; set; }
    public string JsonPath { get; set; } = string.Empty;
    public string CsvPath { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public int PoolCount { get; set; }
    public int CourseCount { get; set; }
}
