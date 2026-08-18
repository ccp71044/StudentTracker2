using System.Globalization;
using ClosedXML.Excel;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class LegacyStudentRegisterImporter
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;
    private readonly List<ImportReviewQueue> _reviewQueue = new();

    public LegacyStudentRegisterImporter(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public IReadOnlyList<ImportReviewQueue> ReviewQueue => _reviewQueue;

    public ImportResult Import(string xlsxPath)
    {
        using var workbook = new XLWorkbook(xlsxPath);
        var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == "Sheet1") ?? workbook.Worksheets.First();

        var headerRow = FindHeaderRow(worksheet);
        if (headerRow < 0)
            return new ImportResult { Success = false, Message = "Could not locate the header row. Expected 'First Name' and 'Last Name' columns." };

        var map = BuildColumnMap(worksheet, headerRow);
        var budgetPool = EnsureBudgetPool();

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;
        for (int rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            try
            {
                ProcessRow(row, map, budgetPool);
            }
            catch (Exception ex)
            {
                QueueReview("Row", rowNumber, ex.Message);
            }
        }

        _context.SaveChanges();
        _audit.Record("LegacyStudentRegisterImported", "Import", Guid.NewGuid());
        _context.SaveChanges();

        return new ImportResult
        {
            Success = true,
            RowsProcessed = _context.ChangeTracker.Entries().Count(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added),
            Message = $"Legacy student register imported. Review queue items: {_reviewQueue.Count}.",
            Errors = _reviewQueue.Select(r => r.Issue ?? string.Empty).ToList()
        };
    }

    private static int FindHeaderRow(IXLWorksheet worksheet)
    {
        foreach (var row in worksheet.RowsUsed())
        {
            var cells = row.CellsUsed().Select(c => c.GetString().Trim()).ToList();
            if (cells.Contains("First Name", StringComparer.OrdinalIgnoreCase) &&
                cells.Contains("Last Name", StringComparer.OrdinalIgnoreCase) &&
                cells.Contains("Course", StringComparer.OrdinalIgnoreCase))
            {
                return row.RowNumber();
            }
        }
        return -1;
    }

    private static ColumnMap BuildColumnMap(IXLWorksheet worksheet, int headerRowNumber)
    {
        var map = new ColumnMap();
        var headerRow = worksheet.Row(headerRowNumber);
        foreach (var cell in headerRow.CellsUsed())
        {
            var value = cell.GetString().Trim().Replace(" ", "").ToLowerInvariant();
            map.Columns[value] = cell.Address.ColumnNumber;
        }

        // Fallback to known positions if headers are missing or merged.
        map.TrySet("firstname", 1);
        map.TrySet("lastname", 2);
        map.TrySet("email", 3);
        map.TrySet("phone", 4);
        map.TrySet("course", 5);
        map.TrySet("date", 6);
        map.TrySet("cost(cert)", 7);
        map.TrySet("cost", 7);
        map.TrySet("notes", 8);
        map.TrySet("group", 9);
        map.TrySet("topupdate", 14);
        map.TrySet("topupamount", 15);
        map.TrySet("topupnotes", 16);

        return map;
    }

    private void ProcessRow(IXLRow row, ColumnMap map, BudgetPool budgetPool)
    {
        var firstName = GetString(row, map, "firstname");
        var lastName = GetString(row, map, "lastname");

        ProcessTopUp(row, map, budgetPool);

        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
            return;

        var email = GetString(row, map, "email");
        var phone = GetString(row, map, "phone");
        var courseRaw = GetString(row, map, "course");
        var date = GetDate(row, map, "date");
        var cost = GetDecimal(row, map, "cost");
        var notes = GetString(row, map, "notes");
        var group = GetString(row, map, "group");

        if (string.IsNullOrWhiteSpace(courseRaw))
        {
            QueueReview("Student", row.RowNumber(), $"No course specified for {firstName} {lastName}.");
            return;
        }

        var (courseCode, courseTitle) = SplitCourse(courseRaw);
        var student = EnsureStudent(firstName, lastName, email, phone, group);
        var course = EnsureCourseDefinition(courseCode, courseTitle, cost);
        var delivery = EnsureDelivery(course, date);

        var allocation = new Allocation
        {
            DisplayId = _idGenerator.NextDisplayId<Allocation>("ALL"),
            StudentId = student.Id,
            CourseDeliveryId = delivery.Id,
            AllocatedAt = date ?? DateTime.UtcNow,
            CertificateCost = cost,
            OutcomeNotes = notes,
            AllocationStatus = AllocationStatus.Enrolled,
            OutcomeStatus = InferOutcome(notes),
            AttendanceStatus = AttendanceStatus.NotRecorded,
            CreditStatus = CreditStatus.None,
            CashCommitmentStatus = CashCommitmentStatus.None,
            CertificateOrderStatus = CertificateOrderStatus.NotReady,
            CertificateDeliveryStatus = CertificateDeliveryStatus.NotApplicable,
            IsBillable = cost.GetValueOrDefault() > 0 && InferOutcome(notes) == OutcomeStatus.Completed
        };

        if (allocation.OutcomeStatus == OutcomeStatus.Completed && allocation.CertificateCost.GetValueOrDefault() > 0)
            allocation.CertificateOrderStatus = CertificateOrderStatus.Ready;

        _context.Allocations.Add(allocation);
    }

    private Student EnsureStudent(string? firstName, string? lastName, string? email, string? phone, string? group)
    {
        var normalizedEmail = email?.Trim() ?? string.Empty;
        var existing = _context.Students.Local.FirstOrDefault(s =>
            (!string.IsNullOrEmpty(normalizedEmail) && s.Email == normalizedEmail) ||
            (s.FirstName == firstName && s.LastName == lastName));

        if (existing != null)
        {
            existing.WorkGroup = !string.IsNullOrWhiteSpace(group) ? group : existing.WorkGroup;
            return existing;
        }

        var student = new Student
        {
            DisplayId = _idGenerator.NextStudentId(),
            FirstName = firstName ?? "Unknown",
            LastName = lastName ?? "Unknown",
            Email = email,
            Phone = phone,
            WorkGroup = group,
            IsArchived = false
        };
        _context.Students.Add(student);
        return student;
    }

    private CourseDefinition EnsureCourseDefinition(string courseCode, string courseTitle, decimal? defaultCost)
    {
        var existing = _context.CourseDefinitions.Local.FirstOrDefault(c => c.CourseCode == courseCode)
            ?? _context.CourseDefinitions.FirstOrDefault(c => c.CourseCode == courseCode);

        if (existing != null)
            return existing;

        var course = new CourseDefinition
        {
            CourseCode = courseCode,
            CourseTitle = courseTitle,
            Provider = "Imported",
            DefaultCertificateCost = defaultCost,
            DefaultCreditQuantity = 1m
        };
        _context.CourseDefinitions.Add(course);
        return course;
    }

    private CourseDelivery EnsureDelivery(CourseDefinition course, DateTime? date)
    {
        var dateValue = date ?? DateTime.UtcNow;
        var existing = _context.CourseDeliveries.Local.FirstOrDefault(d => d.CourseDefinitionId == course.Id && d.StartDate == dateValue);
        if (existing != null)
            return existing;

        var delivery = new CourseDelivery
        {
            DisplayId = _idGenerator.NextDisplayId<CourseDelivery>("DEL"),
            CourseDefinitionId = course.Id,
            StartDate = dateValue,
            EndDate = dateValue,
            DateStatus = DeliveryDateStatus.Confirmed,
            Location = "Imported"
        };
        _context.CourseDeliveries.Add(delivery);
        return delivery;
    }

    private BudgetPool EnsureBudgetPool()
    {
        var existing = _context.BudgetPools.Local.FirstOrDefault(b => b.Name == "Legacy Budget")
            ?? _context.BudgetPools.FirstOrDefault(b => b.Name == "Legacy Budget");

        if (existing != null)
            return existing;

        var pool = new BudgetPool
        {
            Name = "Legacy Budget",
            FinancialPeriod = "Imported",
            Notes = "Imported from legacy student register"
        };
        _context.BudgetPools.Add(pool);
        return pool;
    }

    private void ProcessTopUp(IXLRow row, ColumnMap map, BudgetPool pool)
    {
        var topUpDate = GetDate(row, map, "topupdate");
        var topUpAmount = GetDecimal(row, map, "topupamount");
        var topUpNotes = GetString(row, map, "topupnotes");

        if (!topUpDate.HasValue || !topUpAmount.HasValue || topUpAmount.Value <= 0)
            return;

        var transaction = new BudgetTransaction
        {
            DisplayId = _idGenerator.NextDisplayId<BudgetTransaction>("BTX"),
            PoolId = pool.Id,
            TransactionType = BudgetTransactionType.FundsAdded,
            Amount = topUpAmount.Value,
            TransactionDate = topUpDate.Value,
            Reason = topUpNotes ?? "Legacy top-up"
        };
        _context.BudgetTransactions.Add(transaction);
    }

    private static (string Code, string Title) SplitCourse(string courseRaw)
    {
        var parts = courseRaw.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        var code = parts[0].Trim();
        var title = parts.Length > 1 ? parts[1].Trim() : code;
        return (code, title);
    }

    private static OutcomeStatus InferOutcome(string? notes)
    {
        var text = notes?.ToLowerInvariant() ?? string.Empty;
        if (text.Contains("cancel")) return OutcomeStatus.Cancelled;
        if (text.Contains("withdraw")) return OutcomeStatus.Withdrawn;
        return OutcomeStatus.Completed;
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

    private static string? GetString(IXLRow row, ColumnMap map, string key)
    {
        if (!map.TryGetColumn(key, out var col)) return null;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;
        return cell.GetValue<string>().Trim();
    }

    private static DateTime? GetDate(IXLRow row, ColumnMap map, string key)
    {
        if (!map.TryGetColumn(key, out var col)) return null;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;
        if (cell.TryGetValue<DateTime>(out var dt)) return dt;
        return null;
    }

    private static decimal? GetDecimal(IXLRow row, ColumnMap map, string key)
    {
        if (!map.TryGetColumn(key, out var col)) return null;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;
        if (cell.TryGetValue<decimal>(out var d)) return d;
        if (decimal.TryParse(cell.GetValue<string>(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
        return null;
    }

    private class ColumnMap
    {
        public Dictionary<string, int> Columns { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void TrySet(string key, int column)
        {
            if (!Columns.ContainsKey(key))
                Columns[key] = column;
        }

        public bool TryGetColumn(string key, out int column)
        {
            return Columns.TryGetValue(key, out column);
        }
    }
}
