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
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<Student> _students = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private Student? _selectedStudent;

    public StudentsViewModel(StudentService studentService, AllocationService allocationService, IDialogService dialogService)
    {
        _studentService = studentService;
        _allocationService = allocationService;
        _dialogService = dialogService;
    }

    protected override async Task InitialiseAsync()
    {
        var list = await _studentService.SearchAsync(SearchText);
        Students = new ObservableCollection<Student>(list);
    }

    [RelayCommand]
    private Task Search() => GuardAsync("Search", InitialiseAsync);

    [RelayCommand]
    private Task AddStudent() => GuardAsync("AddStudent", async () =>
    {
        var vm = new StudentEditViewModel(new Student { FirstName = "", LastName = "" }, _studentService, isNew: true);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await InitialiseAsync();
        }
    });

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteStudent))]
    private Task EditStudent() => GuardAsync("EditStudent", async () =>
    {
        if (SelectedStudent == null) return;
        var vm = new StudentEditViewModel(SelectedStudent, _studentService, isNew: false);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await InitialiseAsync();
        }
    });

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteStudent))]
    private Task DeleteStudent() => GuardAsync("DeleteStudent", async () =>
    {
        if (SelectedStudent == null) return;
        await _studentService.ArchiveAsync(SelectedStudent.Id);
        await InitialiseAsync();
        SelectedStudent = null;
    });

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteStudent))]
    private Task ViewStudent() => GuardAsync("ViewStudent", async () =>
    {
        if (SelectedStudent == null) return;
        var allocations = await _allocationService.GetByStudentAsync(SelectedStudent.Id);
        var vm = new StudentViewViewModel(SelectedStudent, allocations, _studentService, _dialogService);
        _dialogService.ShowDialog(vm);
    });

    private bool CanEditOrDeleteStudent => SelectedStudent != null;

    partial void OnSelectedStudentChanged(Student? value)
    {
        EditStudentCommand.NotifyCanExecuteChanged();
        DeleteStudentCommand.NotifyCanExecuteChanged();
        ViewStudentCommand.NotifyCanExecuteChanged();
    }
}
