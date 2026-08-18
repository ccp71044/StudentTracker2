using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CertificatesViewModel : ViewModelBase
{
    private readonly CertificateService _certificateService;

    [ObservableProperty]
    private ObservableCollection<CertificateOrder> _orders = new();

    [ObservableProperty]
    private CertificateOrder? _selectedOrder;

    public CertificatesViewModel(CertificateService certificateService)
    {
        _certificateService = certificateService;
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
}
