using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CreditTransactionHistoryViewModel : ViewModelBase, ICloseable
{
    private readonly CreditService _creditService;
    private readonly CertificateCreditPool _pool;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Transaction History";

    [ObservableProperty]
    private ObservableCollection<CertificateCreditTransaction> _transactions = new();

    public CreditTransactionHistoryViewModel(CertificateCreditPool pool, CreditService creditService)
    {
        _pool = pool;
        _creditService = creditService;
        Title = $"Credit Transactions - {pool.Name}";
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        Transactions = new ObservableCollection<CertificateCreditTransaction>(await _creditService.GetTransactionsAsync(_pool.Id));
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(true);
    }
}
