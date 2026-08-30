using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CarryForwardViewModel : ViewModelBase, ICloseable
{
    private readonly CourseService _courseService;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Carry Forward Placeholder";

    [ObservableProperty]
    private Allocation _sourceAllocation;

    [ObservableProperty]
    private ObservableCollection<CourseDelivery> _availableDeliveries = new();

    [ObservableProperty]
    private CourseDelivery? _selectedDelivery;

    [ObservableProperty]
    private string? _reason;

    public CarryForwardViewModel(Allocation sourceAllocation, CourseService courseService)
    {
        _sourceAllocation = sourceAllocation;
        _courseService = courseService;
        _title = $"Carry forward {sourceAllocation.DisplayId}";
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var deliveries = await _courseService.GetDeliveriesAsync();
        var sourceCourseId = SourceAllocation.CourseDelivery?.CourseDefinitionId;
        AvailableDeliveries = new ObservableCollection<CourseDelivery>(
            deliveries.Where(d => d.CourseDefinitionId == sourceCourseId && d.Id != SourceAllocation.CourseDeliveryId).ToList());
    }

    [RelayCommand]
    private void Save()
    {
        RequestClose?.Invoke(SelectedDelivery != null);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
