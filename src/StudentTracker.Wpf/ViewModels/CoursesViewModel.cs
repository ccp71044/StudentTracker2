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

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showInactive;

    public CoursesViewModel(CourseService courseService, IDialogService dialogService)
    {
        _courseService = courseService;
        _dialogService = dialogService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var query = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
        var list = await _courseService.GetDefinitionsAsync(query: query, includeInactive: ShowInactive);
        Courses = new ObservableCollection<CourseDefinition>(list);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Search()
    {
        await LoadAsync();
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
    private async Task AddDelivery()
    {
        if (SelectedCourse == null) return;
        var delivery = new CourseDelivery { CourseDefinitionId = SelectedCourse.Id, CourseDefinition = SelectedCourse };
        var vm = new DeliveryEditViewModel(delivery, _courseService, isNew: true);
        await vm.LoadDataAsync();
        vm.SelectedCourse = SelectedCourse;
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteCourse))]
    private async Task DeleteCourse()
    {
        if (SelectedCourse == null || !_dialogService.Confirm($"Archive course {SelectedCourse.CourseCode}? Historical deliveries will be retained.")) return;
        try
        {
            await _courseService.SetDefinitionActiveAsync(SelectedCourse.Id, false);
            await LoadAsync();
            SelectedCourse = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The course could not be archived.", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestoreCourse))]
    private async Task RestoreCourse()
    {
        if (SelectedCourse == null || !_dialogService.Confirm($"Restore course {SelectedCourse.CourseCode}?")) return;
        try
        {
            await _courseService.SetDefinitionActiveAsync(SelectedCourse.Id, true);
            await LoadAsync();
            SelectedCourse = null;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("The course could not be restored.", ex);
        }
    }

    private bool CanEditOrDeleteCourse => SelectedCourse != null;
    private bool CanRestoreCourse => SelectedCourse?.IsActive == false;

    partial void OnShowInactiveChanged(bool value) => LoadAsync().ConfigureAwait(false);

    partial void OnSelectedCourseChanged(CourseDefinition? value)
    {
        EditCourseCommand.NotifyCanExecuteChanged();
        DeleteCourseCommand.NotifyCanExecuteChanged();
        RestoreCourseCommand.NotifyCanExecuteChanged();
        AddDeliveryCommand.NotifyCanExecuteChanged();
    }
}
