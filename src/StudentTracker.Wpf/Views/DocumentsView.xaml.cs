using System.Windows.Controls;
using System.Windows.Input;

namespace StudentTracker.Wpf.Views;

public partial class DocumentsView
{
    public DocumentsView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.DocumentsViewModel viewModel && !viewModel.IsTableEditingEnabled && viewModel.ViewDocumentCommand.CanExecute(null))
        {
            viewModel.ViewDocumentCommand.Execute(null);
        }
    }

    private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is Core.Models.Document document && DataContext is ViewModels.DocumentsViewModel viewModel)
            viewModel.BeginInlineEdit(document);
    }

    private void DataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not Core.Models.Document document || DataContext is not ViewModels.DocumentsViewModel viewModel) return;
        Dispatcher.BeginInvoke(new Action(async () => await viewModel.CommitInlineEditAsync(document)));
    }
}