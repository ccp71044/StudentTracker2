using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class ReplacePlaceholderViewModel : ViewModelBase, ICloseable
{
    private readonly AllocationService _allocationService;
    private readonly StudentService _studentService;
    private readonly Allocation _allocation;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Replace Placeholder";

    [ObservableProperty]
    private Student? _selectedStudent;

    [ObservableProperty]
    private ObservableCollection<Student> _availableStudents = new();

    public ReplacePlaceholderViewModel(Allocation allocation, AllocationService allocationService, StudentService studentService)
    {
        _allocation = allocation;
        _allocationService = allocationService;
        _studentService = studentService;
    }

    public string PlaceholderName => _allocation.PlaceholderName ?? string.Empty;
    public string AllocationDisplayId => _allocation.DisplayId ?? string.Empty;

    public async Task LoadDataAsync()
    {
        var students = await _studentService.SearchAsync(string.Empty);
        AvailableStudents = new ObservableCollection<Student>(students);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedStudent == null)
        {
            return;
        }

        await _allocationService.ReplacePlaceholderAsync(_allocation.Id, SelectedStudent.Id);
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
