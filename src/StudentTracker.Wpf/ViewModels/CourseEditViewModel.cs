using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CourseEditViewModel : ViewModelBase, ICloseable
{
    private readonly CourseService _courseService;
    private readonly CourseDefinition _course;
    private readonly bool _isNew;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Course";

    [ObservableProperty]
    private string _courseCode = string.Empty;

    [ObservableProperty]
    private string _courseTitle = string.Empty;

    [ObservableProperty]
    private string? _category;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _provider;

    [ObservableProperty]
    private decimal? _defaultCertificateCost;

    [ObservableProperty]
    private decimal? _defaultCreditQuantity;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _notes;

    public CourseEditViewModel(CourseDefinition course, CourseService courseService, bool isNew = false)
    {
        _course = course;
        _courseService = courseService;
        _isNew = isNew;
        Title = isNew ? "Add Course" : "Edit Course";
        CourseCode = course.CourseCode;
        CourseTitle = course.CourseTitle;
        Category = course.Category;
        Description = course.Description;
        Provider = course.Provider;
        DefaultCertificateCost = course.DefaultCertificateCost;
        DefaultCreditQuantity = course.DefaultCreditQuantity;
        IsActive = course.IsActive;
        Notes = course.Notes;
    }

    [RelayCommand]
    private async Task Save()
    {
        _course.CourseCode = CourseCode;
        _course.CourseTitle = CourseTitle;
        _course.Category = Category;
        _course.Description = Description;
        _course.Provider = Provider;
        _course.DefaultCertificateCost = DefaultCertificateCost;
        _course.DefaultCreditQuantity = DefaultCreditQuantity;
        _course.IsActive = IsActive;
        _course.Notes = Notes;

        if (_isNew)
        {
            await _courseService.CreateDefinitionAsync(_course);
        }
        else
        {
            await _courseService.UpdateDefinitionAsync(_course);
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
