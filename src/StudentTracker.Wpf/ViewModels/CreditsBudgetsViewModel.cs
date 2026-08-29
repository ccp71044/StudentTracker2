using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CreditsBudgetsViewModel : ViewModelBase
{
    private readonly CreditService _creditService;
    private readonly BudgetService _budgetService;
    private readonly IDialogService _dialogService;
    private readonly Dictionary<Guid, (string Name, string? Provider, DateTime? ExpiryDate, string? Notes)> _creditEditSnapshots = new();
    private readonly Dictionary<Guid, (string Name, string? FinancialPeriod, string? Notes)> _budgetEditSnapshots = new();

    [ObservableProperty]
    private ObservableCollection<CertificateCreditPool> _creditPools = new();

    [ObservableProperty]
    private ObservableCollection<BudgetPoolRow> _budgetPools = new();

    [ObservableProperty]
    private BudgetPoolRow? _selectedBudgetPool;

    [ObservableProperty]
    private CertificateCreditPool? _selectedCreditPool;

    [ObservableProperty]
    private bool _showInactive;

    [ObservableProperty]
    private bool _isCreditTableEditingEnabled;

    [ObservableProperty]
    private bool _isBudgetTableEditingEnabled;

    public CreditsBudgetsViewModel(CreditService creditService, BudgetService budgetService, IDialogService dialogService)
    {
        _creditService = creditService;
        _budgetService = budgetService;
        _dialogService = dialogService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        CreditPools = new ObservableCollection<CertificateCreditPool>(await _creditService.GetPoolsAsync(ShowInactive));
        var pools = await _budgetService.GetPoolsAsync(ShowInactive);
        var rows = new List<BudgetPoolRow>();
        foreach (var pool in pools)
        {
            rows.Add(new BudgetPoolRow
            {
                Pool = pool,
                ActualAvailable = await _budgetService.GetActualAvailableAsync(pool.Id),
                ForecastAvailable = await _budgetService.GetForecastAvailableAsync(pool.Id)
            });
        }
        BudgetPools = new ObservableCollection<BudgetPoolRow>(rows);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddBudgetPool()
    {
        var vm = new BudgetPoolEditViewModel(new BudgetPool { Name = "" }, _budgetService, isNew: true);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditBudgetPool))]
    private async Task EditBudgetPool()
    {
        if (SelectedBudgetPool == null) return;
        var vm = new BudgetPoolEditViewModel(SelectedBudgetPool.Pool, _budgetService, isNew: false);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditBudgetPool))]
    private async Task AddFunds()
    {
        if (SelectedBudgetPool == null) return;
        var vm = new BudgetAddFundsViewModel(SelectedBudgetPool.Pool, _budgetService);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditBudgetPool))]
    private void BudgetTransactionHistory()
    {
        if (SelectedBudgetPool == null) return;
        var vm = new BudgetTransactionHistoryViewModel(SelectedBudgetPool.Pool, _budgetService);
        _dialogService.ShowDialog(vm);
    }

    [RelayCommand(CanExecute = nameof(CanEditBudgetPool))]
    private async Task ArchiveBudgetPool()
    {
        if (SelectedBudgetPool == null || !_dialogService.Confirm($"Archive budget pool {SelectedBudgetPool.Name}? Transactions will be retained.")) return;
        try
        {
            await _budgetService.ArchivePoolAsync(SelectedBudgetPool.Pool.Id);
            await LoadAsync();
            SelectedBudgetPool = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The budget pool could not be archived.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestoreBudgetPool))]
    private async Task RestoreBudgetPool()
    {
        if (SelectedBudgetPool == null || !_dialogService.Confirm($"Restore budget pool {SelectedBudgetPool.Name}?")) return;
        try
        {
            await _budgetService.RestorePoolAsync(SelectedBudgetPool.Pool.Id);
            await LoadAsync();
            SelectedBudgetPool = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The budget pool could not be restored.", ex);
        }
    }

    [RelayCommand]
    private async Task AddCreditPool()
    {
        var vm = new CreditPoolEditViewModel(new CertificateCreditPool { Name = "" }, _creditService, isNew: true);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditCreditPool))]
    private async Task EditCreditPool()
    {
        if (SelectedCreditPool == null) return;
        var vm = new CreditPoolEditViewModel(SelectedCreditPool, _creditService, isNew: false);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditCreditPool))]
    private async Task AddCredits()
    {
        if (SelectedCreditPool == null) return;
        var vm = new CreditAddFundsViewModel(SelectedCreditPool, _creditService);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditCreditPool))]
    private void CreditTransactionHistory()
    {
        if (SelectedCreditPool == null) return;
        var vm = new CreditTransactionHistoryViewModel(SelectedCreditPool, _creditService);
        _dialogService.ShowDialog(vm);
    }

    [RelayCommand(CanExecute = nameof(CanEditCreditPool))]
    private async Task ArchiveCreditPool()
    {
        if (SelectedCreditPool == null || !_dialogService.Confirm($"Archive credit pool {SelectedCreditPool.Name}? Transactions will be retained.")) return;
        try
        {
            await _creditService.ArchivePoolAsync(SelectedCreditPool.Id);
            await LoadAsync();
            SelectedCreditPool = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The credit pool could not be archived.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestoreCreditPool))]
    private async Task RestoreCreditPool()
    {
        if (SelectedCreditPool == null || !_dialogService.Confirm($"Restore credit pool {SelectedCreditPool.Name}?")) return;
        try
        {
            await _creditService.RestorePoolAsync(SelectedCreditPool.Id);
            await LoadAsync();
            SelectedCreditPool = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The credit pool could not be restored.", ex);
        }
    }

    public void BeginCreditPoolInlineEdit(CertificateCreditPool pool) =>
        _creditEditSnapshots.TryAdd(pool.Id, (pool.Name, pool.Provider, pool.ExpiryDate, pool.Notes));

    public void BeginBudgetPoolInlineEdit(BudgetPoolRow row) =>
        _budgetEditSnapshots.TryAdd(row.Pool.Id, (row.Name, row.FinancialPeriod, row.Notes));

    public async Task CommitCreditPoolInlineEditAsync(CertificateCreditPool pool)
    {
        if (!IsCreditTableEditingEnabled) return;
        try
        {
            await _creditService.UpdatePoolAsync(pool);
        }
        catch (Exception ex)
        {
            if (_creditEditSnapshots.TryGetValue(pool.Id, out var snapshot))
            {
                pool.Name = snapshot.Name;
                pool.Provider = snapshot.Provider;
                pool.ExpiryDate = snapshot.ExpiryDate;
                pool.Notes = snapshot.Notes;
            }
            _dialogService.ShowError("The credit pool metadata could not be updated.", ex);
        }
        finally
        {
            _creditEditSnapshots.Remove(pool.Id);
            await LoadAsync();
        }
    }

    public async Task CommitBudgetPoolInlineEditAsync(BudgetPoolRow row)
    {
        if (!IsBudgetTableEditingEnabled) return;
        try
        {
            await _budgetService.UpdatePoolAsync(row.Pool);
        }
        catch (Exception ex)
        {
            if (_budgetEditSnapshots.TryGetValue(row.Pool.Id, out var snapshot))
            {
                row.Name = snapshot.Name;
                row.FinancialPeriod = snapshot.FinancialPeriod;
                row.Notes = snapshot.Notes;
            }
            _dialogService.ShowError("The budget pool metadata could not be updated.", ex);
        }
        finally
        {
            _budgetEditSnapshots.Remove(row.Pool.Id);
            await LoadAsync();
        }
    }

    private bool CanEditBudgetPool => SelectedBudgetPool != null;
    private bool CanEditCreditPool => SelectedCreditPool != null;
    private bool CanRestoreBudgetPool => SelectedBudgetPool?.Pool.IsActive == false;
    private bool CanRestoreCreditPool => SelectedCreditPool?.IsActive == false;

    partial void OnSelectedBudgetPoolChanged(BudgetPoolRow? value)
    {
        EditBudgetPoolCommand.NotifyCanExecuteChanged();
        AddFundsCommand.NotifyCanExecuteChanged();
        BudgetTransactionHistoryCommand.NotifyCanExecuteChanged();
        ArchiveBudgetPoolCommand.NotifyCanExecuteChanged();
        RestoreBudgetPoolCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCreditPoolChanged(CertificateCreditPool? value)
    {
        EditCreditPoolCommand.NotifyCanExecuteChanged();
        AddCreditsCommand.NotifyCanExecuteChanged();
        CreditTransactionHistoryCommand.NotifyCanExecuteChanged();
        ArchiveCreditPoolCommand.NotifyCanExecuteChanged();
        RestoreCreditPoolCommand.NotifyCanExecuteChanged();
    }

    partial void OnShowInactiveChanged(bool value) => LoadAsync().ConfigureAwait(false);
}

public class BudgetPoolRow
{
    public BudgetPool Pool { get; set; } = null!;
    public decimal ActualAvailable { get; set; }
    public decimal ForecastAvailable { get; set; }
    public string Name
    {
        get => Pool.Name;
        set => Pool.Name = value;
    }
    public string? FinancialPeriod
    {
        get => Pool.FinancialPeriod;
        set => Pool.FinancialPeriod = value;
    }
    public string? Notes
    {
        get => Pool.Notes;
        set => Pool.Notes = value;
    }
}

