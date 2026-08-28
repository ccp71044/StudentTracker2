using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DeliveryEditViewModel : ViewModelBase, ICloseable
{
    private readonly CourseService _courseService;
    private readonly CourseDelivery _delivery;
    private readonly bool _isNew;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Course Delivery";

    [ObservableProperty]
    private CourseDefinition? _selectedCourse;

    [ObservableProperty]
    private ObservableCollection<CourseDefinition> _availableCourses = new();

    [ObservableProperty]
    private DateTime? _startDate;

    [ObservableProperty]
    private DateTime? _endDate;

    [ObservableProperty]
    private DeliveryDateStatus _dateStatus = DeliveryDateStatus.Confirmed;

    [ObservableProperty]
    private string? _location;

    [ObservableProperty]
    private string? _trainerName;

    [ObservableProperty]
    private string? _trainerBusinessDetails;

    [ObservableProperty]
    private int? _capacity;

    [ObservableProperty]
    private string? _deliveryStatus = "Scheduled";

    [ObservableProperty]
    private string? _notes;

    public IReadOnlyList<DeliveryDateStatus> DateStatusOptions { get; } = Enum.GetValues<DeliveryDateStatus>();

    public DeliveryEditViewModel(CourseDelivery delivery, CourseService courseService, bool isNew = false)
    {
        _delivery = delivery;
        _courseService = courseService;
        _isNew = isNew;
        Title = isNew ? "Add Course Delivery" : "Edit Course Delivery";

        // Initialize with empty collection - load data when dialog opens
        AvailableCourses = new ObservableCollection<CourseDefinition>();

        if (!_isNew)
        {
            SelectedCourse = _delivery.CourseDefinition;
            StartDate = _delivery.StartDate;
            EndDate = _delivery.EndDate;
            DateStatus = _delivery.DateStatus;
            Location = _delivery.Location;
            TrainerName = _delivery.TrainerName;
            TrainerBusinessDetails = _delivery.TrainerBusinessDetails;
            Capacity = _delivery.Capacity;
            DeliveryStatus = _delivery.DeliveryStatus;
            Notes = _delivery.Notes;
        }
    }

    public async Task LoadDataAsync()
    {
        var courses = await _courseService.GetDefinitionsAsync();
        AvailableCourses = new ObservableCollection<CourseDefinition>(courses);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedCourse == null)
        {
            // Show error - course required
            return;
        }

        _delivery.CourseDefinitionId = SelectedCourse.Id;
        _delivery.StartDate = StartDate;
        _delivery.EndDate = EndDate;
        _delivery.DateStatus = DateStatus;
        _delivery.Location = Location;
        _delivery.TrainerName = TrainerName;
        _delivery.TrainerBusinessDetails = TrainerBusinessDetails;
        _delivery.Capacity = Capacity;
        _delivery.DeliveryStatus = DeliveryStatus;
        _delivery.Notes = Notes;

        if (_isNew)
        {
            await _courseService.CreateDeliveryAsync(_delivery);
        }
        else
        {
            await _courseService.UpdateDeliveryAsync(_delivery);
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}