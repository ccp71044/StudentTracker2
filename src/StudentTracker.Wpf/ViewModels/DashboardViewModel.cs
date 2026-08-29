using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using StudentTracker.Core.Models;
using StudentTracker.Data;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly StudentTrackerDbContext _context;
    private readonly StudentService _studentService;
    private readonly CourseService _courseService;
    private readonly IDialogService _dialogService;
    private readonly BudgetSummaryService _budgetSummary;

    [ObservableProperty]
    private int _studentCount;

    [ObservableProperty]
    private int _courseCount;

    [ObservableProperty]
    private int _deliveryCount;

    [ObservableProperty]
    private int _allocationCount;

    [ObservableProperty]
    private int _pendingCertificateCount;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _reconciliationStatus = string.Empty;

    [ObservableProperty]
    private bool _hasNegativePool;

    public ObservableCollection<PoolSummary> Pools { get; } = new();

    public ObservableCollection<CompletionsRemaining> CompletionsRemaining { get; } = new();

    public DashboardViewModel(StudentTrackerDbContext context, StudentService studentService, CourseService courseService, IDialogService dialogService, BudgetSummaryService budgetSummary)
    {
        _context = context;
        _studentService = studentService;
        _courseService = courseService;
        _dialogService = dialogService;
        _budgetSummary = budgetSummary;
        Refresh().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StudentCount = await _context.Students.CountAsync(s => !s.IsArchived);
        CourseCount = await _context.CourseDefinitions.CountAsync(c => c.IsActive);
        DeliveryCount = await _context.CourseDeliveries.CountAsync();
        AllocationCount = await _context.Allocations.CountAsync();
        PendingCertificateCount = await _context.Allocations
            .CountAsync(a => a.CertificateOrderStatus == Core.Enums.CertificateOrderStatus.Ready || a.CertificateOrderStatus == Core.Enums.CertificateOrderStatus.Ordered);
        Status = $"{StudentCount} students, {DeliveryCount} deliveries";

        Pools.Clear();
        foreach (var pool in await _budgetSummary.GetPoolSummariesAsync())
            Pools.Add(pool);

        HasNegativePool = Pools.Any(p => p.Free < 0);

        CompletionsRemaining.Clear();
        foreach (var course in await _budgetSummary.GetCompletionsRemainingAsync())
            CompletionsRemaining.Add(course);

        var reconciliation = await _budgetSummary.ReconcileTopUpsAsync();
        ReconciliationStatus = reconciliation.IsBalanced
            ? "Register and provider ledger agree."
            : $"{reconciliation.Discrepancies.Count} unreconciled top-up(s); provider ledger differs by {reconciliation.Difference:C}.";
    }

    [RelayCommand]
    private async Task AddStudent()
    {
        var vm = new StudentEditViewModel(new Student { FirstName = "", LastName = "" }, _studentService, isNew: true);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await Refresh();
        }
    }

    [RelayCommand]
    private async Task AddCourse()
    {
        var vm = new CourseEditViewModel(new CourseDefinition(), _courseService, isNew: true);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await Refresh();
        }
    }
}
