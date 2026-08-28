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
            switch (DetectFormat(xlsxPath))
            {
                case WorkbookFormat.LegacyStudentRegister:
                {
                    var importer = new LegacyStudentRegisterImporter(_context, _idGenerator, _audit);
                    result = importer.Import(xlsxPath);
                    reviewQueue = importer.ReviewQueue;
                    break;
                }
                case WorkbookFormat.ProviderStudentList:
                {
                    var importer = new ProviderStudentListImporter(_context, _idGenerator, _audit);
                    result = importer.Import(xlsxPath);
                    reviewQueue = importer.ReviewQueue;
                    break;
                }
                case WorkbookFormat.ProviderCourseList:
                {
                    var importer = new ProviderCourseListImporter(_context, _idGenerator, _audit);
                    result = importer.Import(xlsxPath);
                    reviewQueue = importer.ReviewQueue;
                    break;
                }
                default:
                {
                    var importer = new MigrationPackageImporter(_context, _idGenerator, _audit);
                    result = importer.ImportWorkbook(xlsxPath);
                    reviewQueue = importer.ReviewQueue;
                    break;
                }
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

    private enum WorkbookFormat
    {
        MigrationPackage,
        LegacyStudentRegister,
        ProviderStudentList,
        ProviderCourseList
    }

    /// <summary>
    /// Single-sheet workbooks are told apart by their headers, so the operator picks a file rather
    /// than a file and a format.
    /// </summary>
    private static WorkbookFormat DetectFormat(string xlsxPath)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook(xlsxPath);
        var firstSheet = workbook.Worksheets.FirstOrDefault();
        if (firstSheet == null || workbook.Worksheets.Count > 1)
            return WorkbookFormat.MigrationPackage;

        // The provider's headers must sit together on one row; matching them anywhere in the sheet
        // would let a stray cell in another export choose the wrong importer.
        if (ProviderSheet.FindHeaderRow(firstSheet, "Course ID", "Course Type") > 0)
            return WorkbookFormat.ProviderCourseList;

        if (ProviderSheet.FindHeaderRow(firstSheet, "ID", "First name", "Email") > 0)
            return WorkbookFormat.ProviderStudentList;

        return IsLegacyStudentRegisterFormat(firstSheet) ? WorkbookFormat.LegacyStudentRegister : WorkbookFormat.MigrationPackage;
    }

    private static bool IsLegacyStudentRegisterFormat(ClosedXML.Excel.IXLWorksheet firstSheet)
    {
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
