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
    }

    protected override async Task InitialiseAsync()
    {
        var list = await _allocationService.GetAllocationsAsync();
        Allocations = new ObservableCollection<Allocation>(list);
    }

    [RelayCommand]
    private Task Refresh() => GuardAsync("Refresh", InitialiseAsync);

    [RelayCommand]
    private Task AddAllocation() => GuardAsync("Add allocation", async () =>
    {
        var vm = new AllocationEditViewModel(new Allocation(), _allocationService, _studentService, _courseService, _creditService, _budgetService, isNew: true);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await InitialiseAsync();
        }
    });

    [RelayCommand(CanExecute = nameof(CanEditAllocation))]
    private Task EditAllocation() => GuardAsync("Edit allocation", async () =>
    {
        if (SelectedAllocation == null) return;
        var vm = new AllocationEditViewModel(SelectedAllocation, _allocationService, _studentService, _courseService, _creditService, _budgetService, isNew: false);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await InitialiseAsync();
        }
    });

    [RelayCommand(CanExecute = nameof(CanEditAllocation))]
    private Task MarkAttendance() => EditAllocation();

    [RelayCommand(CanExecute = nameof(CanEditAllocation))]
    private Task MarkOutcome() => EditAllocation();

    private bool CanEditAllocation => SelectedAllocation != null;

    partial void OnSelectedAllocationChanged(Allocation? value)
    {
        EditAllocationCommand.NotifyCanExecuteChanged();
        MarkAttendanceCommand.NotifyCanExecuteChanged();
        MarkOutcomeCommand.NotifyCanExecuteChanged();
    }
}
