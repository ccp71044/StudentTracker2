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
    }

    protected override async Task InitialiseAsync()
    {
        var list = await _courseService.GetDeliveriesAsync();
        Deliveries = new ObservableCollection<CourseDelivery>(list);
    }

    [RelayCommand]
    private Task Refresh() => GuardAsync("Refresh", InitialiseAsync);

    [RelayCommand]
    private Task AddDelivery() => GuardAsync("Add delivery", async () =>
    {
        var vm = new DeliveryEditViewModel(new CourseDelivery(), _courseService, isNew: true);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await InitialiseAsync();
        }
    });

    [RelayCommand(CanExecute = nameof(CanEditDelivery))]
    private Task EditDelivery() => GuardAsync("Edit delivery", async () =>
    {
        if (SelectedDelivery == null) return;
        var vm = new DeliveryEditViewModel(SelectedDelivery, _courseService, isNew: false);
        await vm.LoadDataAsync();
        if (_dialogService.ShowDialog(vm) == true)
        {
            await InitialiseAsync();
        }
    });

    private bool CanEditDelivery => SelectedDelivery != null;

    partial void OnSelectedDeliveryChanged(CourseDelivery? value)
    {
        EditDeliveryCommand.NotifyCanExecuteChanged();
    }
}
