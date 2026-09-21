using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using Xunit;

namespace StudentTracker.UITests;

public class ToolbarDiscoverabilityTests : IClassFixture<AppUiTestFixture>, IDisposable
{
    private readonly AppUiTestFixture _fixture;
    private readonly Window _mainWindow;

    public ToolbarDiscoverabilityTests(AppUiTestFixture fixture)
    {
        _fixture = fixture;
        _mainWindow = fixture.GetMainWindow(TimeSpan.FromSeconds(30));
        Thread.Sleep(500);
    }

    public void Dispose()
    {
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

    [Theory]
    [InlineData("StudentsButton", "StudentsHeader", "AddStudentButton")]
    [InlineData("StudentsButton", "StudentsHeader", "EditStudentButton")]
    [InlineData("StudentsButton", "StudentsHeader", "StudentsAddAllocationButton")]
    [InlineData("StudentsButton", "StudentsHeader", "RefreshStudentsButton")]
    [InlineData("CoursesButton", "CoursesHeader", "AddCourseButton")]
    [InlineData("CoursesButton", "CoursesHeader", "EditCourseButton")]
    [InlineData("CoursesButton", "CoursesHeader", "CoursesAddDeliveryButton")]
    [InlineData("CoursesButton", "CoursesHeader", "RefreshCoursesButton")]
    [InlineData("DeliveriesButton", "DeliveriesHeader", "AddDeliveryButton")]
    [InlineData("DeliveriesButton", "DeliveriesHeader", "EditDeliveryButton")]
    [InlineData("DeliveriesButton", "DeliveriesHeader", "DeliveriesAddAllocationButton")]
    [InlineData("DeliveriesButton", "DeliveriesHeader", "RefreshDeliveriesButton")]
    [InlineData("AllocationsButton", "AllocationsHeader", "AddAllocationButton")]
    [InlineData("AllocationsButton", "AllocationsHeader", "EditAllocationButton")]
    [InlineData("AllocationsButton", "AllocationsHeader", "MarkAttendanceButton")]
    [InlineData("AllocationsButton", "AllocationsHeader", "RefreshAllocationsButton")]
    [InlineData("CertificatesButton", "CertificatesHeader", "CertificatesNewOrderButton")]
    [InlineData("CertificatesButton", "CertificatesHeader", "RefreshCertificatesButton")]
    [InlineData("DocumentsButton", "DocumentsHeader", "AddDocumentButton")]
    [InlineData("DocumentsButton", "DocumentsHeader", "EditDocumentButton")]
    [InlineData("DocumentsButton", "DocumentsHeader", "RefreshDocumentsButton")]
    [InlineData("CreditsBudgetsButton", "CreditsBudgetsHeader", "RefreshCreditsBudgetsButton")]
    [InlineData("ImportExportButton", "ImportExportHeader", "CreateBackupButton")]
    [InlineData("ImportExportButton", "ImportExportHeader", "ImportMigrationPackageButton")]
    public void View_PrimaryToolbarButton_IsDiscoverable(string navButtonId, string headerId, string toolbarButtonId)
    {
        ClickButton(navButtonId);

        var header = Retry.Find(
            () => FindByAutomationId(_fixture.App.GetMainWindow(_fixture.Automation), headerId),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });
        Assert.NotNull(header);

        var button = Retry.Find(
            () => FindByAutomationId(_fixture.App.GetMainWindow(_fixture.Automation), toolbarButtonId),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });
        Assert.NotNull(button);
    }

    [Fact]
    public void ReportsView_ReportSelectorAndExportMenu_AreDiscoverable()
    {
        ClickButton("ReportsButton");

        var mainWindow = _fixture.App.GetMainWindow(_fixture.Automation);
        var selector = Retry.Find(
            () => FindByAutomationId(mainWindow, "ReportsListBox"),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });
        Assert.NotNull(selector);

        var exportMenu = Retry.Find(
            () => FindByAutomationId(mainWindow, "ReportsExportMenu"),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });
        Assert.NotNull(exportMenu);
    }

    private void ClickButton(string automationId)
    {
        var element = Retry.Find(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) });

        Assert.True(element != null, $"Navigation button {automationId} was not found.");

        element!.AsButton().Invoke();
        Wait.UntilInputIsProcessed();
        Thread.Sleep(1000);
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
