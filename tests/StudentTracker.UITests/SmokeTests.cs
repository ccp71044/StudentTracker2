using Xunit;

namespace StudentTracker.UITests;

/// <summary>
/// The first suite to run on a fresh machine: it proves the executable starts, creates its data
/// tree in the throwaway root and writes a log, so later failures are behavioural rather than
/// environmental.
/// </summary>
public class SmokeTests : UiTest
{
    public SmokeTests(AppUiTestFixture fixture) : base(fixture) { }

    [Fact]
    public void Application_Starts_AndShowsItsMainWindow() => Run(() =>
    {
        var window = Fixture.GetMainWindow(TimeSpan.FromSeconds(30));
        Assert.Contains("Student Tracker", window.Title, StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public void Application_UsesTheThrowawayDataRoot() => Run(() =>
    {
        Fixture.GetMainWindow();

        var database = Path.Combine(Fixture.DataDirectory, "Database", "student-tracker.db");
        Assert.True(File.Exists(database), $"No database was created under the test data root: {Fixture.DataDirectory}");

        foreach (var folder in new[] { "Documents", "Imports", "Exports", "Integration", "Backups", "Logs", "Templates" })
        {
            Assert.True(
                Directory.Exists(Path.Combine(Fixture.DataDirectory, folder)),
                $"{folder} was not created under the test data root.");
        }
    });

    [Fact]
    public void Application_LogsItsStartUp() => Run(() =>
    {
        Fixture.GetMainWindow();
        Assert.Contains("Starting Student Tracker", Fixture.LogText(), StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public void Dashboard_ShowsSampleData() => Run(() =>
    {
        var window = Fixture.Navigate("DashboardButton", "DashboardHeader");
        var pools = Fixture.WaitFor("DashboardPoolsGrid", TimeSpan.FromSeconds(10));
        Assert.NotNull(pools);
        Assert.NotNull(window);
    });
}
