using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CreditAddFundsViewModel : ViewModelBase, ICloseable
{
    private readonly CreditService _creditService;
    private readonly CertificateCreditPool _pool;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Add Credits";

    [ObservableProperty]
    private decimal _amount;

    [ObservableProperty]
    private decimal? _quantity;

    [ObservableProperty]
    private string? _reason;

    public CreditAddFundsViewModel(CertificateCreditPool pool, CreditService creditService)
    {
        _pool = pool;
        _creditService = creditService;
        Title = $"Add credits to {pool.Name}";
    }

    [RelayCommand]
    private async Task Save()
    {
        if (Amount <= 0)
        {
            return;
        }

        if (Quantity.HasValue && Quantity.Value <= 0)
        {
            return;
        }

        await _creditService.TopUpAsync(_pool.Id, Amount, quantity: Quantity, reason: Reason);
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
