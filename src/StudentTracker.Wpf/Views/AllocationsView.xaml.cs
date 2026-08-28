using System.Windows.Controls;
using System.Windows.Input;

namespace StudentTracker.Wpf.Views;

public partial class AllocationsView
{
    public AllocationsView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.AllocationsViewModel viewModel && viewModel.EditAllocationCommand.CanExecute(null))
        {
            viewModel.EditAllocationCommand.Execute(null);
        }
    }
}