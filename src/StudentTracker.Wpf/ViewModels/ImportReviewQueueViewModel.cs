using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Core.Models;
using StudentTracker.Services;
using StudentTracker.Wpf.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class ImportReviewQueueViewModel : ViewModelBase
{
    private readonly ImportService _importService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<ImportReviewQueue> _items = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResolveCommand))]
    private ImportReviewQueue? _selectedItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResolveCommand))]
    private string? _resolution;

    [ObservableProperty]
    private string _statusFilter = "Pending";

    public IReadOnlyList<string> StatusFilterOptions { get; } = new[] { "Pending", "Resolved", "All" };

    public ImportReviewQueueViewModel(ImportService importService, IDialogService dialogService)
    {
        _importService = importService;
        _dialogService = dialogService;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        string? status = StatusFilter == "All" ? null : StatusFilter;
        var list = await _importService.GetReviewQueueAsync(status);
        Items = new ObservableCollection<ImportReviewQueue>(list);
    }

    partial void OnStatusFilterChanged(string value)
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanResolve))]
    private async Task Resolve()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(Resolution)) return;

        await _importService.ResolveAsync(SelectedItem.Id, Resolution);
        Resolution = null;
        await LoadAsync();
    }

    private bool CanResolve => SelectedItem != null && !string.IsNullOrWhiteSpace(Resolution);

    [RelayCommand]
    private void CopyDetails()
    {
        if (SelectedItem == null) return;
        Clipboard.SetText($"{SelectedItem.EntityType} {SelectedItem.SourceFileName} row {SelectedItem.SourceRow}: {SelectedItem.Issue}");
    }
}
