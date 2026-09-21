using System.Globalization;
using ClosedXML.Excel;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class MigrationPackageImporter
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;
    private readonly List<ImportReviewQueue> _reviewQueue = new();
    private Dictionary<string, int>? _headerMap;

    public MigrationPackageImporter(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public IReadOnlyList<ImportReviewQueue> ReviewQueue => _reviewQueue;

    public ImportResult ImportWorkbook(string xlsxPath)
    {
        _reviewQueue.Clear();
        using var workbook = new XLWorkbook(xlsxPath);
        var sheets = workbook.Worksheets.Select(ws => ws.Name).ToList();

        ImportStudents(workbook.Worksheets.FirstOrDefault(ws => NameMatches(ws.Name, "Students", "Student", "Learners")));
        ImportCourseDefinitions(workbook.Worksheets.FirstOrDefault(ws => NameMatches(ws.Name, "Courses", "Course Definitions", "CourseDefinitions")));
        ImportCourseDeliveries(workbook.Worksheets.FirstOrDefault(ws => NameMatches(ws.Name, "Deliveries", "Course Deliveries", "CourseDeliveries")));
        ImportCreditPools(workbook.Worksheets.FirstOrDefault(ws => NameMatches(ws.Name, "Credit Pools", "CreditPools", "Certificate Credits", "CertificateCreditPools")));
        ImportBudgetPools(workbook.Worksheets.FirstOrDefault(ws => NameMatches(ws.Name, "Budget Pools", "BudgetPools", "Budget")));
        ImportAllocations(workbook.Worksheets.FirstOrDefault(ws => NameMatches(ws.Name, "Allocations", "Enrolments", "Bookings")));

        var rowsProcessed = _context.ChangeTracker.Entries().Count(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added);
        _context.SaveChanges();
        _audit.Record("MigrationImported", "Import", Guid.Empty, null, null, new { Sheets = sheets });
        _context.SaveChanges();

        return new ImportResult
        {
            Success = true,
            RowsProcessed = rowsProcessed,
            Message = $"Imported workbook. Sheets found: {string.Join(", ", sheets)}. Review queue items: {_reviewQueue.Count}.",
            Errors = _reviewQueue.Select(r => r.Issue ?? string.Empty).ToList()
        };
    }

    private static bool NameMatches(string sheetName, params string[] candidates)
    {
        var normalized = sheetName.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        return candidates.Any(c => normalized == c.Replace(" ", "").Replace("_", "").ToLowerInvariant());
    }

    private void ImportStudents(IXLWorksheet? sheet)
    {
        if (sheet == null) return;
        BuildHeaderMap(sheet);
        var rows = sheet.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            try
            {
                var student = new Student
                {
                    DisplayId = GetString(row, "DisplayId") ?? _idGenerator.NextStudentId(),
                    FirstName = GetString(row, "FirstName") ?? "Unknown",
                    LastName = GetString(row, "LastName") ?? "Unknown",
                    MiddleName = GetString(row, "MiddleName"),
                    PreferredName = GetString(row, "PreferredName"),
                    Email = GetString(row, "Email"),
                    Phone = GetString(row, "Phone"),
                    Employer = GetString(row, "Employer"),
                    WorkGroup = GetString(row, "WorkGroup"),
                    EmployeeNumber = GetString(row, "EmployeeNumber"),
                    USI = GetString(row, "USI"),
                    Notes = GetString(row, "Notes"),
                    Manager = GetString(row, "Manager"),
                    GroupTag = GetString(row, "GroupTag"),
                    IsActive = GetBoolean(row, "IsActive") ?? true,
                    IsArchived = GetBoolean(row, "IsArchived") ?? false
                };
                _context.Students.Add(student);
            }
            catch (Exception ex)
            {
                QueueReview("Student", row.RowNumber(), ex.Message);
            }
        }
    }

    private void ImportCourseDefinitions(IXLWorksheet? sheet)
    {
        if (sheet == null) return;
        BuildHeaderMap(sheet);
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            try
            {
                var course = new CourseDefinition
                {
                    CourseCode = GetString(row, "CourseCode") ?? "MIGRATED",
                    CourseTitle = GetString(row, "CourseTitle") ?? "Migrated Course",
                    MatchKey = GetString(row, "MatchKey") ?? GetString(row, "CourseCode"),
                    Category = GetString(row, "Category"),
                    Provider = GetString(row, "Provider"),
                    DefaultCertificateCost = GetDecimal(row, "DefaultCertificateCost"),
                    CourseDurationDays = GetDecimal(row, "CourseDurationDays") is decimal duration ? (int)duration : null,
                    DefaultCreditQuantity = GetDecimal(row, "DefaultCreditQuantity") ?? 1m,
                    Description = GetString(row, "Description"),
                    IsActive = GetBoolean(row, "IsActive") ?? true,
                    Notes = GetString(row, "Notes")
                };
                _context.CourseDefinitions.Add(course);
            }
            catch (Exception ex)
            {
                QueueReview("CourseDefinition", row.RowNumber(), ex.Message);
            }
        }
    }

    private void ImportCourseDeliveries(IXLWorksheet? sheet)
    {
        if (sheet == null) return;
        BuildHeaderMap(sheet);
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            try
            {
                var courseCode = GetString(row, "CourseCode");
                var course = _context.CourseDefinitions.Local.FirstOrDefault(c => c.CourseCode == courseCode);
                var delivery = new CourseDelivery
                {
                    DisplayId = GetString(row, "DisplayId") ?? _idGenerator.NextDisplayId<CourseDelivery>("DEL"),
                    CourseDefinitionId = course?.Id ?? Guid.Empty,
                    StartDate = GetDate(row, "StartDate"),
                    EndDate = GetDate(row, "EndDate"),
                    Location = GetString(row, "Location"),
                    TrainerName = GetString(row, "TrainerName"),
                    TrainerBusinessDetails = GetString(row, "TrainerBusinessDetails"),
                    Capacity = GetDecimal(row, "Capacity") is decimal cap ? (int)cap : (int?)null,
                    DateStatus = ParseDateStatus(GetString(row, "DateStatus")),
                    DeliveryStatus = GetString(row, "DeliveryStatus") ?? "Scheduled",
                    Notes = GetString(row, "Notes")
                };
                if (delivery.CourseDefinitionId == Guid.Empty)
                    QueueReview("CourseDelivery", row.RowNumber(), $"Course code {courseCode} not found; delivery queued for manual review.");
                else
                    _context.CourseDeliveries.Add(delivery);
            }
            catch (Exception ex)
            {
                QueueReview("CourseDelivery", row.RowNumber(), ex.Message);
            }
        }
    }

    private void ImportAllocations(IXLWorksheet? sheet)
    {
        if (sheet == null) return;
        BuildHeaderMap(sheet);
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            try
            {
                var studentDisplayId = GetString(row, "StudentDisplayId");
                var deliveryDisplayId = GetString(row, "DeliveryDisplayId");
                var studentName = GetString(row, "StudentName");
                var courseCode = GetString(row, "CourseCode");
                var student = studentDisplayId != null
                    ? _context.Students.Local.FirstOrDefault(s => string.Equals(s.DisplayId, studentDisplayId, StringComparison.OrdinalIgnoreCase))
                    : _context.Students.Local.FirstOrDefault(s => string.Equals(s.FullName, studentName, StringComparison.OrdinalIgnoreCase));
                var delivery = deliveryDisplayId != null
                    ? _context.CourseDeliveries.Local.FirstOrDefault(d => string.Equals(d.DisplayId, deliveryDisplayId, StringComparison.OrdinalIgnoreCase))
                    : FindDeliveryByCourseCode(courseCode);
                var budgetPoolName = GetString(row, "BudgetPoolName");
                var creditPoolName = GetString(row, "CreditPoolName");

                if (studentDisplayId != null && student == null)
                {
                    QueueReview("Allocation", row.RowNumber(), $"Student {studentDisplayId} not found; allocation queued.");
                    continue;
                }

                if (delivery == null)
                {
                    QueueReview("Allocation", row.RowNumber(), $"Course delivery {deliveryDisplayId ?? courseCode ?? "(unspecified)"} not found; allocation queued.");
                    continue;
                }

                var alloc = new Allocation
                {
                    DisplayId = GetString(row, "DisplayId") ?? _idGenerator.NextDisplayId<Allocation>("ALL"),
                    StudentId = student?.Id,
                    CourseDeliveryId = delivery.Id,
                    PlaceholderName = student == null ? (GetString(row, "PlaceholderName") ?? studentName ?? "Placeholder") : null,
                    CertificateCost = GetDecimal(row, "CertificateCost"),
                    BudgetPoolId = FindBudgetPool(budgetPoolName)?.Id,
                    CreditPoolId = FindCreditPool(creditPoolName)?.Id,
                    AllocationStatus = ParseAllocationStatus(GetString(row, "AllocationStatus")),
                    OutcomeStatus = ParseOutcomeStatus(GetString(row, "OutcomeStatus")),
                    OutcomeDate = GetDate(row, "OutcomeDate"),
                    OutcomeNotes = GetString(row, "OutcomeNotes"),
                    AttendanceStatus = ParseAttendanceStatus(GetString(row, "AttendanceStatus")),
                    CreditStatus = ParseCreditStatus(GetString(row, "CreditStatus")),
                    CashCommitmentStatus = ParseCashCommitmentStatus(GetString(row, "CashCommitmentStatus")),
                    CertificateOrderStatus = ParseCertificateOrderStatus(GetString(row, "CertificateOrderStatus")),
                    CertificateDeliveryStatus = ParseCertificateDeliveryStatus(GetString(row, "CertificateDeliveryStatus")),
                    IsBillable = GetBoolean(row, "IsBillable") ?? false
                };

                if (budgetPoolName != null && alloc.BudgetPoolId == null)
                    QueueReview("Allocation", row.RowNumber(), $"Budget pool {budgetPoolName} not found; allocation imported without a budget pool.");
                if (creditPoolName != null && alloc.CreditPoolId == null)
                    QueueReview("Allocation", row.RowNumber(), $"Credit pool {creditPoolName} not found; allocation imported without a credit pool.");
                _context.Allocations.Add(alloc);
            }
            catch (Exception ex)
            {
                QueueReview("Allocation", row.RowNumber(), ex.Message);
            }
        }
    }

    private void ImportCreditPools(IXLWorksheet? sheet)
    {
        if (sheet == null) return;
        BuildHeaderMap(sheet);
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            try
            {
                var pool = new CertificateCreditPool
                {
                    Name = GetString(row, "Name") ?? "Migrated Pool",
                    Provider = GetString(row, "Provider"),
                    UnitType = ParseCreditUnitType(GetString(row, "UnitType")),
                    ExpiryDate = GetDate(row, "ExpiryDate"),
                    IsActive = GetBoolean(row, "IsActive") ?? true,
                    Notes = GetString(row, "Notes")
                };
                _context.CertificateCreditPools.Add(pool);

                var topUp = GetDecimal(row, "OpeningBalance");
                if (topUp.HasValue && topUp.Value > 0)
                {
                    _context.CertificateCreditTransactions.Add(new CertificateCreditTransaction
                    {
                        DisplayId = _idGenerator.NextDisplayId<CertificateCreditTransaction>("CTX"),
                        PoolId = pool.Id,
                        TransactionType = CreditTransactionType.TopUp,
                        Amount = topUp.Value,
                        Quantity = topUp.Value,
                        SourceType = CreditSourceType.Migration,
                        Reason = "Migrated opening balance",
                        TransactionDateTime = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                QueueReview("CertificateCreditPool", row.RowNumber(), ex.Message);
            }
        }
    }

    private void ImportBudgetPools(IXLWorksheet? sheet)
    {
        if (sheet == null) return;
        BuildHeaderMap(sheet);
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            try
            {
                var pool = new BudgetPool
                {
                    Name = GetString(row, "Name") ?? "Migrated Budget",
                    FinancialPeriod = GetString(row, "FinancialPeriod"),
                    IsActive = GetBoolean(row, "IsActive") ?? true,
                    Notes = GetString(row, "Notes")
                };
                _context.BudgetPools.Add(pool);

                var funds = GetDecimal(row, "OpeningBalance");
                if (funds.HasValue && funds.Value > 0)
                {
                    _context.BudgetTransactions.Add(new BudgetTransaction
                    {
                        DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
                        PoolId = pool.Id,
                        TransactionType = BudgetTransactionType.FundsAdded,
                        Amount = funds.Value,
                        Reason = "Migrated opening balance",
                        TransactionDate = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                QueueReview("BudgetPool", row.RowNumber(), ex.Message);
            }
        }
    }

    private CourseDelivery? FindDeliveryByCourseCode(string? courseCode)
    {
        if (courseCode == null) return null;
        var course = _context.CourseDefinitions.Local.FirstOrDefault(c => string.Equals(c.CourseCode, courseCode, StringComparison.OrdinalIgnoreCase))
            ?? _context.CourseDefinitions.FirstOrDefault(c => c.CourseCode == courseCode);
        if (course == null) return null;
        return _context.CourseDeliveries.Local.FirstOrDefault(d => d.CourseDefinitionId == course.Id)
            ?? _context.CourseDeliveries.FirstOrDefault(d => d.CourseDefinitionId == course.Id);
    }

    private BudgetPool? FindBudgetPool(string? name)
    {
        if (name == null) return null;
        return _context.BudgetPools.Local.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? _context.BudgetPools.FirstOrDefault(p => p.Name == name);
    }

    private CertificateCreditPool? FindCreditPool(string? name)
    {
        if (name == null) return null;
        return _context.CertificateCreditPools.Local.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? _context.CertificateCreditPools.FirstOrDefault(p => p.Name == name);
    }

    private void QueueReview(string entityType, int sourceRow, string issue)
    {
        _reviewQueue.Add(new ImportReviewQueue
        {
            SourceRow = sourceRow,
            EntityType = entityType,
            Issue = issue,
            Status = "Pending"
        });
    }

    private void BuildHeaderMap(IXLWorksheet sheet)
    {
        _headerMap = sheet.Row(1).CellsUsed()
            .ToDictionary(
                c => c.GetString().Trim().Replace(" ", "").ToLowerInvariant(),
                c => c.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);
    }

    private string? GetString(IXLRow row, string columnName)
    {
        if (_headerMap == null || !_headerMap.TryGetValue(NormalizeHeader(columnName), out var col)) return null;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;
        return cell.GetValue<string>().Trim();
    }

    private DateTime? GetDate(IXLRow row, string columnName)
    {
        if (_headerMap == null || !_headerMap.TryGetValue(NormalizeHeader(columnName), out var col)) return null;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;
        if (cell.TryGetValue<DateTime>(out var dt)) return dt;
        var value = cell.GetValue<string>().Trim();
        return DateTime.TryParseExact(value, ["dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
            ? dt
            : null;
    }

    private bool? GetBoolean(IXLRow row, string columnName)
    {
        if (_headerMap == null || !_headerMap.TryGetValue(NormalizeHeader(columnName), out var col)) return null;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;
        if (cell.TryGetValue<bool>(out var value)) return value;
        return cell.GetValue<string>().Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "1" => true,
            "false" or "no" or "0" => false,
            _ => null
        };
    }

    private decimal? GetDecimal(IXLRow row, string columnName)
    {
        if (_headerMap == null || !_headerMap.TryGetValue(NormalizeHeader(columnName), out var col)) return null;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;
        if (cell.TryGetValue<decimal>(out var d)) return d;
        if (decimal.TryParse(cell.GetValue<string>(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
        return null;
    }

    private static string NormalizeHeader(string name) => name.Replace(" ", "").ToLowerInvariant();

    private static DeliveryDateStatus ParseDateStatus(string? value) => value?.ToLowerInvariant() switch
    {
        "confirmed" => DeliveryDateStatus.Confirmed,
        "estimated" => DeliveryDateStatus.Estimated,
        "tbc" => DeliveryDateStatus.TBC,
        "blank" => DeliveryDateStatus.Blank,
        _ => DeliveryDateStatus.Confirmed
    };

    private static AllocationStatus ParseAllocationStatus(string? value) => Enum.TryParse<AllocationStatus>(value, true, out var v) ? v : AllocationStatus.Enrolled;
    private static OutcomeStatus ParseOutcomeStatus(string? value) => Enum.TryParse<OutcomeStatus>(value, true, out var v) ? v : OutcomeStatus.Pending;
    private static AttendanceStatus ParseAttendanceStatus(string? value) => Enum.TryParse<AttendanceStatus>(value, true, out var v) ? v : AttendanceStatus.NotRecorded;
    private static CreditStatus ParseCreditStatus(string? value) => Enum.TryParse<CreditStatus>(value, true, out var v) ? v : CreditStatus.None;
    private static CashCommitmentStatus ParseCashCommitmentStatus(string? value) => Enum.TryParse<CashCommitmentStatus>(value, true, out var v) ? v : CashCommitmentStatus.None;
    private static CertificateOrderStatus ParseCertificateOrderStatus(string? value) => Enum.TryParse<CertificateOrderStatus>(value, true, out var v) ? v : CertificateOrderStatus.NotReady;
    private static CertificateDeliveryStatus ParseCertificateDeliveryStatus(string? value) => Enum.TryParse<CertificateDeliveryStatus>(value, true, out var v) ? v : CertificateDeliveryStatus.Awaiting;
    private static CreditUnitType ParseCreditUnitType(string? value) => value?.ToLowerInvariant() switch
    {
        "monetary" or "dollar" or "dollars" or "$" => CreditUnitType.Monetary,
        _ => CreditUnitType.Count
    };
}
