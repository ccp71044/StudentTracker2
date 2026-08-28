using System.Windows.Controls;
using System.Windows.Input;

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
}
