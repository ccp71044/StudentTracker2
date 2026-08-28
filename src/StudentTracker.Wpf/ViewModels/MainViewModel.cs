using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentViewModel;

    [ObservableProperty]
    private string _title = "Student Tracker";

    public DashboardViewModel DashboardViewModel { get; }
    public StudentsViewModel StudentsViewModel { get; }
    public CoursesViewModel CoursesViewModel { get; }
    public DeliveriesViewModel DeliveriesViewModel { get; }
    public AllocationsViewModel AllocationsViewModel { get; }
    public CertificatesViewModel CertificatesViewModel { get; }
    public CreditsBudgetsViewModel CreditsBudgetsViewModel { get; }
    public DocumentsViewModel DocumentsViewModel { get; }
    public ReportsViewModel ReportsViewModel { get; }
    public ImportExportViewModel ImportExportViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }

    public MainViewModel(DashboardViewModel dashboard, StudentsViewModel students, CoursesViewModel courses, DeliveriesViewModel deliveries, AllocationsViewModel allocations, CertificatesViewModel certificates, CreditsBudgetsViewModel creditsBudgets, DocumentsViewModel documents, ReportsViewModel reports, ImportExportViewModel importExport, SettingsViewModel settings)
    {
        DashboardViewModel = dashboard;
        StudentsViewModel = students;
        CoursesViewModel = courses;
        DeliveriesViewModel = deliveries;
        AllocationsViewModel = allocations;
        CertificatesViewModel = certificates;
        CreditsBudgetsViewModel = creditsBudgets;
        DocumentsViewModel = documents;
        ReportsViewModel = reports;
        ImportExportViewModel = importExport;
        SettingsViewModel = settings;
        _currentViewModel = dashboard;
    }

    /// <summary>
    /// Shows a section and loads its data on first use. Sections load one at a time because they
    /// share a single database context, which cannot serve concurrent queries.
    /// </summary>
    private async Task NavigateAsync(ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel;
        await viewModel.EnsureInitialisedAsync();
    }

    protected override Task InitialiseAsync() => DashboardViewModel.EnsureInitialisedAsync();

    [RelayCommand]
    private Task ShowDashboard() => NavigateAsync(DashboardViewModel);

    [RelayCommand]
    private Task ShowStudents() => NavigateAsync(StudentsViewModel);

    [RelayCommand]
    private Task ShowCourses() => NavigateAsync(CoursesViewModel);

    [RelayCommand]
    private Task ShowDeliveries() => NavigateAsync(DeliveriesViewModel);

    [RelayCommand]
    private Task ShowAllocations() => NavigateAsync(AllocationsViewModel);

    [RelayCommand]
    private Task ShowCertificates() => NavigateAsync(CertificatesViewModel);

    [RelayCommand]
    private Task ShowCreditsBudgets() => NavigateAsync(CreditsBudgetsViewModel);

    [RelayCommand]
    private Task ShowDocuments() => NavigateAsync(DocumentsViewModel);

    [RelayCommand]
    private Task ShowReports() => NavigateAsync(ReportsViewModel);

    [RelayCommand]
    private Task ShowImportExport() => NavigateAsync(ImportExportViewModel);

    [RelayCommand]
    private Task ShowSettings() => NavigateAsync(SettingsViewModel);
}
