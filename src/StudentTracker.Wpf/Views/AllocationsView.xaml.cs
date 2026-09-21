using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

    private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dependencyObject = (DependencyObject)e.OriginalSource;
        while (dependencyObject != null && dependencyObject is not DataGridRow)
        {
            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        if (dependencyObject is DataGridRow row)
        {
            AllocationsDataGrid.SelectedItem = row.Item;
        }
    }
}
