using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StudentTracker.Core.Models;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly ReportService _reportService;

    [ObservableProperty]
    private ObservableCollection<Allocation> _completedStudents = new();

    [ObservableProperty]
    private ObservableCollection<Allocation> _awaitingOrder = new();

    [ObservableProperty]
    private ObservableCollection<Allocation> _withdrawnStudents = new();

    [ObservableProperty]
    private ObservableCollection<Allocation> _nonCompletions = new();

    [ObservableProperty]
    private ObservableCollection<Allocation> _awaitingDelivery = new();

    [ObservableProperty]
    private ObservableCollection<Allocation> _certificatesDelivered = new();

    [ObservableProperty]
    private bool _includeCostsInWithdrawn = true;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private bool _includeArchived;

    public ReportsViewModel(ReportService reportService)
    {
        _reportService = reportService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        CompletedStudents = new ObservableCollection<Allocation>(await _reportService.GetCompletedStudentsAsync(FromDate, ToDate, IncludeArchived));
        AwaitingOrder = new ObservableCollection<Allocation>(await _reportService.GetCertificatesAwaitingOrderAsync(IncludeArchived));
        WithdrawnStudents = new ObservableCollection<Allocation>(await _reportService.GetWithdrawnStudentsAsync(IncludeCostsInWithdrawn, FromDate, ToDate, IncludeArchived));
        NonCompletions = new ObservableCollection<Allocation>(await _reportService.GetNonCompletionsAsync(FromDate, ToDate, IncludeArchived));
        AwaitingDelivery = new ObservableCollection<Allocation>(await _reportService.GetCertificatesAwaitingDeliveryAsync(IncludeArchived));
        CertificatesDelivered = new ObservableCollection<Allocation>(await _reportService.GetCertificatesDeliveredAsync(FromDate, ToDate, IncludeArchived));
    }

    [RelayCommand]
    private async Task ExportCompletedCsv()
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "completed-students.csv" };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(CompletedStudents.ToList());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
        }
    }

    [RelayCommand]
    private async Task ExportWithdrawnCsv()
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "withdrawn-students.csv" };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(WithdrawnStudents.ToList());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
        }
    }

    [RelayCommand]
    private async Task ExportNonCompletionsCsv()
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "non-completions.csv" };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(NonCompletions.ToList());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
        }
    }

    [RelayCommand]
    private async Task ExportAwaitingDeliveryCsv()
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "awaiting-delivery.csv" };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(AwaitingDelivery.ToList());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
        }
    }

    [RelayCommand]
    private async Task ExportDeliveredCsv()
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "certificates-delivered.csv" };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(CertificatesDelivered.ToList());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    partial void OnIncludeCostsInWithdrawnChanged(bool value) => LoadAsync().ConfigureAwait(false);
    partial void OnIncludeArchivedChanged(bool value) => LoadAsync().ConfigureAwait(false);
}
