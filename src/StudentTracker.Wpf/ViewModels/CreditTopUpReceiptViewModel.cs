using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CreditTopUpReceiptViewModel : ViewModelBase, ICloseable
{
    private readonly CreditService _creditService;
    private readonly IDialogService _dialogService;
    private readonly CertificateCreditPool _pool;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Add Credits with Receipt";

    [ObservableProperty]
    private decimal _amount;

    [ObservableProperty]
    private decimal? _quantity;

    [ObservableProperty]
    private DateTime _transactionDate = DateTime.Today;

    [ObservableProperty]
    private string? _reference;

    [ObservableProperty]
    private string? _reason;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _receiptFilePath;

    [ObservableProperty]
    private string? _receiptFileName;

    public CreditTopUpReceiptViewModel(CertificateCreditPool pool, CreditService creditService, IDialogService dialogService)
    {
        _pool = pool;
        _creditService = creditService;
        _dialogService = dialogService;
        Title = $"Add credits with receipt - {pool.Name}";
    }

    [RelayCommand]
    private void SelectReceipt()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select receipt file",
            Filter = "Receipt files (*.pdf;*.png;*.jpg;*.jpeg)|*.pdf;*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            ReceiptFilePath = dialog.FileName;
            ReceiptFileName = Path.GetFileName(dialog.FileName);
        }
    }

    [RelayCommand]
    private void ClearReceipt()
    {
        ReceiptFilePath = null;
        ReceiptFileName = null;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (Amount <= 0)
        {
            _dialogService.ShowError("Amount must be greater than zero.");
            return;
        }

        if (Quantity.HasValue && Quantity.Value <= 0)
        {
            _dialogService.ShowError("Quantity must be greater than zero when supplied.");
            return;
        }

        try
        {
            await _creditService.TopUpWithReceiptAsync(
                _pool.Id,
                Amount,
                Quantity,
                TransactionDate,
                Reference,
                Reason,
                Notes,
                ReceiptFilePath);
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The credit top-up could not be saved.", ex);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
