using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CertificatesViewModel : ViewModelBase
{
    private readonly CertificateService _certificateService;
    private readonly ReportService _reportService;
    private readonly AllocationService _allocationService;
    private readonly DocumentService _documentService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<CertificateOrder> _orders = new();

    [ObservableProperty]
    private CertificateOrder? _selectedOrder;

    [ObservableProperty]
    private ObservableCollection<CertificateDelivery> _deliveryHistory = new();

    public CertificatesViewModel(CertificateService certificateService, ReportService reportService, AllocationService allocationService, DocumentService documentService, IDialogService dialogService)
    {
        _certificateService = certificateService;
        _reportService = reportService;
        _allocationService = allocationService;
        _documentService = documentService;
        _dialogService = dialogService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var list = await _certificateService.GetOrdersAsync();
        Orders = new ObservableCollection<CertificateOrder>(list);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NewOrder()
    {
        var vm = new CertificateOrderEditViewModel(_certificateService, _reportService, _allocationService);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRecordDelivery))]
    private async Task RecordDelivery()
    {
        if (SelectedOrder == null) return;
        var vm = new CertificateDeliveryEditViewModel(_certificateService, _documentService, SelectedOrder);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
            await LoadDeliveryHistoryAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanShowOrderDetail))]
    private async Task ShowOrderDetail()
    {
        await LoadDeliveryHistoryAsync();
    }

    private bool CanRecordDelivery => SelectedOrder != null;
    private bool CanShowOrderDetail => SelectedOrder != null;

    private async Task LoadDeliveryHistoryAsync()
    {
        DeliveryHistory = SelectedOrder == null
            ? new ObservableCollection<CertificateDelivery>()
            : new ObservableCollection<CertificateDelivery>(await _certificateService.GetDeliveriesAsync(SelectedOrder.Id));
    }

    partial void OnSelectedOrderChanged(CertificateOrder? value)
    {
        RecordDeliveryCommand.NotifyCanExecuteChanged();
        ShowOrderDetailCommand.NotifyCanExecuteChanged();
        LoadDeliveryHistoryAsync().ConfigureAwait(false);
    }
}
