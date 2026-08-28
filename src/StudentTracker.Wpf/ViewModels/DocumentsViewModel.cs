using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StudentTracker.Core.Models;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DocumentsViewModel : ViewModelBase
{
    private readonly DocumentService _documentService;

    [ObservableProperty]
    private ObservableCollection<Document> _documents = new();

    [ObservableProperty]
    private Document? _selectedDocument;

    public DocumentsViewModel(DocumentService documentService)
    {
        _documentService = documentService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        Documents = new ObservableCollection<Document>(await _documentService.GetDocumentsForEntityAsync("All", Guid.Empty));
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
                // Show error - file not found
            }
        }
        catch
        {
            // Show error - could not open file
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteDocument))]
    private async Task DeleteDocument()
    {
        if (SelectedDocument == null) return;
        
        // Confirm deletion
        await _documentService.DeleteDocumentAsync(SelectedDocument.Id);
        await LoadAsync();
        SelectedDocument = null;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    private bool CanViewDocument => SelectedDocument != null;
    private bool CanDeleteDocument => SelectedDocument != null;

    partial void OnSelectedDocumentChanged(Document? value)
    {
        ViewDocumentCommand.NotifyCanExecuteChanged();
        DeleteDocumentCommand.NotifyCanExecuteChanged();
    }
}
