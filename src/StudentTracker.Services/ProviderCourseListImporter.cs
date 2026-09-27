using System.Globalization;
using ClosedXML.Excel;
using StudentTracker.Core.Common;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

/// <summary>
/// Imports the provider's course list export ("Course ID, Course Type, Course Start Date,
/// Process Status") as course deliveries, creating the course definitions they refer to.
/// </summary>
public class ProviderCourseListImporter
{
    private static readonly string[] StartDateFormats =
    {
        "dd/MM/yyyy hh:mmtt", "d/M/yyyy h:mmtt", "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "dd/MM/yyyy", "d/M/yyyy"
    };

    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;
    private readonly List<ImportReviewQueue> _reviewQueue = new();

    public ProviderCourseListImporter(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public IReadOnlyList<ImportReviewQueue> ReviewQueue => _reviewQueue;

    public int Created { get; private set; }
    public int Updated { get; private set; }
    public int CoursesCreated { get; private set; }

    public ImportResult Import(string xlsxPath)
    {
        using var workbook = new XLWorkbook(xlsxPath);
        return Import(workbook, Path.GetFileName(xlsxPath));
    }

    public ImportResult Import(XLWorkbook workbook, string sourceFileName)
    {
        var worksheet = workbook.Worksheets.First();
        var headerRow = ProviderSheet.FindHeaderRow(worksheet, "Course ID", "Course Type");
        if (headerRow < 0)
            return new ImportResult { Success = false, Message = "Expected 'Course ID' and 'Course Type' columns in the provider course list." };

        var columns = ProviderSheet.MapColumns(worksheet.Row(headerRow));
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;

        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var providerCourseId = ProviderSheet.Text(row, columns, "courseid");
            var description = ProviderSheet.Text(row, columns, "coursetype");
            var status = ProviderSheet.Text(row, columns, "processstatus");

            if (providerCourseId.Length == 0 && description.Length == 0)
                continue;

            if (providerCourseId.Length == 0)
            {
                Queue(sourceFileName, rowNumber, $"No provider course number for '{description}'.", "Skipped");
                continue;
            }

            if (description.Length == 0)
            {
                Queue(sourceFileName, rowNumber, $"Provider course {providerCourseId} has no course type.", "Skipped");
                continue;
            }

            var course = ResolveCourse(description, sourceFileName, rowNumber);
            var delivery = FindDelivery(providerCourseId);

            if (delivery == null)
            {
                delivery = new CourseDelivery
                {
                    DisplayId = _idGenerator.NextDeliveryId(),
                    ProviderCourseId = providerCourseId
                };
                _context.CourseDeliveries.Add(delivery);
                Created++;
            }
            else
            {
                Updated++;
                delivery.UpdatedAt = DateTime.UtcNow;
            }

            delivery.CourseDefinition = course;
            ApplyStartDate(delivery, ProviderSheet.Text(row, columns, "coursestartdate"), sourceFileName, rowNumber);
            if (status.Length > 0) delivery.DeliveryStatus = status;
        }

        _context.SaveChanges();
        _audit.Record("ProviderCourseListImported", "Import", Guid.NewGuid());
        _context.SaveChanges();

        return new ImportResult
        {
            Success = true,
            RowsProcessed = Created + Updated,
            Message = $"Provider course list imported. {Created} new deliveries, {Updated} matched to existing records, {CoursesCreated} new courses. Review queue items: {_reviewQueue.Count}.",
            Errors = _reviewQueue.Select(r => r.Issue ?? string.Empty).ToList()
        };
    }

    /// <summary>
    /// The export truncates long course names ("PUAFIR306 Identify, detect and monitor hazardou...").
    /// A truncated name is never written over a full one, and course sets - which match on their
    /// description rather than a unit code - go to review when truncated, because the part that was
    /// cut off is the part that distinguishes them.
    /// </summary>
    private CourseDefinition ResolveCourse(string description, string sourceFileName, int rowNumber)
    {
        var truncated = description.EndsWith("...", StringComparison.Ordinal);
        var key = CourseKey.Build(description);
        var (code, title) = CourseKey.Split(description);

        var existing = FindCourse(key);
        if (existing != null)
        {
            if (!truncated && existing.CourseTitle.EndsWith("...", StringComparison.Ordinal))
                existing.CourseTitle = title;
            return existing;
        }

        if (truncated && code == "Course Set")
        {
            Queue(sourceFileName, rowNumber,
                $"Course set '{description}' is truncated in the export, so it cannot be matched to an existing set with certainty.",
                "Created as a new course set");
        }

        var course = new CourseDefinition
        {
            CourseCode = code,
            CourseTitle = title,
            MatchKey = key,
            Provider = "Allens Training",
            DefaultCreditQuantity = 1m
        };
        _context.CourseDefinitions.Add(course);
        CoursesCreated++;
        return course;
    }

    private CourseDefinition? FindCourse(string key)
    {
        if (key.Length == 0) return null;

        return _context.CourseDefinitions.Local.FirstOrDefault(c => c.MatchKey == key)
            ?? _context.CourseDefinitions.FirstOrDefault(c => c.MatchKey == key)
            ?? _context.CourseDefinitions.Local.FirstOrDefault(c => c.CourseCode == key)
            ?? _context.CourseDefinitions.FirstOrDefault(c => c.CourseCode == key);
    }

    private CourseDelivery? FindDelivery(string providerCourseId) =>
        _context.CourseDeliveries.Local.FirstOrDefault(d => d.ProviderCourseId == providerCourseId)
        ?? _context.CourseDeliveries.FirstOrDefault(d => d.ProviderCourseId == providerCourseId);

    private void ApplyStartDate(CourseDelivery delivery, string text, string sourceFileName, int rowNumber)
    {
        if (text.Length == 0)
        {
            delivery.DateStatus = DeliveryDateStatus.Blank;
            return;
        }

        if (text.Equals("TBC", StringComparison.OrdinalIgnoreCase))
        {
            delivery.DateStatus = DeliveryDateStatus.TBC;
            return;
        }

        // The export writes the meridiem in lower case ("03:00pm"), which invariant parsing rejects.
        var normalised = text.ToUpperInvariant();
        if (DateTime.TryParseExact(normalised, StartDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            delivery.StartDate = parsed;
            delivery.DateStatus = DeliveryDateStatus.Confirmed;
            return;
        }

        delivery.DateStatus = DeliveryDateStatus.TBC;
        Queue(sourceFileName, rowNumber, $"Could not read the start date '{text}' for provider course {delivery.ProviderCourseId}.", "Date left as TBC");
    }

    private void Queue(string sourceFileName, int rowNumber, string issue, string proposedAction)
    {
        _reviewQueue.Add(new ImportReviewQueue
        {
            DisplayId = _idGenerator.NextDisplayId<ImportReviewQueue>("REV"),
            SourceFileName = sourceFileName,
            SourceSheet = "Course List",
            SourceRow = rowNumber,
            EntityType = "CourseDelivery",
            ProposedAction = proposedAction,
            Issue = issue,
            Status = "Pending"
        });
    }
}
