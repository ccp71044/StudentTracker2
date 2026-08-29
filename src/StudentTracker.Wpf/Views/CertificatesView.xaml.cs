using System.Windows.Controls;
using System.Windows.Input;

namespace StudentTracker.Wpf.Views;

public partial class CertificatesView : UserControl
{
    public CertificatesView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.CertificatesViewModel viewModel && viewModel.ShowOrderDetailCommand.CanExecute(null))
            viewModel.ShowOrderDetailCommand.Execute(null);
    }
}
