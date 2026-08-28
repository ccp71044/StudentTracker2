using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class BudgetAddFundsViewModel : ViewModelBase, ICloseable
{
    private readonly BudgetService _budgetService;
    private readonly BudgetPool _pool;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Add Funds";

    [ObservableProperty]
    private decimal _amount;

    [ObservableProperty]
    private string? _reason;

    public BudgetAddFundsViewModel(BudgetPool pool, BudgetService budgetService)
    {
        _pool = pool;
        _budgetService = budgetService;
        Title = $"Add funds to {pool.Name}";
    }

    [RelayCommand]
    private Task Save() => GuardAsync("Save", async () =>
    {
        if (Amount <= 0)
        {
            ErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        await _budgetService.AddFundsAsync(_pool.Id, Amount, reason: Reason);
        RequestClose?.Invoke(true);
    });

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
