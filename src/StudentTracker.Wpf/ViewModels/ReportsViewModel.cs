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

    public ReportsViewModel(ReportService reportService)
    {
        _reportService = reportService;
    }

    protected override async Task InitialiseAsync()
    {
        CompletedStudents = new ObservableCollection<Allocation>(await _reportService.GetCompletedStudentsAsync());
        AwaitingOrder = new ObservableCollection<Allocation>(await _reportService.GetCertificatesAwaitingOrderAsync());
    }

    [RelayCommand]
    private Task ExportCompletedCsv() => GuardAsync("ExportCompletedCsv", async () =>
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "completed-students.csv" };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(CompletedStudents.ToList());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
        }
    });

    [RelayCommand]
    private Task Refresh() => GuardAsync("Refresh", InitialiseAsync);
}
