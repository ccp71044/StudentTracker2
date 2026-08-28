using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class AllocationsViewModel : ViewModelBase
{
    private readonly AllocationService _allocationService;
    private readonly StudentService _studentService;
    private readonly CourseService _courseService;
    private readonly CreditService _creditService;
    private readonly BudgetService _budgetService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<Allocation> _allocations = new();

    [ObservableProperty]
    private Allocation? _selectedAllocation;

    public AllocationsViewModel(AllocationService allocationService, StudentService studentService, CourseService courseService, CreditService creditService, BudgetService budgetService, IDialogService dialogService)
    {
        _allocationService = allocationService;
        _studentService = studentService;
        _courseService = courseService;
        _creditService = creditService;
        _budgetService = budgetService;
        _dialogService = dialogService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var list = await _allocationService.GetAllocationsAsync();
        Allocations = new ObservableCollection<Allocation>(list);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddAllocation()
    {
        var vm = new AllocationEditViewModel(new Allocation(), _allocationService, _studentService, _courseService, _creditService, _budgetService, isNew: true);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditAllocation))]
    private async Task EditAllocation()
    {
        if (SelectedAllocation == null) return;
        var vm = new AllocationEditViewModel(SelectedAllocation, _allocationService, _studentService, _courseService, _creditService, _budgetService, isNew: false);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditAllocation))]
    private async Task MarkAttendance()
    {
        if (SelectedAllocation == null) return;
        // Simple attendance marking dialog could be added here
        // For now, use the edit dialog
        await EditAllocation();
    }

    [RelayCommand(CanExecute = nameof(CanEditAllocation))]
    private async Task MarkOutcome()
    {
        if (SelectedAllocation == null) return;
        // Simple outcome marking dialog could be added here
        // For now, use the edit dialog
        await EditAllocation();
    }

    private bool CanEditAllocation => SelectedAllocation != null;

    partial void OnSelectedAllocationChanged(Allocation? value)
    {
        EditAllocationCommand.NotifyCanExecuteChanged();
        MarkAttendanceCommand.NotifyCanExecuteChanged();
        MarkOutcomeCommand.NotifyCanExecuteChanged();
    }
}
