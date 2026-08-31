using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;

namespace StudentTracker.Wpf.ViewModels;

public partial class StudentOverviewViewModel : ViewModelBase
{
    private readonly StudentTrackerDbContext _context;

    [ObservableProperty]
    private ObservableCollection<Student> _students = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName), nameof(StudentDisplayId))]
    private Student? _selectedStudent;

    [ObservableProperty]
    private ObservableCollection<StudentCourseItem> _completedCourses = new();

    [ObservableProperty]
    private ObservableCollection<StudentCourseItem> _upcomingCourses = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveNotesCommand))]
    private string? _studentNotes;

    public string FullName => SelectedStudent?.FullName ?? string.Empty;
    public string StudentDisplayId => SelectedStudent?.DisplayId ?? string.Empty;

    public StudentOverviewViewModel(StudentTrackerDbContext context)
    {
        _context = context;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _context.Students
            .Where(s => !s.IsArchived)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .AsNoTracking()
            .ToListAsync();

        foreach (var s in list)
            Students.Add(s);

        if (Students.Any())
            SelectedStudent = Students.First();
    }

    partial void OnSelectedStudentChanged(Student? value)
    {
        if (value == null)
        {
            CompletedCourses.Clear();
            UpcomingCourses.Clear();
            StudentNotes = null;
            return;
        }

        StudentNotes = value.Notes;
        _ = LoadStudentCoursesAsync(value.Id);
    }

    private async Task LoadStudentCoursesAsync(Guid studentId)
    {
        var allocations = await _context.Allocations
            .Where(a => a.StudentId == studentId)
            .Include(a => a.CourseDelivery).ThenInclude(d => d!.CourseDefinition)
            .Include(a => a.ClientPrepaidPool)
            .Include(a => a.BudgetPool)
            .Include(a => a.CreditPool)
            .AsNoTracking()
            .ToListAsync();

        CompletedCourses.Clear();
        UpcomingCourses.Clear();

        foreach (var a in allocations)
        {
            var item = new StudentCourseItem
            {
                AllocationDisplayId = a.DisplayId,
                CourseCode = a.CourseDelivery?.CourseDefinition?.CourseCode ?? string.Empty,
                CourseTitle = a.CourseDelivery?.CourseDefinition?.CourseTitle ?? string.Empty,
                DeliveryDisplayId = a.CourseDelivery?.DisplayId,
                StartDate = a.CourseDelivery?.StartDate,
                Outcome = a.OutcomeStatus.ToString(),
                FundingSource = a.ClientPrepaidPool?.Name ?? a.BudgetPool?.Name ?? a.CreditPool?.Name ?? "—",
                Notes = a.Notes
            };

            if (a.OutcomeStatus == Core.Enums.OutcomeStatus.Completed)
                CompletedCourses.Add(item);
            else if (a.CourseDelivery?.StartDate > DateTime.UtcNow)
                UpcomingCourses.Add(item);
            else
                CompletedCourses.Add(item);
        }
    }

    [RelayCommand]
    private async Task SaveNotes()
    {
        if (SelectedStudent == null) return;

        var student = await _context.Students.FindAsync(SelectedStudent.Id);
        if (student == null) return;

        student.Notes = StudentNotes;
        await _context.SaveChangesAsync();
    }
}

public class StudentCourseItem
{
    public string? AllocationDisplayId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string? DeliveryDisplayId { get; set; }
    public DateTime? StartDate { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string FundingSource { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
