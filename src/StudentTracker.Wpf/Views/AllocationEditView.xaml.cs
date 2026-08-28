using StudentTracker.Wpf.ViewModels;

namespace StudentTracker.Wpf.Views;

public partial class AllocationEditView
{
    public AllocationEditView(AllocationEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}