using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using Xunit;

namespace StudentTracker.UITests;

public class NavigationTests : IClassFixture<AppUiTestFixture>, IDisposable
{
    private readonly AppUiTestFixture _fixture;
    private readonly Window _mainWindow;

    public NavigationTests(AppUiTestFixture fixture)
    {
        _fixture = fixture;
        _mainWindow = fixture.GetMainWindow(TimeSpan.FromSeconds(30));
        // Give the window time to fully render its visual tree.
        Thread.Sleep(500);
    }

    public void Dispose()
    {
        // Return to dashboard after each test to leave the app in a known state.
        try
        {
            var element = Retry.Find(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DashboardButton")),
                new RetrySettings { Timeout = TimeSpan.FromSeconds(2), Interval = TimeSpan.FromMilliseconds(200) });
            element?.AsButton().Invoke();
            Wait.UntilInputIsProcessed();
        }
        catch { /* ignored */ }
    }

    [Fact]
    public void MainWindow_IsVisible_WithCorrectTitle()
    {
        Assert.Contains("Student Tracker", _mainWindow.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("StudentsButton", "StudentsHeader")]
    [InlineData("CoursesButton", "CoursesHeader")]
    [InlineData("DeliveriesButton", "DeliveriesHeader")]
    [InlineData("AllocationsButton", "AllocationsHeader")]
    [InlineData("CertificatesButton", "CertificatesHeader")]
    [InlineData("CreditsBudgetsButton", "CreditsBudgetsHeader")]
    [InlineData("DocumentsButton", "DocumentsHeader")]
    [InlineData("ReportsButton", "ReportsHeader")]
    [InlineData("ImportExportButton", "ImportExportHeader")]
    [InlineData("SettingsButton", "SettingsHeader")]
    public void NavigatingToView_LoadsExpectedView(string buttonAutomationId, string expectedHeaderAutomationId)
    {
        ClickButton(buttonAutomationId);

        // Allow WPF to render the new view and for its header to enter the automation tree.
        Thread.Sleep(1500);

        var window = _fixture.App.GetMainWindow(_fixture.Automation);
        var header = FindByAutomationId(window, expectedHeaderAutomationId);
        Assert.NotNull(header);
    }

    private static FlaUI.Core.AutomationElements.AutomationElement? FindByAutomationId(FlaUI.Core.AutomationElements.AutomationElement root, string automationId)
    {
        try
        {
            if (root.AutomationId == automationId)
                return root;
        }
        catch { /* property may not be supported */ }

        foreach (var child in root.FindAllChildren())
        {
            var match = FindByAutomationId(child, automationId);
            if (match != null)
                return match;
        }

        return null;
    }

    private void ClickButton(string automationId)
    {
        var element = Retry.Find(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });

        Assert.True(element != null, $"Button {automationId} was not found in the automation tree.");

        element!.AsButton().Invoke();
        Wait.UntilInputIsProcessed();
        Thread.Sleep(1500); // allow the view transition to complete
    }
}
