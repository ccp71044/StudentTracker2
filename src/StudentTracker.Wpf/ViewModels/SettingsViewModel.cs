using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentTracker.Data;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly DatabaseBootstrap _bootstrap;

    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private string _version = AppVersion.Current;

    [ObservableProperty]
    private string _status = string.Empty;

    public SettingsViewModel(DatabaseBootstrap bootstrap, DataLocationService dataLocation)
    {
        _bootstrap = bootstrap;
        DatabasePath = dataLocation.DatabasePath;
    }

    [RelayCommand]
    private void CompactDatabase() => Guard("CompactDatabase", () =>
    {
        using var context = _bootstrap.CreateContext();
        _bootstrap.CompactDatabase(context);
        Status = "Database compacted.";
    });
}
