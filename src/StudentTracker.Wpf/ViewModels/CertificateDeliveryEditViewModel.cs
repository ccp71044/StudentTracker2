using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CertificateDeliveryEditViewModel : ViewModelBase, ICloseable
{
    private readonly CertificateService _certificateService;
    private readonly DocumentService _documentService;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Record Certificate Delivery";

    [ObservableProperty]
    private CertificateOrder? _selectedOrder;

    [ObservableProperty]
    private ObservableCollection<CertificateOrder> _availableOrders = new();

    [ObservableProperty]
    private DateTime _deliveredDate = DateTime.Now;

    [ObservableProperty]
    private string _deliveryMethod = string.Empty;

    [ObservableProperty]
    private string _deliveredTo = string.Empty;

    [ObservableProperty]
    private string? _recipientDetails;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private ObservableCollection<Document> _availableDocuments = new();

    [ObservableProperty]
    private Document? _selectedEvidenceDocument;

    public CertificateDeliveryEditViewModel(CertificateService certificateService, DocumentService documentService)
    {
        _certificateService = certificateService;
        _documentService = documentService;

        AvailableOrders = new ObservableCollection<CertificateOrder>();
        AvailableDocuments = new ObservableCollection<Document>();

        LoadDataAsync().ConfigureAwait(false);
    }

    private async Task LoadDataAsync()
    {
        var orders = await _certificateService.GetOrdersAsync();
        var awaiting = orders.Where(o => o.Status == CertificateOrderStatus.Ordered).ToList();
        AvailableOrders = new ObservableCollection<CertificateOrder>(awaiting);

        var documents = await _documentService.GetDocumentsForEntityAsync("All", Guid.Empty);
        AvailableDocuments = new ObservableCollection<Document>(documents);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedOrder == null)
        {
            // Show error - order required
            return;
        }

        if (string.IsNullOrEmpty(DeliveryMethod))
        {
            // Show error - delivery method required
            return;
        }

        if (string.IsNullOrEmpty(DeliveredTo))
        {
            // Show error - delivered to required
            return;
        }

        try
        {
            await _certificateService.RecordDeliveryAsync(
                SelectedOrder.Id,
                DeliveredDate,
                DeliveryMethod,
                DeliveredTo,
                Notes,
                SelectedEvidenceDocument?.Id);

            RequestClose?.Invoke(true);
        }
        catch
        {
            // Show error
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}