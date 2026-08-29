using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DocumentsViewModel : ViewModelBase
{
    private readonly DocumentService _documentService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<Document> _documents = new();

    [ObservableProperty]
    private Document? _selectedDocument;

    [ObservableProperty]
    private bool _showArchived;

    public DocumentsViewModel(DocumentService documentService, IDialogService dialogService)
    {
        _documentService = documentService;
        _dialogService = dialogService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        Documents = new ObservableCollection<Document>(await _documentService.GetDocumentsForEntityAsync("All", Guid.Empty, ShowArchived));
    }

    [RelayCommand]
    private async Task AddDocument()
    {
        var dialog = new OpenFileDialog { Multiselect = false };
        if (dialog.ShowDialog() == true)
        {
            await _documentService.AddDocumentAsync(dialog.FileName, "General");
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanViewDocument))]
    private void ViewDocument()
    {
        if (SelectedDocument == null) return;
        
        try
        {
            var filePath = _documentService.GetFullPath(SelectedDocument);
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            else
            {
                _dialogService.ShowError("The document file could not be found.");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The document could not be opened.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteDocument))]
    private async Task DeleteDocument()
    {
        if (SelectedDocument == null || !_dialogService.Confirm($"Archive document {SelectedDocument.DisplayName}? The managed file will be retained.")) return;
        try
        {
            await _documentService.ArchiveDocumentAsync(SelectedDocument.Id);
            await LoadAsync();
            SelectedDocument = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The document could not be archived.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestoreDocument))]
    private async Task RestoreDocument()
    {
        if (SelectedDocument == null || !_dialogService.Confirm($"Restore document {SelectedDocument.DisplayName}?")) return;
        try
        {
            await _documentService.RestoreDocumentAsync(SelectedDocument.Id);
            await LoadAsync();
            SelectedDocument = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The document could not be restored.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditDocument))]
    private async Task EditDocument()
    {
        if (SelectedDocument == null) return;
        if (_dialogService.ShowDialog(new DocumentMetadataEditViewModel(SelectedDocument, _documentService)) == true)
            await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanEditDocument))]
    private void LinkDocument()
    {
        if (SelectedDocument == null) return;
        _dialogService.ShowDialog(new DocumentLinkEditViewModel(SelectedDocument, _documentService));
    }

    [RelayCommand]
    private async Task CheckMissingFiles()
    {
        await _documentService.CheckMissingFilesAsync();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    private bool CanViewDocument => SelectedDocument != null;
    private bool CanEditDocument => SelectedDocument != null;
    private bool CanDeleteDocument => SelectedDocument != null;
    private bool CanRestoreDocument => SelectedDocument?.Status == Core.Enums.DocumentStatus.Archived;

    partial void OnShowArchivedChanged(bool value) => LoadAsync().ConfigureAwait(false);

    partial void OnSelectedDocumentChanged(Document? value)
    {
        ViewDocumentCommand.NotifyCanExecuteChanged();
        EditDocumentCommand.NotifyCanExecuteChanged();
        LinkDocumentCommand.NotifyCanExecuteChanged();
        DeleteDocumentCommand.NotifyCanExecuteChanged();
        RestoreDocumentCommand.NotifyCanExecuteChanged();
    }
}
