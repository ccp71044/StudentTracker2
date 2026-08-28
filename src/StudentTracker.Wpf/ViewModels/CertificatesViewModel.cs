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

    [RelayCommand]
    private async Task RecordDelivery()
    {
        var vm = new CertificateDeliveryEditViewModel(_certificateService, _documentService);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }
}
