using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StudentTracker.Services;
using System.IO;
using System.Windows;

namespace StudentTracker.Wpf.ViewModels;

public partial class ImportExportViewModel : ViewModelBase
{
    private readonly ImportService _importService;
    private readonly BackupService _backupService;
    private readonly InvoicerService _invoicerService;

    [ObservableProperty]
    private string _status = string.Empty;

    public ImportExportViewModel(ImportService importService, BackupService backupService, InvoicerService invoicerService)
    {
        _importService = importService;
        _backupService = backupService;
        _invoicerService = invoicerService;
    }

    [RelayCommand]
    private void CreateBackup()
    {
        var path = _backupService.CreateBackup("manual");
        Status = $"Backup created: {path}";
    }

    [RelayCommand]
    private void RestoreBackup()
    {
        var dialog = new OpenFileDialog { Filter = "Zip files (*.zip)|*.zip" };
        if (dialog.ShowDialog() == true)
        {
            _backupService.RestoreBackup(dialog.FileName);
            Status = "Backup restored.";
        }
    }

    [RelayCommand]
    private async Task ExportInvoicer()
    {
        var billable = await _invoicerService.GetUnexportedBillableAsync();
        if (billable.Count == 0)
        {
            Status = "No billable items to export.";
            return;
        }

        var batch = await _invoicerService.ExportAsync(billable.Select(a => a.Id).ToList());
        Status = $"Invoicer export created: {batch.DisplayId}, items: {batch.ItemCount}";
    }

    [RelayCommand]
    private async Task ImportMigrationPackage()
    {
        var dialog = new OpenFileDialog { Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*" };
        if (dialog.ShowDialog() == true)
        {
            var result = await _importService.ImportMigrationPackageAsync(dialog.FileName);
            Status = result.Message ?? "Import complete.";
        }
    }

    [RelayCommand]
    private Task ImportCompletionPricing() => ImportCsv("CompletionPricing", "provider price list");

    [RelayCommand]
    private Task ImportCreditHistory() => ImportCsv("CreditHistory", "provider credit history");

    private async Task ImportCsv(string entityType, string description)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"Select the {description} CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        await using var stream = File.OpenRead(dialog.FileName);
        var result = await _importService.ImportCsvAsync(entityType, stream);
        Status = result.Message ?? "Import complete.";
    }
}
