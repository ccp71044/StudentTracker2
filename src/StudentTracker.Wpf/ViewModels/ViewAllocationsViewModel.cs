using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class ViewAllocationsViewModel : ViewModelBase, ICloseable
{
    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Delivery Allocations";

    [ObservableProperty]
    private ObservableCollection<Allocation> _allocations = new();

    public string DeliveryDisplayId { get; }
    public string? CourseCode { get; }

    public ViewAllocationsViewModel(CourseDelivery delivery, List<Allocation> allocations)
    {
        DeliveryDisplayId = delivery.DisplayId ?? "Unknown";
        CourseCode = delivery.CourseDefinition?.CourseCode;
        Title = $"Allocations - {DeliveryDisplayId} ({CourseCode ?? "No Course"})";
        Allocations = new ObservableCollection<Allocation>(allocations);
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(null);
    }
}
