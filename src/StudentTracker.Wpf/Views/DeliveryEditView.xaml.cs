using StudentTracker.Wpf.ViewModels;

namespace StudentTracker.Wpf.Views;

public partial class DeliveryEditView
{
    public DeliveryEditView(DeliveryEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}