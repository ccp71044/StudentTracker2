using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class ImportService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;

    public ImportService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    /// <summary>
    /// Supported entity types are "CompletionPricing" (the provider's course price list) and
    /// "CreditHistory" (the provider's credit transaction export).
    /// </summary>
    public Task<ImportResult> ImportCsvAsync(string entityType, Stream csvStream)
    {
        _audit.Record("ImportStarted", "Import", Guid.NewGuid());
        _context.SaveChanges();

        using var reader = new StreamReader(csvStream);
        IReadOnlyList<ImportReviewQueue> reviewQueue;
        ImportResult result;

        switch (entityType)
        {
            case "CompletionPricing":
            {
                var importer = new CompletionPricingImporter(_context, _idGenerator, _audit);
                result = importer.Import(reader, entityType);
                reviewQueue = importer.ReviewQueue;
                break;
            }
            case "CreditHistory":
            {
                var importer = new ProviderCreditHistoryImporter(_context, _idGenerator, _audit);
                result = importer.Import(reader, entityType);
                reviewQueue = importer.ReviewQueue;
                break;
            }
            default:
                return Task.FromResult(new ImportResult
                {
                    Success = false,
                    Message = $"Unsupported CSV type '{entityType}'. Expected 'CompletionPricing' or 'CreditHistory'."
                });
        }

        if (reviewQueue.Any())
        {
            _context.ImportReviewQueues.AddRange(reviewQueue);
            _context.SaveChanges();
        }

        return Task.FromResult(result);
    }

    public Task<ImportResult> ImportMigrationPackageAsync(string xlsxPath)
    {
        var isLegacy = IsLegacyStudentRegisterFormat(xlsxPath);
        IReadOnlyList<ImportReviewQueue> reviewQueue;
        ImportResult result;

        if (isLegacy)
        {
            var importer = new LegacyStudentRegisterImporter(_context, _idGenerator, _audit);
            result = importer.Import(xlsxPath);
            reviewQueue = importer.ReviewQueue;
        }
        else
        {
            var importer = new MigrationPackageImporter(_context, _idGenerator, _audit);
            result = importer.ImportWorkbook(xlsxPath);
            reviewQueue = importer.ReviewQueue;
        }

        if (reviewQueue.Any())
        {
            _context.ImportReviewQueues.AddRange(reviewQueue);
            _context.SaveChanges();
        }

        _audit.Record("MigrationImportCompleted", "Import", Guid.NewGuid());
        _context.SaveChanges();
        return Task.FromResult(result);
    }

    private static bool IsLegacyStudentRegisterFormat(string xlsxPath)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook(xlsxPath);
        var firstSheet = workbook.Worksheets.FirstOrDefault();
        if (firstSheet == null) return false;

        // Legacy format is a single worksheet that contains the student-register column headers.
        if (workbook.Worksheets.Count > 1) return false;

        var usedText = firstSheet.CellsUsed()
            .Select(c => c.GetString().Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return usedText.Contains("First Name") && usedText.Contains("Last Name") && usedText.Contains("Course");
    }
}

public class ImportResult
{
    public bool Success { get; set; }
    public int RowsProcessed { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
}
