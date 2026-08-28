using System.Reflection;
using Serilog;
using System.Windows;
using System.Windows.Controls;
using StudentTracker.Wpf.ViewModels;
using StudentTracker.Wpf.Views;

namespace StudentTracker.Wpf.Services;

public class DialogService : IDialogService
{
    private readonly Dictionary<Type, Type> _viewMappings;

    public DialogService()
    {
        _viewMappings = new Dictionary<Type, Type>();
        var assembly = typeof(DialogService).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (typeof(ViewModelBase).IsAssignableFrom(type) && type.Name.EndsWith("ViewModel"))
            {
                var viewName = type.Name.Substring(0, type.Name.Length - "ViewModel".Length) + "View";
                var viewType = assembly.GetTypes().FirstOrDefault(t => t.Name == viewName && typeof(FrameworkElement).IsAssignableFrom(t));
                if (viewType != null)
                {
                    _viewMappings[type] = viewType;
                }
            }
        }
    }

    public bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase
    {
        if (!_viewMappings.TryGetValue(typeof(TViewModel), out var viewType))
            throw new InvalidOperationException($"No view registered for ViewModel {typeof(TViewModel).Name}");

        var view = (FrameworkElement)Activator.CreateInstance(viewType)!;
        view.DataContext = viewModel;

        var window = new DialogWindow { Content = view, DataContext = viewModel };

        if (viewModel is ICloseable closeable)
        {
            closeable.RequestClose += result =>
            {
                window.DialogResult = result;
                window.Close();
            };
        }

        window.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        return window.ShowDialog();
    }

    public bool Confirm(string message, string title = "Confirm action") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void ShowError(string message, Exception? exception = null, string title = "Student Tracker")
    {
        if (exception == null)
            Log.Error("{UserMessage}", message);
        else
            Log.Error(exception, "{UserMessage}", message);
        MessageBox.Show($"{message}\n\nSee the application log for details.", title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

public interface ICloseable
{
    event Action<bool?>? RequestClose;
}
