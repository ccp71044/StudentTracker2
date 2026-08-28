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

    [RelayCommand]
    private Task Refresh() => GuardAsync("Refresh", InitialiseAsync);
}
