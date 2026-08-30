using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace StudentTracker.Wpf.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DataLocationService _dataLocation;
    private readonly IDialogService _dialogService;
    private readonly DataBrowserViewModel _dataBrowser;

    [ObservableProperty]
    private ViewModelBase _currentViewModel;

    [ObservableProperty]
    private string _title = "Student Tracker";

    public string Version => AppVersion.Current;

    public DashboardViewModel DashboardViewModel { get; }
    public StudentsViewModel StudentsViewModel { get; }
    public CoursesViewModel CoursesViewModel { get; }
    public DeliveriesViewModel DeliveriesViewModel { get; }
    public AllocationsViewModel AllocationsViewModel { get; }
    public CertificatesViewModel CertificatesViewModel { get; }
    public CreditsBudgetsViewModel CreditsBudgetsViewModel { get; }
    public DocumentsViewModel DocumentsViewModel { get; }
    public ReportsViewModel ReportsViewModel { get; }
    public CompletionsViewModel CompletionsViewModel { get; }
    public PoolPositionViewModel PoolPositionViewModel { get; }
    public InvoicerReferenceViewModel InvoicerReferenceViewModel { get; }
    public ImportExportViewModel ImportExportViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public StudentOverviewViewModel StudentOverviewViewModel { get; }
    public CourseDeliveryOverviewViewModel CourseDeliveryOverviewViewModel { get; }

    public MainViewModel(
        DataLocationService dataLocation,
        IDialogService dialogService,
        DashboardViewModel dashboard,
        StudentsViewModel students,
        CoursesViewModel courses,
        DeliveriesViewModel deliveries,
        AllocationsViewModel allocations,
        CertificatesViewModel certificates,
        CreditsBudgetsViewModel creditsBudgets,
        DocumentsViewModel documents,
        ReportsViewModel reports,
        CompletionsViewModel completions,
        PoolPositionViewModel poolPosition,
        InvoicerReferenceViewModel invoicerReference,
        ImportExportViewModel importExport,
        SettingsViewModel settings,
        DataBrowserViewModel dataBrowser,
        StudentOverviewViewModel studentOverview,
        CourseDeliveryOverviewViewModel courseDeliveryOverview)
    {
        _dataLocation = dataLocation;
        _dialogService = dialogService;
        _dataBrowser = dataBrowser;
        DashboardViewModel = dashboard;
        StudentsViewModel = students;
        CoursesViewModel = courses;
        DeliveriesViewModel = deliveries;
        AllocationsViewModel = allocations;
        CertificatesViewModel = certificates;
        CreditsBudgetsViewModel = creditsBudgets;
        DocumentsViewModel = documents;
        ReportsViewModel = reports;
        CompletionsViewModel = completions;
        PoolPositionViewModel = poolPosition;
        InvoicerReferenceViewModel = invoicerReference;
        ImportExportViewModel = importExport;
        SettingsViewModel = settings;
        StudentOverviewViewModel = studentOverview;
        CourseDeliveryOverviewViewModel = courseDeliveryOverview;
        _currentViewModel = dashboard;
    }

    [RelayCommand]
    private void ShowDashboard() => CurrentViewModel = DashboardViewModel;

    [RelayCommand]
    private void ShowStudents() => CurrentViewModel = StudentsViewModel;

    [RelayCommand]
    private void ShowCourses() => CurrentViewModel = CoursesViewModel;

    [RelayCommand]
    private void ShowDeliveries() => CurrentViewModel = DeliveriesViewModel;

    [RelayCommand]
    private void ShowAllocations() => CurrentViewModel = AllocationsViewModel;

    [RelayCommand]
    private void ShowCertificates() => CurrentViewModel = CertificatesViewModel;

    [RelayCommand]
    private void ShowCreditsBudgets() => CurrentViewModel = CreditsBudgetsViewModel;

    [RelayCommand]
    private void ShowDocuments() => CurrentViewModel = DocumentsViewModel;

    [RelayCommand]
    private void ShowReports() => CurrentViewModel = ReportsViewModel;

    [RelayCommand]
    private void ShowCompletions() => CurrentViewModel = CompletionsViewModel;

    [RelayCommand]
    private void ShowPoolPosition() => CurrentViewModel = PoolPositionViewModel;

    [RelayCommand]
    private void ShowInvoicerReferences() => CurrentViewModel = InvoicerReferenceViewModel;

    [RelayCommand]
    private void ShowImportExport() => CurrentViewModel = ImportExportViewModel;

    [RelayCommand]
    private void ShowSettings() => CurrentViewModel = SettingsViewModel;

    [RelayCommand]
    private void ShowStudentOverview() => CurrentViewModel = StudentOverviewViewModel;

    [RelayCommand]
    private void ShowCourseDeliveryOverview() => CurrentViewModel = CourseDeliveryOverviewViewModel;

    [RelayCommand]
    private void ShowDataBrowser() => _dialogService.ShowDialog(_dataBrowser);

    [RelayCommand]
    private void ExitApplication()
    {
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private void BackupNow()
    {
        ImportExportViewModel.CreateBackup();
    }

    [RelayCommand]
    private void RestoreBackup()
    {
        ImportExportViewModel.RestoreBackup();
    }

    [RelayCommand]
    private async Task ImportMigrationPackage()
    {
        await ImportExportViewModel.ImportMigrationPackage();
    }

    [RelayCommand]
    private async Task ReplaceAllData()
    {
        CurrentViewModel = ImportExportViewModel;
        await ImportExportViewModel.ReplaceAllData();
    }

    [RelayCommand]
    private void CompactDatabase()
    {
        SettingsViewModel.CompactDatabase();
    }

    [RelayCommand]
    private async Task RefreshCurrentView()
    {
        if (CurrentViewModel is null)
            return;

        // Several view models expose a RefreshCommand. Invoke it via reflection so we do not
        // need to couple the main menu to every child view model interface.
        var refreshCommandProperty = CurrentViewModel.GetType().GetProperty("RefreshCommand");
        if (refreshCommandProperty?.GetValue(CurrentViewModel) is ICommand refreshCommand && refreshCommand.CanExecute(null))
        {
            refreshCommand.Execute(null);
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenDataFolder() => OpenFolder(_dataLocation.DataRoot);

    [RelayCommand]
    private void OpenBackupsFolder() => OpenFolder(_dataLocation.BackupsPath);

    [RelayCommand]
    private void OpenExportsFolder() => OpenFolder(_dataLocation.ExportsPath);

    [RelayCommand]
    private void OpenLogsFolder() => OpenFolder(_dataLocation.LogsPath);

    [RelayCommand]
    private void OpenDocumentation()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "docs"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs"),
            Path.Combine(AppContext.BaseDirectory, "..", "docs")
        };

        var docsPath = candidates
            .Select(p => Path.GetFullPath(p))
            .FirstOrDefault(Directory.Exists);

        if (!string.IsNullOrEmpty(docsPath))
        {
            OpenFolder(docsPath);
        }
        else
        {
            MessageBox.Show(
                "The documentation folder could not be found.",
                "Student Tracker",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void ShowAbout()
    {
        MessageBox.Show(
            $"Student Tracker{Environment.NewLine}Version {Version}{Environment.NewLine}{Environment.NewLine}A desktop application for managing students, courses, deliveries and certificates.",
            "About Student Tracker",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open folder:{Environment.NewLine}{ex.Message}",
                "Student Tracker",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
