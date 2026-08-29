using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DocumentMetadataEditViewModel : ViewModelBase, ICloseable
{
    private readonly DocumentService _service;
    private readonly Document _document;

    public event Action<bool?>? RequestClose;

    [ObservableProperty] private string _displayName;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private DateTime? _receivedDate;
    [ObservableProperty] private string? _confidentiality;
    [ObservableProperty] private string? _notes;

    public DocumentMetadataEditViewModel(Document document, DocumentService service)
    {
        _document = document;
        _service = service;
        _displayName = document.DisplayName ?? document.OriginalFileName;
        _description = document.Description;
        _receivedDate = document.ReceivedDate;
        _confidentiality = document.Confidentiality;
        _notes = document.Notes;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(DisplayName)) return;
        await _service.UpdateMetadataAsync(_document.Id, DisplayName, Description, ReceivedDate, Confidentiality, Notes);
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
