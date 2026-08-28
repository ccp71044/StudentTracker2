using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Enums;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class CertificateOrderEditViewModel : ViewModelBase, ICloseable
{
    private readonly CertificateService _certificateService;
    private readonly ReportService _reportService;
    private readonly AllocationService _allocationService;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "New Certificate Order";

    [ObservableProperty]
    private Allocation? _selectedAllocation;

    [ObservableProperty]
    private ObservableCollection<Allocation> _availableAllocations = new();

    [ObservableProperty]
    private string _provider = string.Empty;

    [ObservableProperty]
    private string? _externalReference;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _isReplacement;

    [ObservableProperty]
    private string? _replacementReason;

    [ObservableProperty]
    private bool _overrideEligibility;

    public CertificateOrderEditViewModel(CertificateService certificateService, ReportService reportService, AllocationService allocationService)
    {
        _certificateService = certificateService;
        _reportService = reportService;
        _allocationService = allocationService;

        // Initialize with empty collection
        AvailableAllocations = new ObservableCollection<Allocation>();

        LoadDataAsync().ConfigureAwait(false);
    }

    private async Task LoadDataAsync()
    {
        // Load allocations that are eligible for certificate ordering (completed and have credit allocated)
        var completed = await _reportService.GetCompletedStudentsAsync();
        var eligible = completed
            .Where(a => a.CertificateOrderStatus == CertificateOrderStatus.NotReady || a.CertificateOrderStatus == CertificateOrderStatus.Ready)
            .ToList();
        AvailableAllocations = new ObservableCollection<Allocation>(eligible);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedAllocation == null)
        {
            // Show error - allocation required
            return;
        }

        if (string.IsNullOrEmpty(Provider))
        {
            // Show error - provider required
            return;
        }

        try
        {
            await _certificateService.OrderCertificateAsync(
                SelectedAllocation.Id,
                Provider,
                ExternalReference,
                Notes,
                IsReplacement,
                ReplacementReason,
                OverrideEligibility);

            RequestClose?.Invoke(true);
        }
        catch
        {
            // Show error
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}