using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class PlaceholderAllocationViewModel : ViewModelBase, ICloseable
{
    private readonly AllocationService _allocationService;
    private readonly CourseService _courseService;
    private readonly BudgetService _budgetService;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Add Placeholder";

    [ObservableProperty]
    private string _placeholderName = string.Empty;

    [ObservableProperty]
    private string? _legacyReference;

    [ObservableProperty]
    private int _quantity = 1;

    [ObservableProperty]
    private decimal? _certificateCost;

    [ObservableProperty]
    private CourseDelivery? _selectedDelivery;

    [ObservableProperty]
    private BudgetPool? _selectedBudgetPool;

    [ObservableProperty]
    private ObservableCollection<CourseDelivery> _availableDeliveries = new();

    [ObservableProperty]
    private ObservableCollection<BudgetPool> _availableBudgetPools = new();

    public PlaceholderAllocationViewModel(AllocationService allocationService, CourseService courseService, BudgetService budgetService)
    {
        _allocationService = allocationService;
        _courseService = courseService;
        _budgetService = budgetService;
    }

    public async Task LoadDataAsync()
    {
        var deliveries = await _courseService.GetDeliveriesAsync();
        var budgetPools = await _budgetService.GetPoolsAsync();
        AvailableDeliveries = new ObservableCollection<CourseDelivery>(deliveries);
        AvailableBudgetPools = new ObservableCollection<BudgetPool>(budgetPools);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedDelivery == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PlaceholderName))
        {
            return;
        }

        if (Quantity <= 0)
        {
            return;
        }

        await _allocationService.CreatePlaceholderAllocationsAsync(
            SelectedDelivery.Id,
            PlaceholderName,
            Quantity,
            CertificateCost,
            SelectedBudgetPool?.Id,
            LegacyReference);

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
