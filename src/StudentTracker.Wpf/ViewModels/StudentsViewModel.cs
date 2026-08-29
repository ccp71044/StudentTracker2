using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class StudentsViewModel : ViewModelBase
{
    private readonly StudentService _studentService;
    private readonly AllocationService _allocationService;
    private readonly CourseService _courseService;
    private readonly CreditService _creditService;
    private readonly BudgetService _budgetService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<Student> _students = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private Student? _selectedStudent;

    [ObservableProperty]
    private bool _showArchived;

    public StudentsViewModel(StudentService studentService, AllocationService allocationService, CourseService courseService, CreditService creditService, BudgetService budgetService, IDialogService dialogService)
    {
        _studentService = studentService;
        _allocationService = allocationService;
        _courseService = courseService;
        _creditService = creditService;
        _budgetService = budgetService;
        _dialogService = dialogService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var list = await _studentService.SearchAsync(SearchText, ShowArchived);
        Students = new ObservableCollection<Student>(list);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Search()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddStudent()
    {
        var vm = new StudentEditViewModel(new Student { FirstName = "", LastName = "" }, _studentService, isNew: true);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteStudent))]
    private async Task EditStudent()
    {
        if (SelectedStudent == null) return;
        var vm = new StudentEditViewModel(SelectedStudent, _studentService, isNew: false);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteStudent))]
    private async Task DeleteStudent()
    {
        if (SelectedStudent == null || !_dialogService.Confirm($"Archive {SelectedStudent.FirstName} {SelectedStudent.LastName}? Historical records will be retained.")) return;
        try
        {
            await _studentService.ArchiveAsync(SelectedStudent.Id);
            await LoadAsync();
            SelectedStudent = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The student could not be archived.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestoreStudent))]
    private async Task RestoreStudent()
    {
        if (SelectedStudent == null || !_dialogService.Confirm($"Restore {SelectedStudent.FirstName} {SelectedStudent.LastName}?")) return;
        try
        {
            await _studentService.ArchiveAsync(SelectedStudent.Id, false);
            await LoadAsync();
            SelectedStudent = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The student could not be restored.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteStudent))]
    private async Task ViewStudent()
    {
        if (SelectedStudent == null) return;
        var allocations = await _allocationService.GetByStudentAsync(SelectedStudent.Id);
        var vm = new StudentViewViewModel(SelectedStudent, allocations, _studentService, _dialogService);
        _dialogService.ShowDialog(vm);
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteStudent))]
    private async Task AddAllocation()
    {
        if (SelectedStudent == null) return;
        var allocation = new Allocation { StudentId = SelectedStudent.Id, Student = SelectedStudent };
        var vm = new AllocationEditViewModel(allocation, _allocationService, _studentService, _courseService, _creditService, _budgetService, isNew: true);
        await vm.LoadDataAsync();
        vm.SelectedStudent = SelectedStudent;
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    private bool CanEditOrDeleteStudent => SelectedStudent != null;
    private bool CanRestoreStudent => SelectedStudent?.IsArchived == true;

    partial void OnShowArchivedChanged(bool value) => LoadAsync().ConfigureAwait(false);

    partial void OnSelectedStudentChanged(Student? value)
    {
        EditStudentCommand.NotifyCanExecuteChanged();
        DeleteStudentCommand.NotifyCanExecuteChanged();
        RestoreStudentCommand.NotifyCanExecuteChanged();
        ViewStudentCommand.NotifyCanExecuteChanged();
        AddAllocationCommand.NotifyCanExecuteChanged();
    }
}
