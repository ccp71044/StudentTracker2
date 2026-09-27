using ClosedXML.Excel;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

/// <summary>
/// Imports the provider's student list export ("ID, First name, Last name, Dob, Email, Client").
/// The provider's student number is the stable identity; names are not. Anything the export leaves
/// blank, contradicts or spells two ways is preserved and queued for review rather than guessed at.
/// </summary>
public class ProviderStudentListImporter
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;
    private readonly List<ImportReviewQueue> _reviewQueue = new();

    public ProviderStudentListImporter(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public IReadOnlyList<ImportReviewQueue> ReviewQueue => _reviewQueue;

    public int Created { get; private set; }
    public int Updated { get; private set; }

    public ImportResult Import(string xlsxPath)
    {
        using var workbook = new XLWorkbook(xlsxPath);
        return Import(workbook, Path.GetFileName(xlsxPath));
    }

    public ImportResult Import(XLWorkbook workbook, string sourceFileName)
    {
        var worksheet = workbook.Worksheets.First();
        var headerRow = ProviderSheet.FindHeaderRow(worksheet, "ID", "First name", "Email");
        if (headerRow < 0)
            return new ImportResult { Success = false, Message = "Expected 'ID', 'First name' and 'Email' columns in the provider student list." };

        var columns = ProviderSheet.MapColumns(worksheet.Row(headerRow));
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;

        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var providerId = ProviderSheet.Text(row, columns, "id");
            var firstName = ProviderSheet.Text(row, columns, "firstname");
            var lastName = ProviderSheet.Text(row, columns, "lastname");
            var email = ProviderSheet.Text(row, columns, "email");
            var client = ProviderSheet.Text(row, columns, "client");

            if (providerId.Length == 0 && firstName.Length == 0 && email.Length == 0)
                continue;

            if (providerId.Length == 0)
            {
                Queue(sourceFileName, rowNumber, $"No provider student number for '{firstName} {lastName}'.", "Skipped");
                continue;
            }

            var student = FindStudent(providerId, email);
            if (student == null)
            {
                student = new Student
                {
                    DisplayId = _idGenerator.NextStudentId(),
                    ProviderStudentId = providerId,
                    FirstName = firstName,
                    LastName = lastName
                };
                _context.Students.Add(student);
                Created++;
            }
            else
            {
                Updated++;
                if (student.ProviderStudentId == null)
                    student.ProviderStudentId = providerId;
                student.UpdatedAt = DateTime.UtcNow;
            }

            ApplyNames(student, firstName, lastName, sourceFileName, rowNumber);
            ApplyEmail(student, email, sourceFileName, rowNumber);
            ApplyDateOfBirth(student, row, columns, sourceFileName, rowNumber);
            ApplyClient(student, client, sourceFileName, rowNumber);
            FlagPossibleDuplicates(student, sourceFileName, rowNumber);
        }

        _context.SaveChanges();
        _audit.Record("ProviderStudentListImported", "Import", Guid.NewGuid());
        _context.SaveChanges();

        return new ImportResult
        {
            Success = true,
            RowsProcessed = Created + Updated,
            Message = $"Provider student list imported. {Created} new students, {Updated} matched to existing records. Review queue items: {_reviewQueue.Count}.",
            Errors = _reviewQueue.Select(r => r.Issue ?? string.Empty).ToList()
        };
    }

    private Student? FindStudent(string providerId, string email)
    {
        var byProviderId = _context.Students.Local.FirstOrDefault(s => s.ProviderStudentId == providerId)
            ?? _context.Students.FirstOrDefault(s => s.ProviderStudentId == providerId);
        if (byProviderId != null) return byProviderId;

        if (email.Length == 0) return null;

        return _context.Students.Local.FirstOrDefault(s => s.Email != null && s.Email.ToLower() == email.ToLower())
            ?? _context.Students.FirstOrDefault(s => s.Email != null && s.Email.ToLower() == email.ToLower());
    }

    private void ApplyNames(Student student, string firstName, string lastName, string sourceFileName, int rowNumber)
    {
        if (firstName.Length > 0) student.FirstName = firstName;

        if (lastName.Length == 0)
        {
            if (student.LastName.Length == 0)
                Queue(sourceFileName, rowNumber, $"The export has no last name for '{student.FirstName}' (provider id {student.ProviderStudentId}).", "Imported with no last name");
            return;
        }

        if (student.LastName.Length > 0 && !string.Equals(student.LastName, lastName, StringComparison.OrdinalIgnoreCase))
        {
            Queue(sourceFileName, rowNumber,
                $"Provider id {student.ProviderStudentId} is '{lastName}' in this export but '{student.LastName}' on the existing record.",
                "Existing name kept");
            return;
        }

        student.LastName = lastName;
    }

    private void ApplyEmail(Student student, string email, string sourceFileName, int rowNumber)
    {
        if (email.Length == 0) return;

        if (student.Email != null && !string.Equals(student.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            Queue(sourceFileName, rowNumber,
                $"Provider id {student.ProviderStudentId} has email '{email}' in this export but '{student.Email}' on the existing record.",
                "Existing email kept");
            return;
        }

        student.Email = email;
    }

    private void ApplyDateOfBirth(Student student, IXLRow row, IReadOnlyDictionary<string, int> columns, string sourceFileName, int rowNumber)
    {
        if (!columns.TryGetValue("dob", out var column)) return;

        var cell = row.Cell(column);
        if (cell.TryGetValue(out DateTime date))
        {
            student.DateOfBirth = date;
            return;
        }

        var text = cell.GetString().Trim();
        // The provider writes "-" when it holds no date of birth; that is a known blank, not a bad value.
        if (text.Length == 0 || text == "-") return;

        if (DateTime.TryParse(text, out var parsed))
        {
            student.DateOfBirth = parsed;
            return;
        }

        Queue(sourceFileName, rowNumber, $"Could not read the date of birth '{text}' for provider id {student.ProviderStudentId}.", "Left blank");
    }

    private void ApplyClient(Student student, string client, string sourceFileName, int rowNumber)
    {
        if (client.Length == 0) return;

        if (student.WorkGroup == null || student.WorkGroup.Length == 0)
        {
            student.WorkGroup = client;
            return;
        }

        if (!string.Equals(student.WorkGroup, client, StringComparison.OrdinalIgnoreCase))
        {
            Queue(sourceFileName, rowNumber,
                $"Provider id {student.ProviderStudentId} appears under both '{student.WorkGroup}' and '{client}'.",
                $"Kept '{student.WorkGroup}'");
        }
    }

    /// <summary>
    /// Marks students who look like the same person under two provider numbers - the same name, or a
    /// name one character apart such as "Dalmeida" and "Delmeida". They are never merged: the flag
    /// and the review row exist so a human decides.
    /// </summary>
    private void FlagPossibleDuplicates(Student student, string sourceFileName, int rowNumber)
    {
        var candidates = _context.Students.Local
            .Concat(_context.Students)
            .Distinct()
            .Where(s => !ReferenceEquals(s, student) && !s.IsArchived);

        foreach (var other in candidates)
        {
            if (!NameLooksLikeSamePerson(student, other)) continue;

            student.PotentialDuplicate = true;
            other.PotentialDuplicate = true;
            Queue(sourceFileName, rowNumber,
                $"'{student.FullName}' (provider id {student.ProviderStudentId}) closely matches '{other.FullName}' (provider id {other.ProviderStudentId ?? other.DisplayId}).",
                "Both flagged as potential duplicates");
            return;
        }
    }

    private static bool NameLooksLikeSamePerson(Student left, Student right)
    {
        if (!string.Equals(left.FirstName, right.FirstName, StringComparison.OrdinalIgnoreCase)) return false;
        if (left.LastName.Length == 0 || right.LastName.Length == 0) return false;

        return string.Equals(left.LastName, right.LastName, StringComparison.OrdinalIgnoreCase)
            || EditDistanceAtMostOne(left.LastName, right.LastName);
    }

    private static bool EditDistanceAtMostOne(string left, string right)
    {
        if (Math.Abs(left.Length - right.Length) > 1) return false;

        var shorter = left.Length <= right.Length ? left : right;
        var longer = left.Length <= right.Length ? right : left;

        var shortIndex = 0;
        var longIndex = 0;
        var edited = false;

        while (shortIndex < shorter.Length && longIndex < longer.Length)
        {
            if (char.ToLowerInvariant(shorter[shortIndex]) == char.ToLowerInvariant(longer[longIndex]))
            {
                shortIndex++;
                longIndex++;
                continue;
            }

            if (edited) return false;
            edited = true;

            if (shorter.Length == longer.Length) shortIndex++;
            longIndex++;
        }

        return true;
    }

    private void Queue(string sourceFileName, int rowNumber, string issue, string proposedAction)
    {
        _reviewQueue.Add(new ImportReviewQueue
        {
            DisplayId = _idGenerator.NextDisplayId<ImportReviewQueue>("REV"),
            SourceFileName = sourceFileName,
            SourceSheet = "Student List",
            SourceRow = rowNumber,
            EntityType = "Student",
            ProposedAction = proposedAction,
            Issue = issue,
            Status = "Pending"
        });
    }
}
