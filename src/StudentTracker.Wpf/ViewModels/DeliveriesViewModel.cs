using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DeliveriesViewModel : ViewModelBase
{
    private readonly CourseService _courseService;
    private readonly AllocationService _allocationService;
    private readonly StudentService _studentService;
    private readonly CreditService _creditService;
    private readonly BudgetService _budgetService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<CourseDelivery> _deliveries = new();

    [ObservableProperty]
    private CourseDelivery? _selectedDelivery;

    public DeliveriesViewModel(CourseService courseService, AllocationService allocationService, StudentService studentService, CreditService creditService, BudgetService budgetService, IDialogService dialogService)
    {
        _courseService = courseService;
        _allocationService = allocationService;
        _studentService = studentService;
        _creditService = creditService;
        _budgetService = budgetService;
        _dialogService = dialogService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var list = await _courseService.GetDeliveriesAsync();
        Deliveries = new ObservableCollection<CourseDelivery>(list);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddDelivery()
    {
        var vm = new DeliveryEditViewModel(new CourseDelivery(), _courseService, isNew: true);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditDelivery))]
    private async Task EditDelivery()
    {
        if (SelectedDelivery == null) return;
        var vm = new DeliveryEditViewModel(SelectedDelivery, _courseService, isNew: false);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditDelivery))]
    private async Task ViewAllocations()
    {
        if (SelectedDelivery == null) return;
        var allocations = await _allocationService.GetByDeliveryAsync(SelectedDelivery.Id);
        var vm = new ViewAllocationsViewModel(SelectedDelivery, allocations);
        _dialogService.ShowDialog(vm);
    }

    [RelayCommand(CanExecute = nameof(CanEditDelivery))]
    private async Task AddAllocation()
    {
        if (SelectedDelivery == null) return;
        var allocation = new Allocation { CourseDeliveryId = SelectedDelivery.Id, CourseDelivery = SelectedDelivery };
        var vm = new AllocationEditViewModel(allocation, _allocationService, _studentService, _courseService, _creditService, _budgetService, isNew: true);
        await vm.LoadDataAsync();
        vm.SelectedDelivery = SelectedDelivery;
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelDelivery))]
    private async Task CancelDelivery()
    {
        if (SelectedDelivery == null || !_dialogService.Confirm($"Cancel delivery {SelectedDelivery.DisplayId}?")) return;
        try
        {
            await _courseService.CancelDeliveryAsync(SelectedDelivery.Id);
            await LoadAsync();
            SelectedDelivery = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The delivery could not be cancelled.", ex);
        }
    }

    private bool CanEditDelivery => SelectedDelivery != null;
    private bool CanCancelDelivery => SelectedDelivery != null && SelectedDelivery.DeliveryStatus != "Cancelled" && SelectedDelivery.DeliveryStatus != "Completed";

    partial void OnSelectedDeliveryChanged(CourseDelivery? value)
    {
        EditDeliveryCommand.NotifyCanExecuteChanged();
        CancelDeliveryCommand.NotifyCanExecuteChanged();
        ViewAllocationsCommand.NotifyCanExecuteChanged();
        AddAllocationCommand.NotifyCanExecuteChanged();
    }
}
