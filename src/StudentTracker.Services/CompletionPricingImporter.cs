using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using StudentTracker.Core.Common;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

/// <summary>
/// Imports the provider's completion price list ("Course Type,completion_price (AU$)") so every
/// course carries a current price and remaining completions can be calculated.
/// </summary>
public class CompletionPricingImporter
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;
    private readonly List<ImportReviewQueue> _reviewQueue = new();
    private readonly Dictionary<CourseDefinition, decimal> _pricesInThisRun = new();

    public CompletionPricingImporter(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public IReadOnlyList<ImportReviewQueue> ReviewQueue => _reviewQueue;

    public ImportResult Import(string csvPath, DateTime? effectiveFrom = null, bool createMissingCourses = true)
    {
        using var reader = new StreamReader(csvPath);
        return Import(reader, Path.GetFileName(csvPath), effectiveFrom, createMissingCourses);
    }

    public ImportResult Import(TextReader reader, string sourceFileName, DateTime? effectiveFrom = null, bool createMissingCourses = true)
    {
        var effective = effectiveFrom ?? DateTime.UtcNow.Date;
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();

        var courseColumn = FindColumn(csv, "Course Type", "course_type", "Course");
        var priceColumn = FindColumn(csv, "completion_price (AU$)", "completion_price", "Price");
        if (courseColumn == null || priceColumn == null)
            return new ImportResult { Success = false, Message = "Expected a 'Course Type' column and a 'completion_price' column." };

        var updated = 0;
        var created = 0;
        var unchanged = 0;
        var rowNumber = 1;

        while (csv.Read())
        {
            rowNumber++;
            var description = csv.GetField(courseColumn)?.Trim();
            var priceText = csv.GetField(priceColumn)?.Trim();

            if (string.IsNullOrWhiteSpace(description))
                continue;

            if (!decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                Queue(sourceFileName, rowNumber, $"Could not read a price for '{description}' (value: '{priceText}').");
                continue;
            }

            var key = CourseKey.Build(description);
            var course = FindCourse(key);

            if (course == null)
            {
                if (!createMissingCourses)
                {
                    Queue(sourceFileName, rowNumber, $"No course matches '{description}'.");
                    continue;
                }

                var (code, title) = CourseKey.Split(description);
                course = new CourseDefinition
                {
                    CourseCode = code,
                    CourseTitle = title,
                    MatchKey = key,
                    Provider = "Allens Training",
                    DefaultCreditQuantity = 1m
                };
                _context.CourseDefinitions.Add(course);
                created++;
            }
            else if (string.IsNullOrEmpty(course.MatchKey))
            {
                course.MatchKey = key;
            }

            if (ApplyPrice(course, price, effective, sourceFileName))
                updated++;
            else
                unchanged++;
        }

        _context.SaveChanges();
        _audit.Record("CompletionPricingImported", "Import", Guid.NewGuid());
        _context.SaveChanges();

        return new ImportResult
        {
            Success = true,
            RowsProcessed = created + updated + unchanged,
            Message = $"Price list imported. {created} new courses, {updated} prices updated, {unchanged} already current. Review queue items: {_reviewQueue.Count}.",
            Errors = _reviewQueue.Select(r => r.Issue ?? string.Empty).ToList()
        };
    }

    /// <summary>Adds a price only when it differs from the price already in force, keeping history meaningful.</summary>
    private bool ApplyPrice(CourseDefinition course, decimal price, DateTime effectiveFrom, string sourceFileName)
    {
        if (_pricesInThisRun.TryGetValue(course, out var pending))
        {
            if (pending == price) return false;
        }
        else if (CurrentPrice(course, effectiveFrom) == price)
        {
            return false;
        }

        _pricesInThisRun[course] = price;

        _context.CoursePrices.Add(new CoursePrice
        {
            CourseDefinition = course,
            CompletionPrice = price,
            EffectiveFrom = effectiveFrom,
            SourceType = PriceSourceType.ProviderPriceList,
            SourceReference = sourceFileName
        });

        course.DefaultCertificateCost = price;
        course.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private decimal? CurrentPrice(CourseDefinition course, DateTime asAt)
    {
        if (course.Id == Guid.Empty)
            return null;

        return _context.CoursePrices
            .Where(p => p.CourseDefinitionId == course.Id && p.EffectiveFrom <= asAt)
            .OrderByDescending(p => p.EffectiveFrom)
            .Select(p => (decimal?)p.CompletionPrice)
            .FirstOrDefault();
    }

    private CourseDefinition? FindCourse(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        return _context.CourseDefinitions.Local.FirstOrDefault(c => c.MatchKey == key)
            ?? _context.CourseDefinitions.FirstOrDefault(c => c.MatchKey == key)
            ?? _context.CourseDefinitions.Local.FirstOrDefault(c => c.CourseCode == key)
            ?? _context.CourseDefinitions.FirstOrDefault(c => c.CourseCode == key);
    }

    private static string? FindColumn(CsvReader csv, params string[] candidates)
    {
        var header = csv.HeaderRecord;
        if (header == null) return null;

        foreach (var candidate in candidates)
        {
            var match = header.FirstOrDefault(h => string.Equals(h?.Trim(), candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        return header.FirstOrDefault(h => h != null && h.Contains(candidates[0], StringComparison.OrdinalIgnoreCase));
    }

    private void Queue(string sourceFileName, int rowNumber, string issue)
    {
        _reviewQueue.Add(new ImportReviewQueue
        {
            DisplayId = _idGenerator.NextDisplayId<ImportReviewQueue>("REV"),
            SourceFileName = sourceFileName,
            SourceRow = rowNumber,
            EntityType = "CoursePrice",
            Issue = issue,
            Status = "Pending"
        });
    }
}
