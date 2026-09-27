using CommunityToolkit.Mvvm.ComponentModel;
using StudentTracker.Services;

namespace StudentTracker.Wpf.ViewModels;

public class ViewModelBase : ObservableObject
{
    private bool _initialised;
    private string _errorMessage = string.Empty;

    /// <summary>
    /// The reason the last action did nothing, shown next to the action that failed.
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Loads the data a section needs. Called the first time the section is shown rather than in
    /// the constructor: constructor loads were fire-and-forget, so they raced each other over the
    /// shared database context and their failures were never seen.
    /// </summary>
    protected virtual Task InitialiseAsync() => Task.CompletedTask;

    public async Task EnsureInitialisedAsync()
    {
        if (_initialised) return;
        _initialised = true;
        await GuardAsync("Load", InitialiseAsync);
    }

    /// <summary>
    /// Runs a command body, logging and reporting any failure instead of letting it reach the
    /// application-wide handler as an unexplained error dialog.
    /// </summary>
    protected void Guard(string operation, Action action)
    {
        ErrorMessage = string.Empty;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            OperationLog.Failure(operation, ex);
            OnOperationFailed(operation, ex);
        }
    }

    protected async Task GuardAsync(string operation, Func<Task> action)
    {
        ErrorMessage = string.Empty;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            OperationLog.Failure(operation, ex);
            OnOperationFailed(operation, ex);
        }
    }

    protected virtual void OnOperationFailed(string operation, Exception exception) =>
        ErrorMessage = exception.Message;
}
