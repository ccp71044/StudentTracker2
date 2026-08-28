using StudentTracker.Wpf.ViewModels;

namespace StudentTracker.Wpf.Views;

public partial class CertificateOrderEditView
{
    public CertificateOrderEditView(CertificateOrderEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}