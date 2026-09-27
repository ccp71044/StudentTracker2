using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class StudentViewViewModel : ViewModelBase, ICloseable
{
    private readonly StudentService _studentService;
    private readonly IDialogService _dialogService;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private Student _student;

    [ObservableProperty]
    private List<AllocationRow> _upcomingClasses = new();

    [ObservableProperty]
    private List<AllocationRow> _pastClasses = new();

    [ObservableProperty]
    private string _title = "Student";

    public StudentViewViewModel(Student student, List<Allocation> allocations, StudentService studentService, IDialogService dialogService)
    {
        Student = student;
        _studentService = studentService;
        _dialogService = dialogService;
        Title = $"{student.FullName}";

        var today = DateTime.Today;
        var rows = allocations.Select(a => new AllocationRow
        {
            AllocationId = a.DisplayId,
            CourseCode = a.CourseDelivery?.CourseDefinition?.CourseCode ?? a.CourseDelivery?.DisplayId,
            StartDate = a.CourseDelivery?.StartDate,
            EndDate = a.CourseDelivery?.EndDate,
            Location = a.CourseDelivery?.Location,
            Status = a.AllocationStatus.ToString(),
            Outcome = a.OutcomeStatus.ToString()
        }).ToList();

        UpcomingClasses = rows.Where(r => r.StartDate.HasValue && r.StartDate.Value.Date >= today)
            .OrderBy(r => r.StartDate).ToList();
        PastClasses = rows.Where(r => !r.StartDate.HasValue || r.StartDate.Value.Date < today)
            .OrderByDescending(r => r.StartDate).ToList();
    }

    [RelayCommand]
    private void EditStudent() => Guard("EditStudent", () =>
    {
        var vm = new StudentEditViewModel(Student, _studentService, isNew: false);
        if (_dialogService.ShowDialog(vm) == true)
        {
            Title = Student.FullName;
            OnPropertyChanged(nameof(Student));
        }
    });

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(null);
    }
}

public class AllocationRow
{
    public string? AllocationId { get; set; }
    public string? CourseCode { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Location { get; set; }
    public string? Status { get; set; }
    public string? Outcome { get; set; }
    public string DateRange => $"{StartDate:dd/MM/yyyy} - {EndDate:dd/MM/yyyy}";
}
