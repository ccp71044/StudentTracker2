using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class BudgetTransactionHistoryViewModel : ViewModelBase, ICloseable
{
    private readonly BudgetService _budgetService;
    private readonly BudgetPool _pool;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Transaction History";

    [ObservableProperty]
    private ObservableCollection<BudgetTransaction> _transactions = new();

    public BudgetTransactionHistoryViewModel(BudgetPool pool, BudgetService budgetService)
    {
        _pool = pool;
        _budgetService = budgetService;
        Title = $"Budget Transactions - {pool.Name}";
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        Transactions = new ObservableCollection<BudgetTransaction>(await _budgetService.GetTransactionsAsync(_pool.Id));
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(true);
    }
}
