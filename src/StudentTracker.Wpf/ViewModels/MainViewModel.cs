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
    private void ShowImportExport() => CurrentViewModel = ImportExportViewModel;

    [RelayCommand]
    private void ShowSettings() => CurrentViewModel = SettingsViewModel;
}
