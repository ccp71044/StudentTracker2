using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StudentTracker.Core.Models;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class PoolPositionViewModel : ViewModelBase
{
    private readonly ClientPrepaidEntitlementService _entitlement;

    [ObservableProperty]
    private ObservableCollection<ClientPrepaidPool> _pools = new();

    [ObservableProperty]
    private ClientPrepaidPool? _selectedPool;

    [ObservableProperty]
    private ClientPrepaidPoolPosition _position = new();

    [ObservableProperty]
    private FundingCalculation _funding = new();

    [ObservableProperty]
    private decimal _requestedPlaces;

    [ObservableProperty]
    private decimal _newPlacesToAdd;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _statusText = "Select a client prepaid pool.";

    public PoolPositionViewModel(ClientPrepaidEntitlementService entitlement)
    {
        _entitlement = entitlement;
        _ = LoadAsync();
    }

    partial void OnSelectedPoolChanged(ClientPrepaidPool? value) => _ = RefreshAsync();
    partial void OnRequestedPlacesChanged(decimal value) => _ = RecalculateFundingAsync();
    partial void OnNewPlacesToAddChanged(decimal value) => _ = RecalculateFundingAsync();

    private async Task LoadAsync()
    {
        var pools = await _entitlement.GetPoolsAsync();
        Pools = new ObservableCollection<ClientPrepaidPool>(pools);
        SelectedPool = Pools.FirstOrDefault();
        IsLoading = false;
    }

    private async Task RefreshAsync()
    {
        if (SelectedPool == null)
        {
            StatusText = "Select a client prepaid pool.";
            return;
        }

        Position = await _entitlement.GetPoolPositionAsync(SelectedPool.Id);
        await RecalculateFundingAsync();
        StatusText = $"{Position.PrepaidPlacesLoaded:0} loaded, {Position.PlacesConsumed:0} consumed, {Position.UnassignedCarryForward:0} unassigned.";
    }

    private async Task RecalculateFundingAsync()
    {
        if (SelectedPool == null)
            return;

        Funding = await _entitlement.CalculateFundingAsync(SelectedPool.Id, RequestedPlaces, NewPlacesToAdd);
    }
}
