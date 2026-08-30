using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using System.IO;
using System.Windows;

namespace StudentTracker.Wpf.ViewModels;

public partial class InvoicerReferenceViewModel : ViewModelBase
{
    private readonly InvoicerReferenceImportService _importService;
    private readonly InvoicerReferenceExportService _exportService;

    [ObservableProperty]
    private ObservableCollection<Invoice> _invoices = new();

    [ObservableProperty]
    private Invoice? _selectedInvoice;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public InvoicerReferenceViewModel(InvoicerReferenceImportService importService, InvoicerReferenceExportService exportService)
    {
        _importService = importService;
        _exportService = exportService;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var list = await _importService.GetLatestInvoicesAsync(50);
        Invoices = new ObservableCollection<Invoice>(list);
        StatusText = $"{Invoices.Count} invoice reference(s) loaded.";
    }

    [RelayCommand]
    private async Task ExportClientPrepaidPositionAsync()
    {
        var result = await _exportService.ExportClientPrepaidPositionSnapshotAsync("Manual export from Invoicer References");
        StatusText = $"Client prepaid position snapshot exported: pools={result.PoolCount}, json={Path.GetFileName(result.JsonPath)}";
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Import Invoicer Reference CSV"
        };

        if (dialog.ShowDialog() != true)
            return;

        var result = await _importService.ImportFromFileAsync(dialog.FileName);
        StatusText = $"Imported {result.ImportedCount}, updated {result.UpdatedCount}, skipped {result.SkippedCount}, errors {result.Errors.Count}.";
        if (result.Errors.Count > 0)
        {
            MessageBox.Show(string.Join("\n", result.Errors.Take(5)), "Import Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        await RefreshAsync();
    }
}
