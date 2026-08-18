using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class BudgetPoolEditViewModel : ViewModelBase, ICloseable
{
    private readonly BudgetService _budgetService;
    private readonly BudgetPool _pool;
    private readonly bool _isNew;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Budget Pool";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _financialPeriod;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _isActive = true;

    public BudgetPoolEditViewModel(BudgetPool pool, BudgetService budgetService, bool isNew = false)
    {
        _pool = pool;
        _budgetService = budgetService;
        _isNew = isNew;
        Title = isNew ? "Add Budget Pool" : "Edit Budget Pool";
        Name = pool.Name;
        Description = pool.Description;
        FinancialPeriod = pool.FinancialPeriod;
        Notes = pool.Notes;
        IsActive = pool.IsActive;
    }

    [RelayCommand]
    private async Task Save()
    {
        _pool.Name = Name;
        _pool.Description = Description;
        _pool.FinancialPeriod = FinancialPeriod;
        _pool.Notes = Notes;
        _pool.IsActive = IsActive;

        if (_isNew)
        {
            await _budgetService.CreatePoolAsync(_pool);
        }
        else
        {
            await _budgetService.UpdatePoolAsync(_pool);
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
