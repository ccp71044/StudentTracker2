using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StudentTracker.Core.Models;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public sealed record ReportItem(string Key, string Name, string Category);
public sealed record ReportCategory(string Name, IEnumerable<ReportItem> Items);

public partial class ReportsViewModel : ViewModelBase
{
    private readonly ReportService _reportService;
    private readonly InvoicerReferenceExportService _referenceExportService;

    [ObservableProperty]
    private ObservableCollection<Allocation> _completedStudents = new();

    [ObservableProperty]
    private ObservableCollection<AwaitingOrderReportItem> _awaitingOrder = new();

    [ObservableProperty]
    private ObservableCollection<Allocation> _withdrawnStudents = new();

    [ObservableProperty]
    private ObservableCollection<Allocation> _nonCompletions = new();

    [ObservableProperty]
    private ObservableCollection<Allocation> _awaitingDelivery = new();

    [ObservableProperty]
    private ObservableCollection<Allocation> _certificatesDelivered = new();

    [ObservableProperty]
    private ObservableCollection<DeliveryReportItem> _upcomingDeliveries = new();

    [ObservableProperty]
    private ObservableCollection<DeliveryReportItem> _cancelledDeliveries = new();

    [ObservableProperty]
    private ObservableCollection<DeliveryReportItem> _completedDeliveries = new();

    [ObservableProperty]
    private ObservableCollection<DeliveryReportItem> _capacityReport = new();

    [ObservableProperty]
    private ObservableCollection<AllocationReportItem> _activeAllocations = new();

    [ObservableProperty]
    private ObservableCollection<AllocationReportItem> _transferredAllocations = new();

    [ObservableProperty]
    private ObservableCollection<AllocationReportItem> _cancelledAllocations = new();

    [ObservableProperty]
    private ObservableCollection<AllocationReportItem> _placeholderAllocations = new();

    [ObservableProperty]
    private ObservableCollection<AllocationReportItem> _attendance = new();

    [ObservableProperty]
    private ObservableCollection<CourseUtilizationReportItem> _courseUtilization = new();

    [ObservableProperty]
    private ObservableCollection<BudgetTransactionSummaryItem> _budgetSummary = new();

    [ObservableProperty]
    private ObservableCollection<BudgetTransactionHistoryItem> _budgetHistory = new();

    [ObservableProperty]
    private ObservableCollection<CreditTransactionSummaryItem> _creditSummary = new();

    [ObservableProperty]
    private ObservableCollection<CreditTransactionHistoryItem> _creditHistory = new();

    [ObservableProperty]
    private ObservableCollection<AuditLogReportItem> _auditActivity = new();

    [ObservableProperty]
    private ObservableCollection<ImportReviewQueueReportItem> _importReviewQueue = new();

    [ObservableProperty]
    private ObservableCollection<CertificateOrderReportItem> _certificateOrders = new();

    [ObservableProperty]
    private ObservableCollection<PrepaidPositionReportItem> _prepaidPosition = new();

    [ObservableProperty]
    private bool _includeCostsInWithdrawn = true;

    [ObservableProperty]
    private bool _replacementsOnly;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private bool _includeArchived;

    [ObservableProperty]
    private ObservableCollection<ReportItem> _reports = new();

    [ObservableProperty]
    private ReportItem? _selectedReport;

    public ReportsViewModel(ReportService reportService, InvoicerReferenceExportService referenceExportService)
    {
        _reportService = reportService;
        _referenceExportService = referenceExportService;
        InitializeReports();
        LoadAsync().ConfigureAwait(false);
    }

    private void InitializeReports()
    {
        Reports = new ObservableCollection<ReportItem>(new[]
        {
            new ReportItem("CompletedStudents", "Completed Students", "Students"),
            new ReportItem("AwaitingOrder", "Awaiting Certificate Order", "Students"),
            new ReportItem("WithdrawnStudents", "Withdrawn Students", "Students"),
            new ReportItem("NonCompletions", "Non-Completions", "Students"),
            new ReportItem("AwaitingDelivery", "Awaiting Delivery", "Certificates"),
            new ReportItem("CertificatesDelivered", "Certificates Delivered", "Certificates"),
            new ReportItem("UpcomingDeliveries", "Upcoming Deliveries", "Deliveries"),
            new ReportItem("CancelledDeliveries", "Cancelled Deliveries", "Deliveries"),
            new ReportItem("CompletedDeliveries", "Completed Deliveries", "Deliveries"),
            new ReportItem("CapacityReport", "Capacity", "Deliveries"),
            new ReportItem("ActiveAllocations", "Active Allocations", "Allocations"),
            new ReportItem("TransferredAllocations", "Transferred Allocations", "Allocations"),
            new ReportItem("CancelledAllocations", "Cancelled Allocations", "Allocations"),
            new ReportItem("PlaceholderAllocations", "Placeholder Allocations", "Allocations"),
            new ReportItem("Attendance", "Attendance", "Allocations"),
            new ReportItem("CourseUtilization", "Course Utilization", "Allocations"),
            new ReportItem("BudgetSummary", "Budget Summary", "Financial"),
            new ReportItem("BudgetHistory", "Budget History", "Financial"),
            new ReportItem("CreditSummary", "Credit Summary", "Financial"),
            new ReportItem("CreditHistory", "Credit History", "Financial"),
            new ReportItem("PrepaidPosition", "Prepaid Position", "Financial"),
            new ReportItem("AuditActivity", "Audit Activity", "Administration"),
            new ReportItem("ImportReviewQueue", "Import Review Queue", "Administration"),
            new ReportItem("CertificateOrders", "Certificate Orders", "Administration"),
        });

        SelectedReport = Reports.First();
    }

    private async Task LoadAsync()
    {
        CompletedStudents = new ObservableCollection<Allocation>(await _reportService.GetCompletedStudentsAsync(FromDate, ToDate, IncludeArchived));
        AwaitingOrder = new ObservableCollection<AwaitingOrderReportItem>(await _reportService.GetAwaitingOrderReportAsync(IncludeArchived));
        WithdrawnStudents = new ObservableCollection<Allocation>(await _reportService.GetWithdrawnStudentsAsync(IncludeCostsInWithdrawn, FromDate, ToDate, IncludeArchived));
        NonCompletions = new ObservableCollection<Allocation>(await _reportService.GetNonCompletionsAsync(FromDate, ToDate, IncludeArchived));
        AwaitingDelivery = new ObservableCollection<Allocation>(await _reportService.GetCertificatesAwaitingDeliveryAsync(IncludeArchived));
        CertificatesDelivered = new ObservableCollection<Allocation>(await _reportService.GetCertificatesDeliveredAsync(FromDate, ToDate, IncludeArchived));

        UpcomingDeliveries = new ObservableCollection<DeliveryReportItem>(await _reportService.GetUpcomingCourseDeliveriesAsync(FromDate));
        CancelledDeliveries = new ObservableCollection<DeliveryReportItem>(await _reportService.GetCancelledCourseDeliveriesAsync());
        CompletedDeliveries = new ObservableCollection<DeliveryReportItem>(await _reportService.GetCompletedCourseDeliveriesAsync());
        CapacityReport = new ObservableCollection<DeliveryReportItem>(await _reportService.GetCapacityReportAsync());

        ActiveAllocations = new ObservableCollection<AllocationReportItem>(await _reportService.GetActiveAllocationsAsync(IncludeArchived));
        TransferredAllocations = new ObservableCollection<AllocationReportItem>(await _reportService.GetTransferredAllocationsAsync(IncludeArchived));
        CancelledAllocations = new ObservableCollection<AllocationReportItem>(await _reportService.GetCancelledAllocationsAsync(IncludeArchived));
        PlaceholderAllocations = new ObservableCollection<AllocationReportItem>(await _reportService.GetPlaceholderAllocationsAsync(IncludeArchived));
        Attendance = new ObservableCollection<AllocationReportItem>(await _reportService.GetAttendanceReportAsync(IncludeArchived));

        CourseUtilization = new ObservableCollection<CourseUtilizationReportItem>(await _reportService.GetCourseUtilizationReportAsync());

        BudgetSummary = new ObservableCollection<BudgetTransactionSummaryItem>(await _reportService.GetBudgetTransactionSummaryAsync());
        BudgetHistory = new ObservableCollection<BudgetTransactionHistoryItem>(await _reportService.GetBudgetTransactionHistoryAsync(FromDate, ToDate));

        CreditSummary = new ObservableCollection<CreditTransactionSummaryItem>(await _reportService.GetCreditTransactionSummaryAsync());
        CreditHistory = new ObservableCollection<CreditTransactionHistoryItem>(await _reportService.GetCreditTransactionHistoryAsync(FromDate, ToDate));

        AuditActivity = new ObservableCollection<AuditLogReportItem>(await _reportService.GetAuditActivityReportAsync(FromDate, ToDate));
        ImportReviewQueue = new ObservableCollection<ImportReviewQueueReportItem>(await _reportService.GetImportReviewQueueReportAsync());
        CertificateOrders = new ObservableCollection<CertificateOrderReportItem>(await _reportService.GetCertificateOrderReportAsync(ReplacementsOnly ? true : null));

        await LoadPrepaidPositionAsync();
    }

    private async Task LoadPrepaidPositionAsync()
    {
        var snapshot = await _referenceExportService.BuildSnapshotAsync("Prepaid position report");
        var rows = new List<PrepaidPositionReportItem>();

        foreach (var p in snapshot.Pools)
        {
            if (p.Courses.Count == 0)
            {
                var unbilled = await _reportService.GetUnbilledCountAsync(p.PoolId);
                rows.Add(new PrepaidPositionReportItem
                {
                    PoolDisplayId = p.PoolDisplayId ?? p.PoolName,
                    PoolName = p.PoolName,
                    FinancialPeriod = p.FinancialPeriod,
                    FundsAdded = p.FundsAdded,
                    Committed = p.Committed,
                    Spent = p.Spent,
                    Available = p.Available,
                    ReservedPlaces = p.AnonymousReservedPlaces,
                    AssignedPending = p.AssignedPending,
                    CompletedAwaitingSpend = p.CompletedAwaitingManualSpend,
                    CompletionsRemaining = p.CompletionsRemaining,
                    TotalAllocations = 0,
                    BillableUnexported = unbilled,
                    AllenCost = null
                });
            }
            else
            {
                foreach (var c in p.Courses)
                {
                    var unbilled = await _reportService.GetUnbilledCountAsync(p.PoolId, c.CourseId);
                    rows.Add(new PrepaidPositionReportItem
                    {
                        PoolDisplayId = p.PoolDisplayId ?? p.PoolName,
                        PoolName = p.PoolName,
                        FinancialPeriod = p.FinancialPeriod,
                        CourseCode = c.CourseCode,
                        CourseTitle = c.CourseTitle,
                        Provider = c.Provider,
                        FundsAdded = p.FundsAdded,
                        Committed = c.Committed,
                        Spent = c.Spent,
                        Available = c.Available,
                        ReservedPlaces = c.AnonymousReservedPlaces,
                        AssignedPending = c.AssignedPending,
                        CompletedAwaitingSpend = c.CompletedAwaitingManualSpend,
                        CompletionsRemaining = c.CompletionsRemaining,
                        TotalAllocations = c.TotalAllocations,
                        BillableUnexported = unbilled,
                        AllenCost = c.ProviderCost
                    });
                }
            }
        }

        PrepaidPosition = new ObservableCollection<PrepaidPositionReportItem>(rows);
    }

    [RelayCommand]
    private async Task ExportCompletedCsv()
    {
        await ExportAsync("completed-students.csv", CompletedStudents.ToList());
    }

    [RelayCommand]
    private async Task ExportAwaitingOrderCsv()
    {
        await ExportAsync("awaiting-order.csv", AwaitingOrder.ToList());
    }

    [RelayCommand]
    private async Task ExportWithdrawnCsv()
    {
        await ExportAsync("withdrawn-students.csv", WithdrawnStudents.ToList());
    }

    [RelayCommand]
    private async Task ExportNonCompletionsCsv()
    {
        await ExportAsync("non-completions.csv", NonCompletions.ToList());
    }

    [RelayCommand]
    private async Task ExportAwaitingDeliveryCsv()
    {
        await ExportAsync("awaiting-delivery.csv", AwaitingDelivery.ToList());
    }

    [RelayCommand]
    private async Task ExportDeliveredCsv()
    {
        await ExportAsync("certificates-delivered.csv", CertificatesDelivered.ToList());
    }

    [RelayCommand]
    private async Task ExportUpcomingDeliveriesCsv()
    {
        await ExportAsync("upcoming-deliveries.csv", UpcomingDeliveries.ToList());
    }

    [RelayCommand]
    private async Task ExportCancelledDeliveriesCsv()
    {
        await ExportAsync("cancelled-deliveries.csv", CancelledDeliveries.ToList());
    }

    [RelayCommand]
    private async Task ExportCompletedDeliveriesCsv()
    {
        await ExportAsync("completed-deliveries.csv", CompletedDeliveries.ToList());
    }

    [RelayCommand]
    private async Task ExportCapacityCsv()
    {
        await ExportAsync("capacity-report.csv", CapacityReport.ToList());
    }

    [RelayCommand]
    private async Task ExportActiveAllocationsCsv()
    {
        await ExportAsync("active-allocations.csv", ActiveAllocations.ToList());
    }

    [RelayCommand]
    private async Task ExportTransferredAllocationsCsv()
    {
        await ExportAsync("transferred-allocations.csv", TransferredAllocations.ToList());
    }

    [RelayCommand]
    private async Task ExportCancelledAllocationsCsv()
    {
        await ExportAsync("cancelled-allocations.csv", CancelledAllocations.ToList());
    }

    [RelayCommand]
    private async Task ExportPlaceholderAllocationsCsv()
    {
        await ExportAsync("placeholder-allocations.csv", PlaceholderAllocations.ToList());
    }

    [RelayCommand]
    private async Task ExportAttendanceCsv()
    {
        await ExportAsync("attendance.csv", Attendance.ToList());
    }

    [RelayCommand]
    private async Task ExportCourseUtilizationCsv()
    {
        await ExportAsync("course-utilization.csv", CourseUtilization.ToList());
    }

    [RelayCommand]
    private async Task ExportBudgetSummaryCsv()
    {
        await ExportAsync("budget-summary.csv", BudgetSummary.ToList());
    }

    [RelayCommand]
    private async Task ExportBudgetHistoryCsv()
    {
        await ExportAsync("budget-history.csv", BudgetHistory.ToList());
    }

    [RelayCommand]
    private async Task ExportCreditSummaryCsv()
    {
        await ExportAsync("credit-summary.csv", CreditSummary.ToList());
    }

    [RelayCommand]
    private async Task ExportCreditHistoryCsv()
    {
        await ExportAsync("credit-history.csv", CreditHistory.ToList());
    }

    [RelayCommand]
    private async Task ExportPrepaidPositionCsv()
    {
        await ExportAsync("prepaid-position.csv", PrepaidPosition.ToList());
    }

    [RelayCommand]
    private async Task ExportAuditActivityCsv()
    {
        await ExportAsync("audit-activity.csv", AuditActivity.ToList());
    }

    [RelayCommand]
    private async Task ExportImportReviewQueueCsv()
    {
        await ExportAsync("import-review-queue.csv", ImportReviewQueue.ToList());
    }

    [RelayCommand]
    private async Task ExportCertificateOrdersCsv()
    {
        await ExportAsync("certificate-orders.csv", CertificateOrders.ToList());
    }

    [RelayCommand]
    private async Task ExportInvoiceManagerCostPosition()
    {
        var result = await _referenceExportService.ExportCostPositionSnapshotAsync("Manual export from Reports");
        MessageBox.Show(
            $"Invoice Manager cost position snapshot exported.\nPools: {result.PoolCount}\nCourses: {result.CourseCount}\nJSON: {Path.GetFileName(result.JsonPath)}\nCSV: {Path.GetFileName(result.CsvPath)}",
            "Export Complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    private async Task ExportAsync<T>(string fileName, List<T> records) where T : class
    {
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = fileName };
        if (dialog.ShowDialog() == true)
        {
            var bytes = await _reportService.ExportCsvAsync(records);
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
        }
    }

    partial void OnIncludeCostsInWithdrawnChanged(bool value) => LoadAsync().ConfigureAwait(false);
    partial void OnIncludeArchivedChanged(bool value) => LoadAsync().ConfigureAwait(false);
    partial void OnReplacementsOnlyChanged(bool value) => LoadAsync().ConfigureAwait(false);
}
