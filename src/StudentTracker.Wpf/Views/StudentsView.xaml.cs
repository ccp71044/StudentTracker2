using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StudentTracker.Wpf.Views;

public partial class StudentsView
{
    public StudentsView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.StudentsViewModel viewModel && viewModel.EditStudentCommand.CanExecute(null))
        {
            viewModel.EditStudentCommand.Execute(null);
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
            StudentsDataGrid.SelectedItem = row.Item;
        }
    }
}
