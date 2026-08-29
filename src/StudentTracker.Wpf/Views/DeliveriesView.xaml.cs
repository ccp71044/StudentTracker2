using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StudentTracker.Core.Models;

namespace StudentTracker.Wpf.Views;

public partial class DeliveriesView
{
    public DeliveriesView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.DeliveriesViewModel viewModel && !viewModel.IsInlineEditingEnabled && viewModel.EditDeliveryCommand.CanExecute(null))
        {
            viewModel.EditDeliveryCommand.Execute(null);
        }
    }

    private void DataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (DataContext is ViewModels.DeliveriesViewModel viewModel && e.EditAction == DataGridEditAction.Commit && e.Row.Item is CourseDelivery delivery)
        {
            viewModel.DeliveryRowEditEndingCommand.Execute(delivery);
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
            DeliveriesDataGrid.SelectedItem = row.Item;
        }
    }
}