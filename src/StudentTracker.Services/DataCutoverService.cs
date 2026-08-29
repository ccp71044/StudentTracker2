using System.IO.Compression;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public sealed record CutoverCounts(int Students, int Courses, int Deliveries, int Allocations, int BudgetPools, int CreditPools)
{
    public int Total => Students + Courses + Deliveries + Allocations + BudgetPools + CreditPools;
}

public sealed class CutoverPreview
{
    public required string WorkbookPath { get; init; }
    public required CutoverCounts DatabaseCounts { get; init; }
    public required CutoverCounts WorkbookCounts { get; init; }
    public List<string> Errors { get; init; } = [];
    public bool IsValid => Errors.Count == 0;
}

public sealed class CutoverResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? PreCutoverBackup { get; init; }
    public string? PostCutoverBackup { get; init; }
    public CutoverCounts? ImportedCounts { get; init; }
}

/// <summary>Validates and atomically replaces database data from a canonical migration workbook.</summary>
public sealed class DataCutoverService
{
    public const string ConfirmationPhrase = "REPLACE DATA";
    private readonly StudentTrackerDbContext _context;
    private readonly BackupService _backups;
    private readonly DisplayIdGenerator _ids;
    private readonly AuditService _audit;

    public DataCutoverService(StudentTrackerDbContext context, BackupService backups, DisplayIdGenerator ids, AuditService audit)
    {
        _context = context;
        _backups = backups;
        _ids = ids;
        _audit = audit;
    }

    public async Task<CutoverPreview> PreviewAsync(string path)
    {
        var preview = ValidateWorkbook(path);
        return new CutoverPreview
        {
            WorkbookPath = path,
            WorkbookCounts = preview.WorkbookCounts,
            DatabaseCounts = await GetCountsAsync(),
            Errors = preview.Errors
        };
    }

    public async Task<CutoverResult> ExecuteAsync(CutoverPreview preview, string typedConfirmation)
    {
        if (!preview.IsValid)
            return new CutoverResult { Success = false, Message = "Cutover refused: workbook validation failed." };
        if (!string.Equals(typedConfirmation, ConfirmationPhrase, StringComparison.Ordinal))
            return new CutoverResult { Success = false, Message = $"Cutover refused: type {ConfirmationPhrase} exactly." };

        // Revalidate immediately before any destructive action so a changed workbook cannot bypass preview.
        var fresh = await PreviewAsync(preview.WorkbookPath);
        if (!fresh.IsValid || fresh.WorkbookCounts != preview.WorkbookCounts)
            return new CutoverResult { Success = false, Message = "Cutover refused: the workbook changed or no longer validates." };

        var integrity = await _context.Database.SqlQueryRaw<string>("PRAGMA integrity_check;").ToListAsync();
        if (integrity.Count != 1 || !string.Equals(integrity[0], "ok", StringComparison.OrdinalIgnoreCase))
            return new CutoverResult { Success = false, Message = "Cutover refused: database integrity check failed." };

        var preBackup = _backups.CreateBackup("verified-pre-cutover");
        VerifyBackup(preBackup);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.ChangeTracker.Clear();
            await ClearDataAsync();
            _context.ChangeTracker.Clear();

            var importer = new MigrationPackageImporter(_context, _ids, _audit);
            var imported = importer.ImportWorkbook(preview.WorkbookPath);
            if (!imported.Success || imported.Errors.Count != 0 || importer.ReviewQueue.Count != 0)
                throw new InvalidOperationException("Import produced errors or manual-review items: " + string.Join("; ", imported.Errors));

            _context.ChangeTracker.Clear();
            var actual = await GetCountsAsync();
            if (actual != fresh.WorkbookCounts)
                throw new InvalidOperationException($"Reconciliation failed. Expected {fresh.WorkbookCounts}; found {actual}.");

            var brokenDeliveries = await _context.CourseDeliveries.CountAsync(d => !_context.CourseDefinitions.Any(c => c.Id == d.CourseDefinitionId));
            var brokenAllocations = await _context.Allocations.CountAsync(a =>
                !_context.CourseDeliveries.Any(d => d.Id == a.CourseDeliveryId) ||
                (a.StudentId != null && !_context.Students.Any(s => s.Id == a.StudentId)));
            if (brokenDeliveries != 0 || brokenAllocations != 0)
                throw new InvalidOperationException("Relationship reconciliation failed.");

            _audit.Record("DataCutoverCompleted", "System", Guid.Empty, null, fresh.DatabaseCounts, actual,
                $"Canonical package: {Path.GetFileName(preview.WorkbookPath)}; pre-cutover backup: {preBackup}");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _context.ChangeTracker.Clear();

            var postBackup = _backups.CreateBackup("verified-post-cutover");
            VerifyBackup(postBackup);
            return new CutoverResult { Success = true, Message = "Data replacement completed and reconciled.", PreCutoverBackup = preBackup, PostCutoverBackup = postBackup, ImportedCounts = actual };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            return new CutoverResult { Success = false, Message = "No database changes were committed. " + ex.Message, PreCutoverBackup = preBackup };
        }
    }

    private async Task ClearDataAsync()
    {
        // Dependants first. Document database records are removed, but this service never accesses or deletes document files.
        await _context.ExportBatchItems.ExecuteDeleteAsync();
        await _context.DocumentLinks.ExecuteDeleteAsync();
        await _context.SignOffParticipants.ExecuteDeleteAsync();
        await _context.CertificateDeliveries.ExecuteDeleteAsync();
        await _context.CertificateOrders.ExecuteDeleteAsync();
        await _context.SignOffs.ExecuteDeleteAsync();
        await _context.Invoices.ExecuteDeleteAsync();
        await _context.BudgetTransactions.ExecuteDeleteAsync();
        await _context.CertificateCreditTransactions.ExecuteDeleteAsync();
        await _context.Allocations.ExecuteDeleteAsync();
        await _context.CoursePrices.ExecuteDeleteAsync();
        await _context.CourseDeliveries.ExecuteDeleteAsync();
        await _context.Documents.ExecuteDeleteAsync();
        await _context.ExportBatches.ExecuteDeleteAsync();
        await _context.ImportReviewQueues.ExecuteDeleteAsync();
        await _context.FundingSources.ExecuteDeleteAsync();
        await _context.OutcomeReasons.ExecuteDeleteAsync();
        await _context.BudgetPools.ExecuteDeleteAsync();
        await _context.CertificateCreditPools.ExecuteDeleteAsync();
        await _context.CourseDefinitions.ExecuteDeleteAsync();
        await _context.Students.ExecuteDeleteAsync();
        await _context.AuditLogs.ExecuteDeleteAsync();
        // AppSettings and __EFMigrationsHistory are deliberately not touched.
    }

    private async Task<CutoverCounts> GetCountsAsync() => new(
        await _context.Students.CountAsync(), await _context.CourseDefinitions.CountAsync(),
        await _context.CourseDeliveries.CountAsync(), await _context.Allocations.CountAsync(),
        await _context.BudgetPools.CountAsync(), await _context.CertificateCreditPools.CountAsync());

    private static void VerifyBackup(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException("Backup file was not created.");
        using var archive = ZipFile.OpenRead(path);
        var db = archive.GetEntry("Database/student-tracker.db");
        if (db == null || db.Length == 0) throw new InvalidOperationException("Backup verification failed: database snapshot is missing or empty.");
    }

    public static CutoverPreview ValidateWorkbook(string path)
    {
        var errors = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return Invalid(path, "Workbook does not exist.");
        try
        {
            using var workbook = new XLWorkbook(path);
            var specs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Students"] = ["DisplayId", "FirstName", "LastName"],
                ["CourseDefinitions"] = ["CourseCode", "CourseTitle"],
                ["CourseDeliveries"] = ["CourseCode", "DisplayId"],
                ["Allocations"] = ["StudentDisplayId", "DeliveryDisplayId"]
            };
            foreach (var spec in specs)
                ValidateSheet(workbook, spec.Key, spec.Value, errors, counts);
            ValidateOptionalSheet(workbook, "BudgetPools", "Name", errors, counts);
            ValidateOptionalSheet(workbook, "CertificateCreditPools", "Name", errors, counts);

            var students = Values(workbook, "Students", "DisplayId");
            var courses = Values(workbook, "CourseDefinitions", "CourseCode");
            var deliveries = Values(workbook, "CourseDeliveries", "DisplayId");
            DuplicateCheck(students, "student DisplayId", errors);
            DuplicateCheck(courses, "course CourseCode", errors);
            DuplicateCheck(deliveries, "delivery DisplayId", errors);
            DuplicateCheck(Values(workbook, "BudgetPools", "Name"), "budget pool Name", errors);
            DuplicateCheck(Values(workbook, "CertificateCreditPools", "Name"), "credit pool Name", errors);

            CheckReferences(Values(workbook, "CourseDeliveries", "CourseCode"), courses, "delivery course", errors);
            CheckReferences(Values(workbook, "Allocations", "StudentDisplayId"), students, "allocation student", errors);
            CheckReferences(Values(workbook, "Allocations", "DeliveryDisplayId"), deliveries, "allocation delivery", errors);
            CheckReferences(Values(workbook, "Allocations", "BudgetPoolName"), Values(workbook, "BudgetPools", "Name"), "allocation budget pool", errors);
            CheckReferences(Values(workbook, "Allocations", "CreditPoolName"), Values(workbook, "CertificateCreditPools", "Name"), "allocation credit pool", errors);

            EnumCheck<DeliveryDateStatus>(workbook, "CourseDeliveries", "DateStatus", errors, "Blank");
            AllowedCheck(workbook, "CourseDeliveries", "DeliveryStatus", ["Scheduled", "Cancelled", "Completed"], errors);
            EnumCheck<AllocationStatus>(workbook, "Allocations", "AllocationStatus", errors);
            EnumCheck<AttendanceStatus>(workbook, "Allocations", "AttendanceStatus", errors);
            EnumCheck<OutcomeStatus>(workbook, "Allocations", "OutcomeStatus", errors);
            EnumCheck<CreditStatus>(workbook, "Allocations", "CreditStatus", errors);
            EnumCheck<CashCommitmentStatus>(workbook, "Allocations", "CashCommitmentStatus", errors);
            EnumCheck<CertificateOrderStatus>(workbook, "Allocations", "CertificateOrderStatus", errors);
            EnumCheck<CertificateDeliveryStatus>(workbook, "Allocations", "CertificateDeliveryStatus", errors);
            EnumCheck<CreditUnitType>(workbook, "CertificateCreditPools", "UnitType", errors);
        }
        catch (Exception ex) { errors.Add("Workbook cannot be read: " + ex.Message); }

        return new CutoverPreview { WorkbookPath = path, DatabaseCounts = new(0, 0, 0, 0, 0, 0), WorkbookCounts = Counts(counts), Errors = errors };
    }

    private static CutoverPreview Invalid(string path, string error) => new() { WorkbookPath = path, DatabaseCounts = new(0, 0, 0, 0, 0, 0), WorkbookCounts = new(0, 0, 0, 0, 0, 0), Errors = { error } };
    private static CutoverCounts Counts(Dictionary<string, int> c) => new(c.GetValueOrDefault("Students"), c.GetValueOrDefault("CourseDefinitions"), c.GetValueOrDefault("CourseDeliveries"), c.GetValueOrDefault("Allocations"), c.GetValueOrDefault("BudgetPools"), c.GetValueOrDefault("CertificateCreditPools"));

    private static void ValidateSheet(XLWorkbook wb, string name, string[] required, List<string> errors, Dictionary<string, int> counts)
    {
        var ws = wb.Worksheets.FirstOrDefault(x => x.Name == name);
        if (ws == null) { errors.Add($"Required sheet '{name}' is missing."); counts[name] = 0; return; }
        var headers = ws.Row(1).CellsUsed().Select(c => c.GetString().Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var header in required) if (!headers.Contains(header)) errors.Add($"Sheet '{name}' is missing header '{header}'.");
        counts[name] = Math.Max(0, ws.RowsUsed().Count() - 1);
        foreach (var header in required.Where(h => h is not "DisplayId"))
        {
            var column = HeaderColumn(ws, header);
            if (column == 0) continue;
            foreach (var row in ws.RowsUsed().Skip(1)) if (string.IsNullOrWhiteSpace(row.Cell(column).GetString())) errors.Add($"Sheet '{name}' row {row.RowNumber()} requires '{header}'.");
        }
    }

    private static void ValidateOptionalSheet(XLWorkbook wb, string name, string required, List<string> errors, Dictionary<string, int> counts)
    {
        var ws = wb.Worksheets.FirstOrDefault(x => x.Name == name);
        if (ws == null) { counts[name] = 0; return; }
        ValidateSheet(wb, name, [required], errors, counts);
    }

    private static List<string> Values(XLWorkbook wb, string sheet, string header)
    {
        var ws = wb.Worksheets.FirstOrDefault(x => x.Name == sheet);
        if (ws == null) return [];
        var col = HeaderColumn(ws, header);
        return col == 0 ? [] : ws.RowsUsed().Skip(1).Select(r => r.Cell(col).GetString().Trim()).Where(v => v.Length > 0).ToList();
    }

    private static int HeaderColumn(IXLWorksheet ws, string header) => ws.Row(1).CellsUsed().FirstOrDefault(c => string.Equals(c.GetString().Trim(), header, StringComparison.OrdinalIgnoreCase))?.Address.ColumnNumber ?? 0;
    private static void DuplicateCheck(List<string> values, string label, List<string> errors)
    {
        foreach (var value in values.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key)) errors.Add($"Duplicate {label}: '{value}'.");
    }
    private static void CheckReferences(List<string> refs, List<string> targets, string label, List<string> errors)
    {
        var set = targets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var value in refs.Distinct(StringComparer.OrdinalIgnoreCase).Where(v => !set.Contains(v))) errors.Add($"Broken {label} reference: '{value}'.");
    }
    private static void EnumCheck<T>(XLWorkbook wb, string sheet, string header, List<string> errors, params string[] aliases) where T : struct, Enum
    {
        var allowed = Enum.GetNames<T>().Concat(aliases).ToHashSet(StringComparer.OrdinalIgnoreCase);
        AllowedCheck(wb, sheet, header, allowed, errors);
    }
    private static void AllowedCheck(XLWorkbook wb, string sheet, string header, IEnumerable<string> allowed, List<string> errors)
    {
        var valid = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var value in Values(wb, sheet, header).Distinct(StringComparer.OrdinalIgnoreCase).Where(v => !valid.Contains(v))) errors.Add($"Invalid enum value in {sheet}.{header}: '{value}'.");
    }
}
