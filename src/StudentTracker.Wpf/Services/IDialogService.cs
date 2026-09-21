using StudentTracker.Wpf.ViewModels;

namespace StudentTracker.Wpf.Services;

public interface IDialogService
{
    bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase;
    bool Confirm(string message, string title = "Confirm action");
    void ShowError(string message, Exception? exception = null, string title = "Student Tracker");
}
