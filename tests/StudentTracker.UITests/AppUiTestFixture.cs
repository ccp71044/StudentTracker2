using System.Diagnostics;
using FlaUI.Core;
using FlaUI.UIA3;

namespace StudentTracker.UITests;

public class AppUiTestFixture : IDisposable
{
    private readonly string _dataDirectory;
    private readonly Process _process;
    private bool _disposed;

    public Application App { get; }
    public UIA3Automation Automation { get; }

    public AppUiTestFixture()
    {
        _dataDirectory = Path.Combine(Path.GetTempPath(), $"StudentTrackerUITest-{Guid.NewGuid()}");
        Directory.CreateDirectory(_dataDirectory);

        var exePath = FindExePath();
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException("Could not locate StudentTracker.exe. Run 'dotnet publish' first.", "StudentTracker.exe");

        var startInfo = new ProcessStartInfo(exePath, "--sample-data")
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
        };
        startInfo.EnvironmentVariables["LOCALAPPDATA"] = _dataDirectory;

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the WPF application.");
        // Wait briefly so the process main module is available before attaching.
        _process.WaitForInputIdle(5000);
        try
        {
            _process.WaitForExit(1);
        }
        catch { /* ignore: process still running */ }

        App = Application.Attach(_process);
        Automation = new UIA3Automation();
    }

    public FlaUI.Core.AutomationElements.Window GetMainWindow(TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);
        var start = DateTime.UtcNow;
        Exception? lastError = null;

        while (DateTime.UtcNow - start < maxWait)
        {
            try
            {
                var window = App.GetMainWindow(Automation);
                if (window != null && !string.IsNullOrEmpty(window.Name))
                    return window;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException("Main window did not appear in time.", lastError);
    }

    private static string FindExePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "StudentTracker.Wpf", "bin", "Release", "net8.0-windows", "StudentTracker.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "StudentTracker.Wpf", "bin", "Release", "net8.0-windows", "win-x64", "StudentTracker.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "release", "StudentTracker-win-x64", "StudentTracker.exe"),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
                return full;
        }

        return string.Empty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { Automation.Dispose(); } catch { /* ignored */ }

        try
        {
            if (!_process.HasExited)
            {
                App.Close();
                if (!_process.WaitForExit(5000))
                    _process.Kill();
            }
        }
        catch { /* ignored */ }

        try { Directory.Delete(_dataDirectory, true); } catch { /* ignored */ }
    }
}
