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
        if (DataContext is ViewModels.DocumentsViewModel viewModel && viewModel.ViewDocumentCommand.CanExecute(null))
        {
            viewModel.ViewDocumentCommand.Execute(null);
        }
    }
}