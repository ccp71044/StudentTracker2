using System.Reflection;
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
        var views = assembly.GetTypes()
            .Where(t => typeof(FrameworkElement).IsAssignableFrom(t))
            .ToDictionary(t => t.Name, t => t, StringComparer.Ordinal);

        foreach (var type in assembly.GetTypes())
        {
            if (!typeof(ViewModelBase).IsAssignableFrom(type) || !type.Name.EndsWith("ViewModel"))
                continue;

            var stem = type.Name.Substring(0, type.Name.Length - "ViewModel".Length);

            // StudentEditViewModel pairs with StudentEditView, but StudentViewViewModel pairs with
            // StudentView - the stem already ends in "View".
            if (views.TryGetValue(stem + "View", out var viewType) || views.TryGetValue(stem, out viewType))
            {
                _viewMappings[type] = viewType;
            }
        }
    }

    public bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : ViewModelBase
    {
        if (!_viewMappings.TryGetValue(viewModel.GetType(), out var viewType))
            throw new InvalidOperationException($"No view registered for ViewModel {viewModel.GetType().Name}");

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
}

public interface ICloseable
{
    event Action<bool?>? RequestClose;
}
