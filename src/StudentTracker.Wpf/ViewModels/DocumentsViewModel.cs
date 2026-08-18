using System.Collections.ObjectModel;
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

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }
}
