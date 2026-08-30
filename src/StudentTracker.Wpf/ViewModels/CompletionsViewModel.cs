using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StudentTracker.Core.Models;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CompletionsViewModel : ViewModelBase
{
    private readonly BudgetSummaryService _budgetSummary;
    private readonly PricingService _pricing;
    private readonly CourseService _courseService;

    [ObservableProperty]
    private ObservableCollection<PoolSummary> _budgetPools = new();

    [ObservableProperty]
    private PoolSummary? _selectedBudgetPool;

    [ObservableProperty]
    private ObservableCollection<CourseDefinition> _courses = new();

    [ObservableProperty]
    private CourseDefinition? _selectedCourse;

    [ObservableProperty]
    private decimal _fundsAdded;

    [ObservableProperty]
    private decimal _committed;

    [ObservableProperty]
    private decimal _spent;

    [ObservableProperty]
    private decimal _available;

    [ObservableProperty]
    private decimal? _costPerStudent;

    [ObservableProperty]
    private int _completionsAvailable;

    [ObservableProperty]
    private string _statusText = "Select a budget and a unit.";

    public CompletionsViewModel(BudgetSummaryService budgetSummary, PricingService pricing, CourseService courseService)
    {
        _budgetSummary = budgetSummary;
        _pricing = pricing;
        _courseService = courseService;
        _ = LoadAsync();
    }

    partial void OnSelectedBudgetPoolChanged(PoolSummary? value) => _ = RecalculateAsync();
    partial void OnSelectedCourseChanged(CourseDefinition? value) => _ = RecalculateAsync();

    private async Task LoadAsync()
    {
        var summaries = await _budgetSummary.GetPoolSummariesAsync();
        BudgetPools = new ObservableCollection<PoolSummary>(summaries);

        var courses = await _courseService.GetDefinitionsAsync();
        Courses = new ObservableCollection<CourseDefinition>(courses);

        SelectedBudgetPool = BudgetPools.FirstOrDefault();
        SelectedCourse = Courses.FirstOrDefault();
    }

    private async Task RecalculateAsync()
    {
        if (SelectedBudgetPool == null || SelectedCourse == null)
        {
            StatusText = "Select a budget and a unit.";
            return;
        }

        var summaries = await _budgetSummary.GetPoolSummariesAsync();
        var summary = summaries.FirstOrDefault(s => s.PoolId == SelectedBudgetPool.PoolId)
            ?? SelectedBudgetPool;

        var prices = await _pricing.GetCurrentPricesAsync();
        var cost = prices.TryGetValue(SelectedCourse.Id, out var price)
            ? price
            : SelectedCourse.DefaultCertificateCost;

        FundsAdded = summary.FundsAdded;
        Committed = summary.Committed;
        Spent = summary.Spent;
        Available = summary.Free;
        CostPerStudent = cost;

        if (cost.HasValue && cost.Value > 0 && Available > 0)
        {
            CompletionsAvailable = (int)Math.Floor(Available / cost.Value);
            StatusText = $"{CompletionsAvailable} students can be completed with the available {Available:C} balance.";
        }
        else if (!cost.HasValue || cost.Value <= 0)
        {
            CompletionsAvailable = 0;
            StatusText = "Set the Allen cost for the selected unit to calculate completions.";
        }
        else
        {
            CompletionsAvailable = 0;
            StatusText = "No available funds for the selected budget.";
        }
    }
}
