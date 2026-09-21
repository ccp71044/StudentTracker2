using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace StudentTracker.UITests;

/// <summary>
/// Starts one copy of Student Tracker for a whole test class, pointed at a throwaway data root so
/// a run can never read or write the operator's real database, documents or backups.
/// </summary>
public class AppUiTestFixture : IDisposable
{
    private readonly Process _process;
    private bool _disposed;

    /// <summary>The temporary %LOCALAPPDATA%-equivalent the application was told to use.</summary>
    public string DataDirectory { get; }

    /// <summary>Where screenshots and copied logs are left for inspection after a failing run.</summary>
    public string ArtifactsDirectory { get; }

    public Application App { get; }
    public UIA3Automation Automation { get; }

    public AppUiTestFixture()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), $"StudentTrackerUITest-{Guid.NewGuid()}");
        Directory.CreateDirectory(DataDirectory);

        ArtifactsDirectory = Path.Combine(AppContext.BaseDirectory, "ui-test-artifacts");
        Directory.CreateDirectory(ArtifactsDirectory);

        var exePath = FindExePath();
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException(
                "Could not locate StudentTracker.exe. Run scripts/run-ui-tests.ps1, or 'dotnet publish' first.",
                "StudentTracker.exe");

        // --data-root is honoured by the application itself; LOCALAPPDATA is set as well so any
        // framework component that ignores our settings still lands inside the throwaway folder.
        var startInfo = new ProcessStartInfo(exePath, $"--sample-data --data-root \"{DataDirectory}\"")
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
        };
        startInfo.EnvironmentVariables["LOCALAPPDATA"] = DataDirectory;

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the WPF application.");
        _process.WaitForInputIdle(10000);

        if (_process.HasExited)
            throw new InvalidOperationException(
                $"Student Tracker exited during start-up with code {_process.ExitCode}. Log: {LogText()}");

        App = Application.Attach(_process);
        Automation = new UIA3Automation();
    }

    public Window GetMainWindow(TimeSpan? timeout = null)
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

        throw new TimeoutException($"Main window did not appear in time. Log: {LogText()}", lastError);
    }

    /// <summary>Clicks a top-level navigation button and waits for the section header to appear.</summary>
    public Window Navigate(string navigationButtonId, string expectedHeaderId)
    {
        var window = GetMainWindow();
        Click(window, navigationButtonId);
        WaitFor(expectedHeaderId, TimeSpan.FromSeconds(10));
        return GetMainWindow();
    }

    public AutomationElement? Find(string automationId, TimeSpan? timeout = null) =>
        Retry.Find(
            () => GetMainWindow().FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            new RetrySettings { Timeout = timeout ?? TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) })
            as AutomationElement;

    public AutomationElement WaitFor(string automationId, TimeSpan? timeout = null) =>
        Find(automationId, timeout) ?? throw new TimeoutException($"'{automationId}' never appeared in the automation tree.");

    public void Click(AutomationElement scope, string automationId)
    {
        var element = Retry.Find(
            () => scope.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) })
            ?? throw new InvalidOperationException($"Button '{automationId}' was not found in the automation tree.");

        element.AsButton().Invoke();
        Wait.UntilInputIsProcessed();
        Thread.Sleep(500);
    }

    /// <summary>The dialog Student Tracker opens for add/edit screens, if one is showing.</summary>
    public Window? ModalDialog(TimeSpan? timeout = null) =>
        Retry.Find(
            () => GetMainWindow().ModalWindows.FirstOrDefault(),
            new RetrySettings { Timeout = timeout ?? TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) })
            as Window;

    /// <summary>Saves a PNG of the desktop, named after the test, and returns its path.</summary>
    public string Screenshot(string name)
    {
        var path = Path.Combine(ArtifactsDirectory, $"{Sanitise(name)}.png");
        try
        {
            Capture.Screen().ToFile(path);
        }
        catch
        {
            return string.Empty;
        }

        return path;
    }

    /// <summary>Everything the application logged during this run, for failure messages.</summary>
    public string LogText()
    {
        try
        {
            var logs = LogFiles();
            return logs.Count == 0 ? "(no log written)" : string.Join("\n", logs.Select(ReadShared));
        }
        catch (Exception ex)
        {
            return $"(logs unreadable: {ex.Message})";
        }
    }

    /// <summary>
    /// Logs live under the data root the application was given; the LOCALAPPDATA fall-back path is
    /// searched too, in case a build without --data-root support is under test.
    /// </summary>
    private List<string> LogFiles() =>
        new[] { Path.Combine(DataDirectory, "Logs"), Path.Combine(DataDirectory, "StudentTracker", "Logs") }
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.log"))
            .ToList();

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Sanitise(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static string FindExePath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("STUDENTTRACKER_EXE");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
            return fromEnvironment;

        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        var candidates = new[]
        {
            Path.Combine(root, "release", "StudentTracker-win-x64", "StudentTracker.exe"),
            Path.Combine(root, "src", "StudentTracker.Wpf", "bin", "Release", "net8.0-windows", "win-x64", "publish", "StudentTracker.exe"),
            Path.Combine(root, "src", "StudentTracker.Wpf", "bin", "Release", "net8.0-windows", "win-x64", "StudentTracker.exe"),
            Path.Combine(root, "src", "StudentTracker.Wpf", "bin", "Release", "net8.0-windows", "StudentTracker.exe"),
            Path.Combine(root, "src", "StudentTracker.Wpf", "bin", "Debug", "net8.0-windows", "StudentTracker.exe"),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
                return full;
        }

        return string.Empty;
    }

    /// <summary>Copies the run's logs next to the screenshots so a failure can be diagnosed later.</summary>
    private void CollectLogs()
    {
        try
        {
            foreach (var log in LogFiles())
            {
                File.WriteAllText(Path.Combine(ArtifactsDirectory, Path.GetFileName(log)), ReadShared(log));
            }
        }
        catch { /* diagnostics only */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CollectLogs();

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

        try { Directory.Delete(DataDirectory, true); } catch { /* ignored */ }
        GC.SuppressFinalize(this);
    }
}
