using System.Windows.Controls;
using System.Windows.Input;
using StudentTracker.Core.Models;
using StudentTracker.Wpf.ViewModels;

namespace StudentTracker.Wpf.Views;

public partial class CreditsBudgetsView : UserControl
{
    public CreditsBudgetsView()
    {
        InitializeComponent();
    }

    private void CreditPoolsDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is CertificateCreditPool pool && DataContext is CreditsBudgetsViewModel viewModel)
            viewModel.BeginCreditPoolInlineEdit(pool);
    }

    private void BudgetPoolsDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is BudgetPoolRow row && DataContext is CreditsBudgetsViewModel viewModel)
            viewModel.BeginBudgetPoolInlineEdit(row);
    }

    private void CreditPoolsDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not CertificateCreditPool pool || DataContext is not CreditsBudgetsViewModel viewModel) return;
        Dispatcher.BeginInvoke(new Action(async () => await viewModel.CommitCreditPoolInlineEditAsync(pool)));
    }

    private void BudgetPoolsDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not BudgetPoolRow row || DataContext is not CreditsBudgetsViewModel viewModel) return;
        Dispatcher.BeginInvoke(new Action(async () => await viewModel.CommitBudgetPoolInlineEditAsync(row)));
    }

    private void CreditPoolsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CreditsBudgetsViewModel viewModel && !viewModel.IsCreditTableEditingEnabled && viewModel.CreditTransactionHistoryCommand.CanExecute(null))
            viewModel.CreditTransactionHistoryCommand.Execute(null);
    }

    private void BudgetPoolsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CreditsBudgetsViewModel viewModel && !viewModel.IsBudgetTableEditingEnabled && viewModel.BudgetTransactionHistoryCommand.CanExecute(null))
            viewModel.BudgetTransactionHistoryCommand.Execute(null);
    }
}
