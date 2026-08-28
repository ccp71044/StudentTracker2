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

    [ObservableProperty]
    private ObservableCollection<CertificateCreditPool> _creditPools = new();

    [ObservableProperty]
    private ObservableCollection<BudgetPoolRow> _budgetPools = new();

    [ObservableProperty]
    private BudgetPoolRow? _selectedBudgetPool;

    [ObservableProperty]
    private CertificateCreditPool? _selectedCreditPool;

    public CreditsBudgetsViewModel(CreditService creditService, BudgetService budgetService, IDialogService dialogService)
    {
        _creditService = creditService;
        _budgetService = budgetService;
        _dialogService = dialogService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        CreditPools = new ObservableCollection<CertificateCreditPool>(await _creditService.GetPoolsAsync());
        var pools = await _budgetService.GetPoolsAsync();
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
    private async Task ArchiveBudgetPool()
    {
        if (SelectedBudgetPool == null) return;
        await _budgetService.ArchivePoolAsync(SelectedBudgetPool.Pool.Id);
        await LoadAsync();
        SelectedBudgetPool = null;
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
    private async Task ArchiveCreditPool()
    {
        if (SelectedCreditPool == null) return;
        await _creditService.ArchivePoolAsync(SelectedCreditPool.Id);
        await LoadAsync();
        SelectedCreditPool = null;
    }

    private bool CanEditBudgetPool => SelectedBudgetPool != null;
    private bool CanEditCreditPool => SelectedCreditPool != null;

    partial void OnSelectedBudgetPoolChanged(BudgetPoolRow? value)
    {
        EditBudgetPoolCommand.NotifyCanExecuteChanged();
        AddFundsCommand.NotifyCanExecuteChanged();
        ArchiveBudgetPoolCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCreditPoolChanged(CertificateCreditPool? value)
    {
        EditCreditPoolCommand.NotifyCanExecuteChanged();
        ArchiveCreditPoolCommand.NotifyCanExecuteChanged();
    }
}

public class BudgetPoolRow
{
    public BudgetPool Pool { get; set; } = null!;
    public decimal ActualAvailable { get; set; }
    public decimal ForecastAvailable { get; set; }
    public string Name => Pool.Name;
    public string? FinancialPeriod => Pool.FinancialPeriod;
    public string? Notes => Pool.Notes;
}

