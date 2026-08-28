using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

/// <summary>
/// Imports the training provider's credit transaction history. Credit rows are purchases that top
/// up the account; debit rows are completions consumed, e.g.
/// "3 x HLTAID011 - Provide First Aid" against course #1761277.
/// The provider's row id is the natural key, so re-importing a longer export only adds new rows.
/// </summary>
public partial class ProviderCreditHistoryImporter
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;
    private readonly List<ImportReviewQueue> _reviewQueue = new();

    public ProviderCreditHistoryImporter(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public IReadOnlyList<ImportReviewQueue> ReviewQueue => _reviewQueue;

    /// <summary>Matches "3 x HLTAID011 - Provide First Aid" and "1 x Course Set - HLTAID011 &amp; HLTAID015".</summary>
    [GeneratedRegex(@"^\s*(?<qty>\d+)\s*x\s*(?<course>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ConsumptionRegex();

    [GeneratedRegex(@"#\s*(?<number>\d+)")]
    private static partial Regex CourseNumberRegex();

    public ImportResult Import(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        return Import(reader, Path.GetFileName(csvPath));
    }

    public ImportResult Import(TextReader reader, string sourceFileName)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim
        };

        var pool = EnsureProviderPool();
        var existingIds = _context.CertificateCreditTransactions
            .Where(t => t.ExternalTransactionId != null)
            .Select(t => t.ExternalTransactionId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();

        // Without these columns every row would import as an unusable blank rather than failing.
        var header = csv.HeaderRecord ?? Array.Empty<string>();
        var missing = new[] { "id", "credit", "debit" }
            .Where(c => !header.Any(h => string.Equals(h?.Trim(), c, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missing.Count > 0)
        {
            var message = $"This does not look like a provider credit export: missing column(s) {string.Join(", ", missing)}.";
            OperationLog.Failure("ImportProviderCreditHistory",
                new InvalidDataException(message), new { Source = sourceFileName });
            return new ImportResult { Success = false, Message = message, Errors = { message } };
        }

        var topUps = 0;
        var consumptions = 0;
        var skipped = 0;
        var rowNumber = 1;

        while (csv.Read())
        {
            rowNumber++;
            var externalId = csv.GetField("id")?.Trim();
            if (string.IsNullOrWhiteSpace(externalId))
            {
                Queue(sourceFileName, rowNumber, "Row has no provider transaction id and cannot be imported safely.");
                continue;
            }

            if (!existingIds.Add(externalId))
            {
                skipped++;
                continue;
            }

            var date = ParseDate(csv.GetField("date_and_time"));
            var credit = ParseAmount(csv.GetField("credit"));
            var debit = ParseAmount(csv.GetField("debit"));
            var descriptor = csv.GetField("descriptor")?.Trim();
            var details = csv.GetField("extra_details")?.Trim();

            if (credit.HasValue && credit.Value > 0)
            {
                _context.CertificateCreditTransactions.Add(new CertificateCreditTransaction
                {
                    DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
                    PoolId = pool.Id,
                    Pool = pool,
                    TransactionType = CreditTransactionType.TopUp,
                    Amount = credit.Value,
                    TransactionDateTime = date ?? DateTime.UtcNow,
                    SourceType = CreditSourceType.ProviderHistory,
                    ExternalTransactionId = externalId,
                    ExternalPurchaseReference = details,
                    Reason = descriptor ?? "Credit purchase"
                });
                topUps++;
                continue;
            }

            if (debit.HasValue && debit.Value > 0)
            {
                var (quantity, courseText) = ParseConsumption(details);
                var course = courseText == null ? null : FindCourse(CourseKey.Build(courseText));

                if (course == null && courseText != null)
                    Queue(sourceFileName, rowNumber, $"No course matches '{courseText}'. Imported as an unmatched consumption.");

                _context.CertificateCreditTransactions.Add(new CertificateCreditTransaction
                {
                    DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
                    PoolId = pool.Id,
                    Pool = pool,
                    TransactionType = CreditTransactionType.ManualConsume,
                    Amount = debit.Value,
                    Quantity = quantity,
                    TransactionDateTime = date ?? DateTime.UtcNow,
                    SourceType = CreditSourceType.ProviderHistory,
                    ExternalTransactionId = externalId,
                    ExternalCourseNumber = ExtractCourseNumber(descriptor),
                    Reason = details ?? descriptor ?? "Completion consumed",
                    Notes = course?.CourseCode
                });
                consumptions++;
                continue;
            }

            Queue(sourceFileName, rowNumber, $"Row {externalId} has neither a credit nor a debit amount.");
        }

        _context.SaveChanges();
        _audit.Record("ProviderCreditHistoryImported", "Import", Guid.NewGuid());
        _context.SaveChanges();

        return new ImportResult
        {
            Success = true,
            RowsProcessed = topUps + consumptions,
            Message = $"Provider credit history imported. {topUps} top-ups, {consumptions} consumptions, {skipped} already present. Review queue items: {_reviewQueue.Count}.",
            Errors = _reviewQueue.Select(r => r.Issue ?? string.Empty).ToList()
        };
    }

    private CertificateCreditPool EnsureProviderPool()
    {
        var existing = _context.CertificateCreditPools.Local.FirstOrDefault(p => p.Name == PoolNames.ProviderCredit)
            ?? _context.CertificateCreditPools.FirstOrDefault(p => p.Name == PoolNames.ProviderCredit);

        if (existing != null)
            return existing;

        var pool = new CertificateCreditPool
        {
            DisplayId = _idGenerator.NextDisplayId<CertificateCreditPool>("CRP"),
            Name = PoolNames.ProviderCredit,
            Provider = "Allens Training",
            UnitType = CreditUnitType.Monetary,
            Description = "Mirrors the credit account held with the training provider."
        };
        _context.CertificateCreditPools.Add(pool);
        _context.SaveChanges();
        return pool;
    }

    private CourseDefinition? FindCourse(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        return _context.CourseDefinitions.Local.FirstOrDefault(c => c.MatchKey == key)
            ?? _context.CourseDefinitions.FirstOrDefault(c => c.MatchKey == key)
            ?? _context.CourseDefinitions.Local.FirstOrDefault(c => c.CourseCode == key)
            ?? _context.CourseDefinitions.FirstOrDefault(c => c.CourseCode == key);
    }

    private static (decimal Quantity, string? CourseText) ParseConsumption(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return (1m, null);

        var match = ConsumptionRegex().Match(details);
        if (!match.Success)
            return (1m, details.Trim());

        var quantity = decimal.TryParse(match.Groups["qty"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var q) ? q : 1m;
        return (quantity, match.Groups["course"].Value.Trim());
    }

    private static string? ExtractCourseNumber(string? descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor)) return null;
        var match = CourseNumberRegex().Match(descriptor);
        return match.Success ? match.Groups["number"].Value : null;
    }

    /// <summary>Provider exports use Australian day-first dates such as "27/07/2026 01:12pm".</summary>
    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var formats = new[]
        {
            "dd/MM/yyyy hh:mmtt", "dd/MM/yyyy HH:mm", "dd/MM/yyyy hh:mm tt",
            "d/M/yyyy hh:mmtt", "d/M/yyyy HH:mm", "dd/MM/yyyy"
        };

        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        return DateTime.TryParse(value, new CultureInfo("en-AU"), DateTimeStyles.None, out var fallback) ? fallback : null;
    }

    private static decimal? ParseAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Replace("$", string.Empty).Replace(",", string.Empty).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    }

    private void Queue(string sourceFileName, int rowNumber, string issue)
    {
        _reviewQueue.Add(new ImportReviewQueue
        {
            DisplayId = _idGenerator.NextDisplayId<ImportReviewQueue>("REV"),
            SourceFileName = sourceFileName,
            SourceRow = rowNumber,
            EntityType = "CertificateCreditTransaction",
            Issue = issue,
            Status = "Pending"
        });
    }
}
