using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CoursesViewModel : ViewModelBase
{
    private readonly CourseService _courseService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<CourseDefinition> _courses = new();

    [ObservableProperty]
    private CourseDefinition? _selectedCourse;

    public CoursesViewModel(CourseService courseService, IDialogService dialogService)
    {
        _courseService = courseService;
        _dialogService = dialogService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var list = await _courseService.GetDefinitionsAsync();
        Courses = new ObservableCollection<CourseDefinition>(list);
    }

    [RelayCommand]
    private async Task AddCourse()
    {
        var vm = new CourseEditViewModel(new CourseDefinition(), _courseService, isNew: true);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteCourse))]
    private async Task EditCourse()
    {
        if (SelectedCourse == null) return;
        var vm = new CourseEditViewModel(SelectedCourse, _courseService, isNew: false);
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteCourse))]
    private async Task DeleteCourse()
    {
        if (SelectedCourse == null) return;
        SelectedCourse.IsActive = false;
        await _courseService.UpdateDefinitionAsync(SelectedCourse);
        await LoadAsync();
        SelectedCourse = null;
    }

    private bool CanEditOrDeleteCourse => SelectedCourse != null;

    partial void OnSelectedCourseChanged(CourseDefinition? value)
    {
        EditCourseCommand.NotifyCanExecuteChanged();
        DeleteCourseCommand.NotifyCanExecuteChanged();
    }
}
