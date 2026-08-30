using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using Xunit;

namespace StudentTracker.UITests;

public class MenuTests : IClassFixture<AppUiTestFixture>, IDisposable
{
    private readonly AppUiTestFixture _fixture;
    private readonly Window _mainWindow;

    public MenuTests(AppUiTestFixture fixture)
    {
        _fixture = fixture;
        _mainWindow = fixture.GetMainWindow(TimeSpan.FromSeconds(30));
        Thread.Sleep(500);
    }

    public void Dispose()
    {
        try
        {
            Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
            var element = Retry.Find(
                () => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DashboardButton")),
                new RetrySettings { Timeout = TimeSpan.FromSeconds(2), Interval = TimeSpan.FromMilliseconds(200) });
            element?.AsButton().Invoke();
            Wait.UntilInputIsProcessed();
        }
        catch { /* ignored */ }
    }

    [Theory]
    [InlineData("FileMenu")]
    [InlineData("ActionsMenu")]
    [InlineData("DataMenu")]
    [InlineData("ViewMenu")]
    [InlineData("ToolsMenu")]
    [InlineData("HelpMenu")]
    public void TopLevelMenu_IsPresent(string automationId)
    {
        var menuItem = FindByAutomationId(_mainWindow, automationId);
        Assert.NotNull(menuItem);
    }

    [Theory]
    [InlineData("FileMenu", "FileMenu_ImportMigrationPackage")]
    [InlineData("FileMenu", "FileMenu_BackupNow")]
    [InlineData("FileMenu", "FileMenu_RestoreBackup")]
    [InlineData("FileMenu", "FileMenu_Exit")]
    [InlineData("ActionsMenu", "ActionsMenu_RefreshCurrentView")]
    [InlineData("DataMenu", "DataMenu_BackupNow")]
    [InlineData("DataMenu", "DataMenu_RestoreBackup")]
    [InlineData("DataMenu", "DataMenu_CompactDatabase")]
    [InlineData("ToolsMenu", "ToolsMenu_OpenDataFolder")]
    [InlineData("ToolsMenu", "ToolsMenu_OpenBackupsFolder")]
    [InlineData("ToolsMenu", "ToolsMenu_OpenExportsFolder")]
    [InlineData("ToolsMenu", "ToolsMenu_OpenLogsFolder")]
    [InlineData("ToolsMenu", "ToolsMenu_CompactDatabase")]
    [InlineData("HelpMenu", "HelpMenu_Documentation")]
    [InlineData("HelpMenu", "HelpMenu_About")]
    public void MenuItem_IsPresent(string parentMenuId, string menuItemId)
    {
        var parent = FindByAutomationId(_mainWindow, parentMenuId);
        Assert.NotNull(parent);

        ExpandMenu(parent!);

        var child = FindByAutomationId(_fixture.Automation.GetDesktop(), menuItemId);
        Assert.NotNull(child);
        parent!.AsMenuItem().Collapse();
    }

    [Fact]
    public void ViewMenu_CanNavigateToDashboard()
    {
        var studentsButton = FindByAutomationId(_mainWindow, "StudentsButton");
        Assert.NotNull(studentsButton);
        studentsButton!.AsButton().Invoke();
        Wait.UntilInputIsProcessed();
        Thread.Sleep(500);

        var viewMenu = FindByAutomationId(_mainWindow, "ViewMenu");
        Assert.NotNull(viewMenu);

        ExpandMenu(viewMenu!);

        var dashboardItem = FindByAutomationId(_fixture.Automation.GetDesktop(), "ViewMenu_Dashboard");
        Assert.NotNull(dashboardItem);

        InvokeMenuItem(dashboardItem!);
        Wait.UntilInputIsProcessed();
        Thread.Sleep(1000);

        var header = Retry.Find(
            () => FindByAutomationId(_fixture.App.GetMainWindow(_fixture.Automation), "DashboardHeader"),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });
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
