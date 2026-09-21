using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CreditPoolEditViewModel : ViewModelBase, ICloseable
{
    private readonly CreditService _creditService;
    private readonly CertificateCreditPool _pool;
    private readonly bool _isNew;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Credit Pool";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _provider;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private CreditUnitType _unitType = CreditUnitType.Count;

    [ObservableProperty]
    private DateTime? _expiryDate;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _isActive = true;

    public IReadOnlyList<CreditUnitType> UnitTypeOptions { get; } = Enum.GetValues<CreditUnitType>();

    public CreditPoolEditViewModel(CertificateCreditPool pool, CreditService creditService, bool isNew = false)
    {
        _pool = pool;
        _creditService = creditService;
        _isNew = isNew;
        Title = isNew ? "Add Credit Pool" : "Edit Credit Pool";
        Name = pool.Name;
        Provider = pool.Provider;
        Description = pool.Description;
        UnitType = pool.UnitType;
        ExpiryDate = pool.ExpiryDate;
        Notes = pool.Notes;
        IsActive = pool.IsActive;
    }

    [RelayCommand]
    private async Task Save()
    {
        _pool.Name = Name;
        _pool.Provider = Provider;
        _pool.Description = Description;
        _pool.UnitType = UnitType;
        _pool.ExpiryDate = ExpiryDate;
        _pool.Notes = Notes;
        _pool.IsActive = IsActive;

        if (_isNew)
        {
            await _creditService.CreatePoolAsync(_pool);
        }
        else
        {
            await _creditService.UpdatePoolAsync(_pool);
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}