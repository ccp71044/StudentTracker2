using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StudentTracker.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace StudentTracker.Wpf.ViewModels;

public partial class ImportExportViewModel : ViewModelBase
{
    private readonly ImportService _importService;
    private readonly BackupService _backupService;
    private readonly InvoicerService _invoicerService;
    private readonly InvoicerReferenceExportService _referenceExportService;
    private readonly CourseService _courseService;
    private readonly StudentService _studentService;
    private readonly AllocationService _allocationService;
    private readonly ReportService _reportService;
    private readonly DataCutoverService _cutoverService;

    [ObservableProperty]
    private string _status = string.Empty;

    public ImportExportViewModel(ImportService importService, BackupService backupService, InvoicerService invoicerService, InvoicerReferenceExportService referenceExportService, CourseService courseService, StudentService studentService, AllocationService allocationService, ReportService reportService, DataCutoverService cutoverService)
    {
        _importService = importService;
        _backupService = backupService;
        _invoicerService = invoicerService;
        _referenceExportService = referenceExportService;
        _courseService = courseService;
        _studentService = studentService;
        _allocationService = allocationService;
        _reportService = reportService;
        _cutoverService = cutoverService;
    }

    [RelayCommand]
    public void CreateBackup()
    {
        var path = _backupService.CreateBackup("manual");
        Status = $"Backup created: {path}";
    }

    [RelayCommand]
    public void RestoreBackup()
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
    private async Task ExportInvoiceManagerCostPosition()
    {
        var result = await _referenceExportService.ExportCostPositionSnapshotAsync("Manual export from Import/Export");
        Status = $"Invoice Manager cost position snapshot exported: pools={result.PoolCount}, courses={result.CourseCount}, json={Path.GetFileName(result.JsonPath)}";
    }

    [RelayCommand]
    public async Task ImportMigrationPackage()
    {
        var dialog = new OpenFileDialog { Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*" };
        if (dialog.ShowDialog() == true)
        {
            var result = await _importService.ImportMigrationPackageAsync(dialog.FileName);
            Status = result.Message ?? "Import complete.";
        }
    }

    [RelayCommand]
    public async Task ReplaceAllData()
    {
        var picker = new OpenFileDialog { Title = "Select canonical migration package", Filter = "Excel files (*.xlsx)|*.xlsx" };
        if (picker.ShowDialog() != true) return;

        var preview = await _cutoverService.PreviewAsync(picker.FileName);
        if (!preview.IsValid)
        {
            Status = "Validation failed; no changes made.\n" + string.Join("\n", preview.Errors);
            MessageBox.Show(Status, "Replace All Data from Migration Package", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var db = preview.DatabaseCounts;
        var wb = preview.WorkbookCounts;
        var text = new TextBox { Margin = new Thickness(0, 8, 0, 8), MinWidth = 300 };
        var ok = new Button { Content = "Replace All Data", IsDefault = true, Margin = new Thickness(0, 8, 8, 0), Padding = new Thickness(12, 5, 12, 5) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(12, 5, 12, 5) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
            $"DESTRUCTIVE REPLACEMENT\n\nCurrent database: {db.Students} students, {db.Courses} courses, {db.Deliveries} deliveries, {db.Allocations} allocations, {db.BudgetPools} budget pools, {db.CreditPools} credit pools.\n\nWorkbook: {wb.Students} students, {wb.Courses} courses, {wb.Deliveries} deliveries, {wb.Allocations} allocations, {wb.BudgetPools} budget pools, {wb.CreditPools} credit pools.\n\nAll operational and test records (including document metadata, but never document files) will be replaced. App settings and EF migration history are preserved. A verified backup is created first.\n\nType {DataCutoverService.ConfirmationPhrase} exactly to continue:" });
        panel.Children.Add(text); panel.Children.Add(buttons);
        var window = new Window { Title = "Replace All Data from Migration Package", Content = panel, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow, ResizeMode = ResizeMode.NoResize };
        ok.Click += (_, _) => { window.DialogResult = true; window.Close(); };
        if (window.ShowDialog() != true) { Status = "Data replacement cancelled; no changes made."; return; }

        Status = "Creating verified backup and replacing data...";
        var result = await _cutoverService.ExecuteAsync(preview, text.Text);
        Status = result.Message ?? "Data replacement failed.";
        MessageBox.Show(Status + (result.Success ? $"\n\nPre-cutover backup: {result.PreCutoverBackup}\nPost-import backup: {result.PostCutoverBackup}" : ""),
            "Replace All Data from Migration Package", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    [RelayCommand]
    private async Task ExportCourseDefinitions()
    {
        var courses = await _courseService.GetDefinitionsAsync();
        if (courses.Count == 0)
        {
            Status = "No course definitions to export.";
            return;
        }

        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "course-definitions.csv" };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(courses);
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            Status = $"Exported {courses.Count} course definitions.";
        }
    }

    [RelayCommand]
    private async Task ExportStudents()
    {
        var students = await _studentService.SearchAsync(string.Empty);
        if (students.Count == 0)
        {
            Status = "No students to export.";
            return;
        }

        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "students.csv" };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(students);
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            Status = $"Exported {students.Count} students.";
        }
    }

    [RelayCommand]
    private async Task ExportAllocations()
    {
        var allocations = await _allocationService.GetAllocationsAsync();
        if (allocations.Count == 0)
        {
            Status = "No allocations to export.";
            return;
        }

        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "allocations.csv" };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(allocations);
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            Status = $"Exported {allocations.Count} allocations.";
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
