using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class AllocationsViewModel : ViewModelBase
{
    private readonly AllocationService _allocationService;

    [ObservableProperty]
    private ObservableCollection<Allocation> _allocations = new();

    [ObservableProperty]
    private Allocation? _selectedAllocation;

    public AllocationsViewModel(AllocationService allocationService)
    {
        _allocationService = allocationService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        var list = await _allocationService.GetAllocationsAsync();
        Allocations = new ObservableCollection<Allocation>(list);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }
}
