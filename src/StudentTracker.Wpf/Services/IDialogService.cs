using StudentTracker.Wpf.ViewModels;

namespace StudentTracker.Wpf.Services;

public interface IDialogService
{
    bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase;
}
