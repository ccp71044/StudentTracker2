using StudentTracker.Wpf.ViewModels;

namespace StudentTracker.Wpf.Views;

public partial class CreditPoolEditView
{
    public CreditPoolEditView(CreditPoolEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}