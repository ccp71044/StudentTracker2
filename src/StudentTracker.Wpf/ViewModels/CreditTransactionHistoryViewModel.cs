using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CreditTransactionHistoryViewModel : ViewModelBase, ICloseable
{
    private readonly CreditService _creditService;
    private readonly IDialogService _dialogService;
    private readonly CertificateCreditPool _pool;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Transaction History";

    [ObservableProperty]
    private ObservableCollection<CreditTransactionRow> _rows = new();

    [ObservableProperty]
    private CreditTransactionRow? _selectedRow;

    public CreditTransactionHistoryViewModel(CertificateCreditPool pool, CreditService creditService, IDialogService dialogService)
    {
        _pool = pool;
        _creditService = creditService;
        _dialogService = dialogService;
        Title = $"Credit Transactions - {pool.Name}";
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var transactions = await _creditService.GetTransactionsWithReceiptsAsync(_pool.Id);
        Rows = new ObservableCollection<CreditTransactionRow>(transactions.Select(t => new CreditTransactionRow
        {
            Transaction = t.Transaction,
            Receipt = t.Receipt
        }));
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(true);
    }

    [RelayCommand(CanExecute = nameof(CanOpenReceipt))]
    private void OpenReceipt()
    {
        if (SelectedRow?.Receipt == null) return;

        try
        {
            var filePath = _creditService.GetDocumentFullPath(SelectedRow.Receipt);
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            else
            {
                _dialogService.ShowError("The receipt file could not be found.");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The receipt could not be opened.", ex);
        }
    }

    partial void OnSelectedRowChanged(CreditTransactionRow? value) => OpenReceiptCommand.NotifyCanExecuteChanged();

    private bool CanOpenReceipt => SelectedRow?.Receipt != null;
}
