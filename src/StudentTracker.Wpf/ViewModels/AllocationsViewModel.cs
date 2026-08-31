using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Data;
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
    private readonly StudentTrackerDbContext _context;

    [ObservableProperty]
    private ObservableCollection<Allocation> _allocations = new();

    [ObservableProperty]
    private Allocation? _selectedAllocation;

    public AllocationsViewModel(AllocationService allocationService, StudentService studentService, CourseService courseService, CreditService creditService, BudgetService budgetService, IDialogService dialogService, StudentTrackerDbContext context)
    {
        _allocationService = allocationService;
        _studentService = studentService;
        _courseService = courseService;
        _creditService = creditService;
        _budgetService = budgetService;
        _dialogService = dialogService;
        _context = context;
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
        var vm = new AllocationEditViewModel(new Allocation(), _allocationService, _studentService, _courseService, _creditService, _budgetService, _context, isNew: true);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task AddPlaceholder()
    {
        var vm = new PlaceholderAllocationViewModel(_allocationService, _courseService, _budgetService);
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
        var vm = new AllocationEditViewModel(SelectedAllocation, _allocationService, _studentService, _courseService, _creditService, _budgetService, _context, isNew: false);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanReplacePlaceholder))]
    private async Task ReplacePlaceholder()
    {
        if (SelectedAllocation == null) return;
        var vm = new ReplacePlaceholderViewModel(SelectedAllocation, _allocationService, _studentService);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCarryForwardPlaceholder))]
    private async Task CarryForwardPlaceholder()
    {
        if (SelectedAllocation == null) return;
        var vm = new CarryForwardViewModel(SelectedAllocation, _courseService);
        await vm.LoadAsync();
        if (_dialogService.ShowDialog(vm) == true && vm.SelectedDelivery != null)
        {
            try
            {
                await _allocationService.CarryForwardPlaceholderAsync(SelectedAllocation.Id, vm.SelectedDelivery.Id, vm.Reason);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("The placeholder could not be carried forward.", ex);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditAllocation))]
    private async Task MarkAttendance()
    {
        if (SelectedAllocation == null) return;
        await EditAllocation();
    }

    [RelayCommand(CanExecute = nameof(CanEditAllocation))]
    private async Task MarkOutcome()
    {
        if (SelectedAllocation == null) return;
        await EditAllocation();
    }

    [RelayCommand(CanExecute = nameof(CanTransferAllocation))]
    private async Task TransferAllocation()
    {
        if (SelectedAllocation == null) return;
        var vm = new TransferAllocationViewModel(SelectedAllocation, _allocationService, _studentService, _courseService);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelAllocation))]
    private async Task CancelAllocation()
    {
        if (SelectedAllocation == null || !_dialogService.Confirm($"Cancel allocation {SelectedAllocation.DisplayId}? Pending budget commitments and allocated credit will be released.")) return;
        try
        {
            await _allocationService.CancelAsync(SelectedAllocation.Id, "Cancelled by user");
            await LoadAsync();
            SelectedAllocation = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The allocation could not be cancelled.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateCommitment))]
    private async Task CreateOrRestoreCommitment()
    {
        if (SelectedAllocation == null) return;
        var cost = SelectedAllocation.CertificateCost;
        if (!cost.HasValue || cost.Value <= 0)
        {
            _dialogService.ShowError("Set a certificate cost on the allocation before creating a commitment.");
            return;
        }
        if (!_dialogService.Confirm($"Create/restore a commitment of {cost.Value:C} against budget pool for allocation {SelectedAllocation.DisplayId}?")) return;
        try
        {
            await _allocationService.CreateOrRestoreCommitmentAsync(SelectedAllocation.Id, cost.Value, "Created from allocations view");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The commitment could not be created.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanReleaseCommitment))]
    private async Task ReleaseCommitment()
    {
        if (SelectedAllocation == null) return;
        if (!_dialogService.Confirm($"Release the pending budget commitment for allocation {SelectedAllocation.DisplayId}?")) return;
        try
        {
            await _allocationService.ReleaseCommitmentAsync(SelectedAllocation.Id, "Released from allocations view");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The commitment could not be released.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMarkCostSpent))]
    private async Task MarkCostSpent()
    {
        if (SelectedAllocation == null) return;
        var force = false;
        if (SelectedAllocation.OutcomeStatus != OutcomeStatus.Completed)
        {
            if (!_dialogService.Confirm("The allocation is not marked as completed. Are you sure you want to mark the cost as spent?"))
                return;
            force = true;
        }
        else
        {
            if (!_dialogService.Confirm($"Mark the cost as spent for allocation {SelectedAllocation.DisplayId}?")) return;
        }
        try
        {
            await _allocationService.MarkCostSpentAsync(SelectedAllocation.Id, force, "Marked as spent from allocations view");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The cost could not be marked as spent.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanReverseSpentCost))]
    private async Task ReverseSpentCost()
    {
        if (SelectedAllocation == null) return;
        if (!_dialogService.Confirm($"Reverse the spent cost for allocation {SelectedAllocation.DisplayId}?")) return;
        try
        {
            await _allocationService.ReverseSpentCostAsync(SelectedAllocation.Id, "Reversed from allocations view");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The spent cost could not be reversed.", ex);
        }
    }

    private bool CanEditAllocation => SelectedAllocation != null;
    private bool CanCancelAllocation => SelectedAllocation != null && SelectedAllocation.AllocationStatus != Core.Enums.AllocationStatus.Cancelled && SelectedAllocation.AllocationStatus != Core.Enums.AllocationStatus.Finalised;
    private bool CanReplacePlaceholder => SelectedAllocation != null && !string.IsNullOrEmpty(SelectedAllocation.PlaceholderName);
    private bool CanCarryForwardPlaceholder => SelectedAllocation != null && !string.IsNullOrEmpty(SelectedAllocation.PlaceholderName) && !SelectedAllocation.StudentId.HasValue;
    private bool CanTransferAllocation => SelectedAllocation != null && SelectedAllocation.StudentId.HasValue &&
        (SelectedAllocation.AllocationStatus == Core.Enums.AllocationStatus.Enrolled || SelectedAllocation.AllocationStatus == Core.Enums.AllocationStatus.Active);
    private bool CanCreateCommitment => SelectedAllocation != null && SelectedAllocation.BudgetPoolId.HasValue &&
        (SelectedAllocation.CashCommitmentStatus == CashCommitmentStatus.None || SelectedAllocation.CashCommitmentStatus == CashCommitmentStatus.Released);
    private bool CanReleaseCommitment => SelectedAllocation != null && SelectedAllocation.BudgetPoolId.HasValue && SelectedAllocation.CashCommitmentStatus == CashCommitmentStatus.Pending;
    private bool CanMarkCostSpent => SelectedAllocation != null && SelectedAllocation.BudgetPoolId.HasValue && SelectedAllocation.CashCommitmentStatus == CashCommitmentStatus.Pending;
    private bool CanReverseSpentCost => SelectedAllocation != null && SelectedAllocation.BudgetPoolId.HasValue && SelectedAllocation.CashCommitmentStatus == CashCommitmentStatus.Spent;

    partial void OnSelectedAllocationChanged(Allocation? value)
    {
        EditAllocationCommand.NotifyCanExecuteChanged();
        MarkAttendanceCommand.NotifyCanExecuteChanged();
        MarkOutcomeCommand.NotifyCanExecuteChanged();
        CancelAllocationCommand.NotifyCanExecuteChanged();
        ReplacePlaceholderCommand.NotifyCanExecuteChanged();
        CarryForwardPlaceholderCommand.NotifyCanExecuteChanged();
        TransferAllocationCommand.NotifyCanExecuteChanged();
        CreateOrRestoreCommitmentCommand.NotifyCanExecuteChanged();
        ReleaseCommitmentCommand.NotifyCanExecuteChanged();
        MarkCostSpentCommand.NotifyCanExecuteChanged();
        ReverseSpentCostCommand.NotifyCanExecuteChanged();
    }
}
