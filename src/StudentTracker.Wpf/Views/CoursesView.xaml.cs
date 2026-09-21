using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StudentTracker.Core.Models;

namespace StudentTracker.Wpf.Views;

public partial class CoursesView
{
    public CoursesView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.CoursesViewModel viewModel && !viewModel.IsInlineEditingEnabled && viewModel.EditCourseCommand.CanExecute(null))
        {
            viewModel.EditCourseCommand.Execute(null);
        }
    }

    private void DataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (DataContext is ViewModels.CoursesViewModel viewModel && e.EditAction == DataGridEditAction.Commit && e.Row.Item is CourseDefinition course)
        {
            viewModel.CourseRowEditEndingCommand.Execute(course);
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