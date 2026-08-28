using StudentTracker.Wpf.ViewModels;

namespace StudentTracker.Wpf.Views;

public partial class CertificateDeliveryEditView
{
    public CertificateDeliveryEditView(CertificateDeliveryEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}