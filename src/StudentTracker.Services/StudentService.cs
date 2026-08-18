using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Services;

public class StudentService
{
    private readonly StudentTrackerDbContext _context;
    private readonly DisplayIdGenerator _idGenerator;
    private readonly AuditService _audit;

    public StudentService(StudentTrackerDbContext context, DisplayIdGenerator idGenerator, AuditService audit)
    {
        _context = context;
        _idGenerator = idGenerator;
        _audit = audit;
    }

    public async Task<Student?> GetByIdAsync(Guid id)
    {
        return await _context.Students.FindAsync(id);
    }

    public async Task<List<Student>> SearchAsync(string? query)
    {
        var q = _context.Students.Where(s => !s.IsArchived).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var lower = query.ToLower();
            q = q.Where(s =>
                (s.DisplayId != null && s.DisplayId.ToLower().Contains(lower)) ||
                s.FirstName.ToLower().Contains(lower) ||
                s.LastName.ToLower().Contains(lower) ||
                (s.Email != null && s.Email.ToLower().Contains(lower)) ||
                (s.Phone != null && s.Phone.ToLower().Contains(lower)) ||
                (s.Employer != null && s.Employer.ToLower().Contains(lower)) ||
                (s.WorkGroup != null && s.WorkGroup.ToLower().Contains(lower)) ||
                (s.Manager != null && s.Manager.ToLower().Contains(lower)) ||
                (s.GroupTag != null && s.GroupTag.ToLower().Contains(lower)));
        }
        return await q.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync();
    }

    public async Task<Student> CreateAsync(Student student)
    {
        await CheckDuplicatesAsync(student);
        student.DisplayId = _idGenerator.NextStudentId();
        student.CreatedAt = DateTime.UtcNow;
        student.UpdatedAt = DateTime.UtcNow;
        _context.Students.Add(student);
        await _context.SaveChangesAsync();
        _audit.Record("Created", "Student", student.Id, student.DisplayId, null, new { student.FirstName, student.LastName });
        await _context.SaveChangesAsync();
        return student;
    }

    public async Task<Student> UpdateAsync(Student student)
    {
        student.UpdatedAt = DateTime.UtcNow;
        _context.Students.Update(student);
        await _context.SaveChangesAsync();
        _audit.Record("Updated", "Student", student.Id, student.DisplayId);
        await _context.SaveChangesAsync();
        return student;
    }

    public async Task ArchiveAsync(Guid id, bool archived = true)
    {
        var student = await _context.Students.FindAsync(id) ?? throw new ArgumentException("Student not found");
        student.IsArchived = archived;
        student.IsActive = !archived;
        student.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _audit.Record(archived ? "Archived" : "Restored", "Student", student.Id, student.DisplayId);
        await _context.SaveChangesAsync();
    }

    private async Task CheckDuplicatesAsync(Student student)
    {
        var duplicates = await _context.Students
            .Where(s => s.Id != student.Id && !s.IsArchived)
            .Where(s =>
                (s.FirstName.ToLower() == student.FirstName.ToLower() && s.LastName.ToLower() == student.LastName.ToLower()) ||
                (student.Email != null && s.Email != null && s.Email.ToLower() == student.Email.ToLower()))
            .AnyAsync();
        student.PotentialDuplicate = duplicates;
    }

    public async Task<List<Student>> GetPotentialDuplicatesAsync()
    {
        return await _context.Students.Where(s => s.PotentialDuplicate && !s.IsArchived).ToListAsync();
    }
}
