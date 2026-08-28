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
    }

    protected override async Task InitialiseAsync()
    {
        Documents = new ObservableCollection<Document>(await _documentService.GetDocumentsForEntityAsync("All", Guid.Empty));
    }

    [RelayCommand]
    private Task AddDocument() => GuardAsync("AddDocument", async () =>
    {
        var dialog = new OpenFileDialog { Multiselect = false };
        if (dialog.ShowDialog() == true)
        {
            await _documentService.AddDocumentAsync(dialog.FileName, "General");
            await InitialiseAsync();
        }
    });

    [RelayCommand(CanExecute = nameof(CanViewDocument))]
    private void ViewDocument() => Guard("ViewDocument", () =>
    {
        if (SelectedDocument == null) return;

        var filePath = _documentService.GetFullPath(SelectedDocument);
        if (!File.Exists(filePath))
        {
            ErrorMessage = $"The file is missing from the document store: {filePath}";
            return;
        }

        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    });

    [RelayCommand(CanExecute = nameof(CanDeleteDocument))]
    private Task DeleteDocument() => GuardAsync("DeleteDocument", async () =>
    {
        if (SelectedDocument == null) return;

        await _documentService.DeleteDocumentAsync(SelectedDocument.Id);
        await InitialiseAsync();
        SelectedDocument = null;
    });

    [RelayCommand]
    private Task Refresh() => GuardAsync("Refresh", InitialiseAsync);

    private bool CanViewDocument => SelectedDocument != null;
    private bool CanDeleteDocument => SelectedDocument != null;

    partial void OnSelectedDocumentChanged(Document? value)
    {
        ViewDocumentCommand.NotifyCanExecuteChanged();
        DeleteDocumentCommand.NotifyCanExecuteChanged();
    }
}
