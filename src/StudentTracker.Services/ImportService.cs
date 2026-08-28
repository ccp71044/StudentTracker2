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

        try
        {
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
        catch (Exception ex)
        {
            // A malformed export must not take the application down: log it and report the reason.
            OperationLog.Failure("ImportCsv", ex, new { EntityType = entityType });
            return Task.FromResult(Failed($"The {entityType} CSV could not be imported: {ex.Message}"));
        }
    }

    public Task<ImportResult> ImportMigrationPackageAsync(string xlsxPath)
    {
        IReadOnlyList<ImportReviewQueue> reviewQueue;
        ImportResult result;

        try
        {
            var isLegacy = IsLegacyStudentRegisterFormat(xlsxPath);
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
        }
        catch (Exception ex)
        {
            OperationLog.Failure("ImportMigrationPackage", ex, new { Path = xlsxPath });
            return Task.FromResult(Failed($"The workbook could not be imported: {ex.Message}"));
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

    private static ImportResult Failed(string message) => new() { Success = false, Message = message, Errors = { message } };

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
