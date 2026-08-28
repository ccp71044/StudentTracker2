using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class DeliveriesViewModel : ViewModelBase
{
    private readonly CourseService _courseService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<CourseDelivery> _deliveries = new();

    [ObservableProperty]
    private CourseDelivery? _selectedDelivery;

    public DeliveriesViewModel(CourseService courseService, IDialogService dialogService)
    {
        _courseService = courseService;
        _dialogService = dialogService;
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

    [RelayCommand]
    private async Task AddDelivery()
    {
        var vm = new DeliveryEditViewModel(new CourseDelivery(), _courseService, isNew: true);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditDelivery))]
    private async Task EditDelivery()
    {
        if (SelectedDelivery == null) return;
        var vm = new DeliveryEditViewModel(SelectedDelivery, _courseService, isNew: false);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await LoadAsync();
        }
    }

    private bool CanEditDelivery => SelectedDelivery != null;

    partial void OnSelectedDeliveryChanged(CourseDelivery? value)
    {
        EditDeliveryCommand.NotifyCanExecuteChanged();
    }
}
