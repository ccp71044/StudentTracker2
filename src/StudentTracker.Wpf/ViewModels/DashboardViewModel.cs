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
    private readonly ClientPrepaidEntitlementService _clientPrepaid;

    [ObservableProperty]
    private int _studentCount;

    [ObservableProperty]
    private string _focusedClientName = "Client Prepaid";

    [ObservableProperty]
    private decimal _focusedTotal;

    [ObservableProperty]
    private decimal _focusedUsed;

    [ObservableProperty]
    private decimal _focusedAvailable;

    [ObservableProperty]
    private decimal _focusedReserved;

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

    public DashboardViewModel(StudentTrackerDbContext context, StudentService studentService, CourseService courseService, IDialogService dialogService, BudgetSummaryService budgetSummary, ClientPrepaidEntitlementService clientPrepaid)
    {
        _context = context;
        _studentService = studentService;
        _courseService = courseService;
        _dialogService = dialogService;
        _budgetSummary = budgetSummary;
        _clientPrepaid = clientPrepaid;
        Refresh().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StudentCount = await _context.Students.CountAsync(s => !s.IsArchived);
        AllocationCount = await _context.Allocations.CountAsync();
        PendingCertificateCount = await _context.Allocations
            .CountAsync(a => a.CertificateOrderStatus == Core.Enums.CertificateOrderStatus.Ready || a.CertificateOrderStatus == Core.Enums.CertificateOrderStatus.Ordered);

        var clientPools = await _context.ClientPrepaidPools.ToListAsync();
        var selected = clientPools
            .Where(p => p.Name != null)
            .OrderByDescending(p => p.Name!.Equals("T&C", StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p.Name)
            .FirstOrDefault();

        if (selected != null)
        {
            var position = await _clientPrepaid.GetPoolPositionAsync(selected.Id);
            FocusedClientName = selected.Name ?? "Client Prepaid";
            FocusedTotal = position.PrepaidPlacesLoaded;
            FocusedUsed = position.PlacesConsumed;
            FocusedAvailable = position.UnassignedCarryForward;
            FocusedReserved = position.ReservedToNamedStudents + position.ReservedPlaceholders;
        }
        else
        {
            FocusedClientName = "Client Prepaid";
            FocusedTotal = FocusedUsed = FocusedAvailable = FocusedReserved = 0m;
        }

        Status = $"{StudentCount} students, {AllocationCount} allocations";

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
