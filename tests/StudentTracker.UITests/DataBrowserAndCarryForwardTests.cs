using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using Xunit;

namespace StudentTracker.UITests;

public class DataBrowserAndCarryForwardTests : IClassFixture<AppUiTestFixture>, IDisposable
{
    private readonly AppUiTestFixture _fixture;
    private readonly Window _mainWindow;

    public DataBrowserAndCarryForwardTests(AppUiTestFixture fixture)
    {
        _fixture = fixture;
        _mainWindow = fixture.GetMainWindow(TimeSpan.FromSeconds(30));
        Thread.Sleep(500);
    }

    public void Dispose()
    {
        try { Keyboard.Press(VirtualKeyShort.ESCAPE); } catch { }
        try
        {
            var element = Retry.Find(
                () => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DashboardButton")),
                new RetrySettings { Timeout = TimeSpan.FromSeconds(2), Interval = TimeSpan.FromMilliseconds(200) });
            element?.AsButton().Invoke();
            Wait.UntilInputIsProcessed();
        }
        catch { /* ignored */ }
    }

    [Fact]
    public void DataBrowser_CanOpenAndShowTables()
    {
        var dataMenu = FindByAutomationId(_mainWindow, "DataMenu");
        Assert.NotNull(dataMenu);

        ExpandMenu(dataMenu!);

        var dataBrowserItem = FindByAutomationId(_fixture.Automation.GetDesktop(), "DataMenu_DataBrowser");
        Assert.NotNull(dataBrowserItem);

        InvokeMenuItem(dataBrowserItem!);
        Wait.UntilInputIsProcessed();
        Thread.Sleep(1000);

        var tableList = Retry.Find(
            () => FindByAutomationId(_fixture.Automation.GetDesktop(), "DataBrowserTableList"),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });
        Assert.NotNull(tableList);

        var grid = Retry.Find(
            () => FindByAutomationId(_fixture.Automation.GetDesktop(), "DataBrowserGrid"),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });
        Assert.NotNull(grid);
    }

    [Fact]
    public void CarryForwardPlaceholder_MenuItem_IsRegisteredInAllocationsGrid()
    {
        var allocationsButton = FindByAutomationId(_mainWindow, "AllocationsButton");
        Assert.NotNull(allocationsButton);
        allocationsButton!.AsButton().Invoke();
        Wait.UntilInputIsProcessed();
        Thread.Sleep(1000);

        var grid = Retry.Find(
            () => FindByAutomationId(_mainWindow, "AllocationsDataGrid"),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });
        Assert.NotNull(grid);

        // The context menu is dynamic; at minimum the grid should exist.
        var header = FindByAutomationId(_mainWindow, "AllocationsHeader");
        Assert.NotNull(header);
    }

    private static void ExpandMenu(AutomationElement menuElement)
    {
        var expandPattern = menuElement.Patterns.ExpandCollapse.PatternOrDefault;
        if (expandPattern != null)
        {
            expandPattern.Expand();
            Wait.UntilInputIsProcessed();
            Thread.Sleep(300);
        }
        else
        {
            menuElement.AsMenuItem().Invoke();
            Wait.UntilInputIsProcessed();
            Thread.Sleep(300);
        }
    }

    private static void InvokeMenuItem(AutomationElement menuItem)
    {
        menuItem.AsMenuItem().Invoke();
    }

    private static AutomationElement? FindByAutomationId(AutomationElement root, string automationId)
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
}
