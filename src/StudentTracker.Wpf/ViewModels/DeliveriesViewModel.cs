using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DeliveriesViewModel : ViewModelBase
{
    private readonly CourseService _courseService;

    [ObservableProperty]
    private ObservableCollection<CourseDelivery> _deliveries = new();

    [ObservableProperty]
    private CourseDelivery? _selectedDelivery;

    public DeliveriesViewModel(CourseService courseService)
    {
        _courseService = courseService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var list = await _courseService.GetDeliveriesAsync();
        Deliveries = new ObservableCollection<CourseDelivery>(list);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }
}
