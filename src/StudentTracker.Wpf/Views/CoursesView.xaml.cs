using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StudentTracker.Wpf.Views;

public partial class CoursesView
{
    public CoursesView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.CoursesViewModel viewModel && viewModel.EditCourseCommand.CanExecute(null))
        {
            viewModel.EditCourseCommand.Execute(null);
        }
    }

    private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dependencyObject = (DependencyObject)e.OriginalSource;
        while (dependencyObject != null && dependencyObject is not DataGridRow)
        {
            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        if (dependencyObject is DataGridRow row)
        {
            CoursesDataGrid.SelectedItem = row.Item;
        }
    }
}