using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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

    [ObservableProperty]
    private CertificateDelivery? _selectedDelivery;

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

    [RelayCommand(CanExecute = nameof(CanViewSelectedEvidence))]
    private void ViewSelectedEvidence() => OpenEvidence(SelectedDelivery?.EvidenceDocument);

    [RelayCommand(CanExecute = nameof(CanViewOrderEvidence))]
    private void ViewOrderEvidence() => OpenEvidence(DeliveryHistory.FirstOrDefault(d => d.EvidenceDocument != null)?.EvidenceDocument);

    private void OpenEvidence(Document? document)
    {
        if (document == null) return;
        try
        {
            var path = _documentService.GetFullPath(document);
            if (!File.Exists(path))
            {
                _dialogService.ShowError("The certificate evidence file could not be found.");
                return;
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The certificate evidence could not be opened.", ex);
        }
    }

    private bool CanRecordDelivery => SelectedOrder != null;
    private bool CanShowOrderDetail => SelectedOrder != null;
    private bool CanViewSelectedEvidence => SelectedDelivery?.EvidenceDocument != null;
    private bool CanViewOrderEvidence => DeliveryHistory.Any(d => d.EvidenceDocument != null);

    private async Task LoadDeliveryHistoryAsync()
    {
        DeliveryHistory = SelectedOrder == null
            ? new ObservableCollection<CertificateDelivery>()
            : new ObservableCollection<CertificateDelivery>(await _certificateService.GetDeliveriesAsync(SelectedOrder.Id));
        SelectedDelivery = null;
        ViewOrderEvidenceCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDeliveryChanged(CertificateDelivery? value) => ViewSelectedEvidenceCommand.NotifyCanExecuteChanged();

    partial void OnSelectedOrderChanged(CertificateOrder? value)
    {
        RecordDeliveryCommand.NotifyCanExecuteChanged();
        ShowOrderDetailCommand.NotifyCanExecuteChanged();
        LoadDeliveryHistoryAsync().ConfigureAwait(false);
    }
}
