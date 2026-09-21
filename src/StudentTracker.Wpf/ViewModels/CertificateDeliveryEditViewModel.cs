using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
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

    [ObservableProperty]
    private string? _evidenceFilePath;

    [RelayCommand]
    private void SelectEvidenceFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select issued certificate",
            Filter = "Certificate files (*.pdf;*.png;*.jpg;*.jpeg)|*.pdf;*.png;*.jpg;*.jpeg",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            EvidenceFilePath = dialog.FileName;
            SelectedEvidenceDocument = null;
        }
    }

    public CertificateDeliveryEditViewModel(CertificateService certificateService, DocumentService documentService)
        : this(certificateService, documentService, null)
    {
    }

    public CertificateDeliveryEditViewModel(CertificateService certificateService, DocumentService documentService, CertificateOrder? selectedOrder)
    {
        _certificateService = certificateService;
        _documentService = documentService;
        SelectedOrder = selectedOrder;
        AvailableOrders = new ObservableCollection<CertificateOrder>();
        AvailableDocuments = new ObservableCollection<Document>();
        LoadDataAsync().ConfigureAwait(false);
    }

    private async Task LoadDataAsync()
    {
        var orders = await _certificateService.GetOrdersAsync();
        var awaiting = orders.Where(o => o.Status == CertificateOrderStatus.Ordered).ToList();
        AvailableOrders = new ObservableCollection<CertificateOrder>(awaiting);
        if (SelectedOrder != null && AvailableOrders.All(o => o.Id != SelectedOrder.Id))
            AvailableOrders.Insert(0, SelectedOrder);

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
            var evidenceDocument = SelectedEvidenceDocument;
            if (!string.IsNullOrWhiteSpace(EvidenceFilePath))
            {
                evidenceDocument = await _documentService.AddDocumentAsync(
                    EvidenceFilePath,
                    "Certificates",
                    $"Issued certificate - {SelectedOrder.DisplayId}",
                    "Issued certificate evidence",
                    receivedDate: DeliveredDate);
            }

            await _certificateService.RecordDeliveryAsync(
                SelectedOrder.Id,
                DeliveredDate,
                DeliveryMethod,
                DeliveredTo,
                Notes,
                evidenceDocument?.Id,
                RecipientDetails);

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