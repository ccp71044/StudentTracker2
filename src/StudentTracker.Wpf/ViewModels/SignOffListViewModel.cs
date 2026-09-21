using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class SignOffListViewModel : ViewModelBase, ICloseable
{
    private readonly CourseDelivery _delivery;
    private readonly SignOffService _signOffService;
    private readonly AllocationService _allocationService;
    private readonly PdfService _pdfService;
    private readonly DocumentService _documentService;
    private readonly IDialogService _dialogService;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Records of Completion";

    [ObservableProperty]
    private ObservableCollection<SignOff> _signOffs = new();

    [ObservableProperty]
    private SignOff? _selectedSignOff;

    public string DeliveryInfo { get; }

    public SignOffListViewModel(
        CourseDelivery delivery,
        SignOffService signOffService,
        AllocationService allocationService,
        PdfService pdfService,
        DocumentService documentService,
        IDialogService dialogService)
    {
        _delivery = delivery;
        _signOffService = signOffService;
        _allocationService = allocationService;
        _pdfService = pdfService;
        _documentService = documentService;
        _dialogService = dialogService;

        DeliveryInfo = $"{delivery.DisplayId} — {delivery.CourseDefinition?.CourseCode ?? "Unknown"} - {delivery.CourseDefinition?.CourseTitle ?? ""}";
        Title = $"Records of Completion — {delivery.DisplayId}";
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var list = await _signOffService.GetForDeliveryAsync(_delivery.Id);
        SignOffs = new ObservableCollection<SignOff>(list);
    }

    [RelayCommand]
    private async Task GenerateNew()
    {
        var allocations = await _allocationService.GetByDeliveryAsync(_delivery.Id);
        var vm = new SignOffEditViewModel(
            null, _delivery, allocations,
            _signOffService, _pdfService, _documentService, _dialogService);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task EditSignOff()
    {
        if (SelectedSignOff == null) return;
        var signOff = await _signOffService.GetAsync(SelectedSignOff.Id);
        if (signOff == null) return;
        var allocations = await _allocationService.GetByDeliveryAsync(_delivery.Id);
        var vm = new SignOffEditViewModel(
            signOff, _delivery, allocations,
            _signOffService, _pdfService, _documentService, _dialogService);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private void OpenPdf()
    {
        if (SelectedSignOff?.FileDocumentId == null) return;
        try
        {
            var path = _documentService.GetDocumentPath(SelectedSignOff.FileDocumentId.Value);
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else
                _dialogService.ShowError("The PDF file could not be found on disk.");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Could not open the PDF.", ex);
        }
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(null);
    }

    private bool CanEditSignOff => SelectedSignOff != null;
    private bool CanOpenPdf => SelectedSignOff?.FileDocumentId != null;

    partial void OnSelectedSignOffChanged(SignOff? value)
    {
        EditSignOffCommand.NotifyCanExecuteChanged();
        OpenPdfCommand.NotifyCanExecuteChanged();
    }
}
