using System.Windows.Controls;
using System.Windows.Input;

namespace StudentTracker.Wpf.Views;

public partial class DeliveriesView
{
    public DeliveriesView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.DeliveriesViewModel viewModel && viewModel.EditDeliveryCommand.CanExecute(null))
        {
            viewModel.EditDeliveryCommand.Execute(null);
        }
    }
}