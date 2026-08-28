using System.Windows.Controls;
using System.Windows.Input;

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
}