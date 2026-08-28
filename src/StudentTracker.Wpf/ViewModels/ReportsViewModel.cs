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

    public ReportsViewModel(ReportService reportService)
    {
        _reportService = reportService;
    }

    protected override async Task InitialiseAsync()
    {
        CompletedStudents = new ObservableCollection<Allocation>(await _reportService.GetCompletedStudentsAsync());
        AwaitingOrder = new ObservableCollection<Allocation>(await _reportService.GetCertificatesAwaitingOrderAsync());
        WithdrawnStudents = new ObservableCollection<Allocation>(await _reportService.GetWithdrawnStudentsAsync(IncludeCostsInWithdrawn));
        NonCompletions = new ObservableCollection<Allocation>(await _reportService.GetNonCompletionsAsync());
        AwaitingDelivery = new ObservableCollection<Allocation>(await _reportService.GetCertificatesAwaitingDeliveryAsync());
        CertificatesDelivered = new ObservableCollection<Allocation>(await _reportService.GetCertificatesDeliveredAsync());
    }

    [RelayCommand]
    private Task ExportCompletedCsv() => ExportCsv("ExportCompletedCsv", "completed-students.csv", CompletedStudents);

    [RelayCommand]
    private Task ExportWithdrawnCsv() => ExportCsv("ExportWithdrawnCsv", "withdrawn-students.csv", WithdrawnStudents);

    [RelayCommand]
    private Task ExportNonCompletionsCsv() => ExportCsv("ExportNonCompletionsCsv", "non-completions.csv", NonCompletions);

    [RelayCommand]
    private Task ExportAwaitingDeliveryCsv() => ExportCsv("ExportAwaitingDeliveryCsv", "awaiting-delivery.csv", AwaitingDelivery);

    [RelayCommand]
    private Task ExportDeliveredCsv() => ExportCsv("ExportDeliveredCsv", "certificates-delivered.csv", CertificatesDelivered);

    [RelayCommand]
    private Task Refresh() => GuardAsync("Refresh", InitialiseAsync);

    private Task ExportCsv(string operation, string fileName, IEnumerable<Allocation> rows) => GuardAsync(operation, async () =>
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = fileName };
        if (dialog.ShowDialog() != true) return;

        var bytes = await _reportService.ExportCsvAsync(rows.ToList());
        await File.WriteAllBytesAsync(dialog.FileName, bytes);
    });

    partial void OnIncludeCostsInWithdrawnChanged(bool value) => RefreshCommand.Execute(null);
}
