using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class TransferAllocationViewModel : ViewModelBase, ICloseable
{
    private readonly AllocationService _allocationService;
    private readonly StudentService _studentService;
    private readonly CourseService _courseService;
    private readonly Allocation _sourceAllocation;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Transfer Allocation";

    [ObservableProperty]
    private Student? _selectedStudent;

    [ObservableProperty]
    private CourseDelivery? _selectedDelivery;

    [ObservableProperty]
    private ObservableCollection<Student> _availableStudents = new();

    [ObservableProperty]
    private ObservableCollection<CourseDelivery> _availableDeliveries = new();

    public TransferAllocationViewModel(Allocation sourceAllocation, AllocationService allocationService, StudentService studentService, CourseService courseService)
    {
        _sourceAllocation = sourceAllocation;
        _allocationService = allocationService;
        _studentService = studentService;
        _courseService = courseService;
    }

    public string SourceDisplayId => _sourceAllocation.DisplayId ?? string.Empty;

    public async Task LoadDataAsync()
    {
        var students = await _studentService.SearchAsync(string.Empty);
        var deliveries = await _courseService.GetDeliveriesAsync();
        AvailableStudents = new ObservableCollection<Student>(students);
        AvailableDeliveries = new ObservableCollection<CourseDelivery>(deliveries);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedStudent == null || SelectedDelivery == null)
        {
            return;
        }

        await _allocationService.TransferAsync(
            _sourceAllocation.Id,
            SelectedStudent.Id,
            SelectedDelivery.Id);

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
