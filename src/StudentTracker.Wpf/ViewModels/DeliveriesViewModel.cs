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
    private readonly SignOffService _signOffService;
    private readonly PdfService _pdfService;
    private readonly DocumentService _documentService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<CourseDelivery> _deliveries = new();

    [ObservableProperty]
    private CourseDelivery? _selectedDelivery;

    [ObservableProperty]
    private bool _isInlineEditingEnabled;

    public DeliveriesViewModel(CourseService courseService, AllocationService allocationService, StudentService studentService, CreditService creditService, BudgetService budgetService, SignOffService signOffService, PdfService pdfService, DocumentService documentService, IDialogService dialogService)
    {
        _courseService = courseService;
        _allocationService = allocationService;
        _studentService = studentService;
        _creditService = creditService;
        _budgetService = budgetService;
        _signOffService = signOffService;
        _pdfService = pdfService;
        _documentService = documentService;
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

    [RelayCommand]
    private async Task DeliveryRowEditEnding(CourseDelivery? delivery)
    {
        if (delivery == null) return;
        try
        {
            var update = new CourseDelivery
            {
                Id = delivery.Id,
                CourseDefinitionId = delivery.CourseDefinitionId,
                DisplayId = delivery.DisplayId,
                CreatedAt = delivery.CreatedAt,
                UpdatedAt = delivery.UpdatedAt,
                StartDate = delivery.StartDate,
                EndDate = delivery.EndDate,
                DateStatus = delivery.DateStatus,
                Location = delivery.Location,
                TrainerName = delivery.TrainerName,
                TrainerBusinessDetails = delivery.TrainerBusinessDetails,
                Capacity = delivery.Capacity,
                DeliveryStatus = delivery.DeliveryStatus,
                Notes = delivery.Notes
            };
            await _courseService.UpdateDeliveryAsync(update);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The delivery changes could not be saved. The table has been reverted to the saved values.", ex);
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

    [RelayCommand(CanExecute = nameof(CanEditDelivery))]
    private void RecordOfCompletion()
    {
        if (SelectedDelivery == null) return;
        var vm = new SignOffListViewModel(
            SelectedDelivery,
            _signOffService,
            _allocationService,
            _pdfService,
            _documentService,
            _dialogService);
        _dialogService.ShowDialog(vm);
    }

    private bool CanEditDelivery => SelectedDelivery != null;
    private bool CanCancelDelivery => SelectedDelivery != null && SelectedDelivery.DeliveryStatus != "Cancelled" && SelectedDelivery.DeliveryStatus != "Completed";

    partial void OnSelectedDeliveryChanged(CourseDelivery? value)
    {
        EditDeliveryCommand.NotifyCanExecuteChanged();
        CancelDeliveryCommand.NotifyCanExecuteChanged();
        ViewAllocationsCommand.NotifyCanExecuteChanged();
        AddAllocationCommand.NotifyCanExecuteChanged();
        RecordOfCompletionCommand.NotifyCanExecuteChanged();
    }
}
