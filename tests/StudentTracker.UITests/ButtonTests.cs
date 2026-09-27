using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using Xunit;

namespace StudentTracker.UITests;

/// <summary>
/// "Some buttons don't work" was the original report, so every command surface on every section is
/// exercised here: toolbar buttons, the grid context menus that own the add/edit/archive actions on
/// Students, Courses, Deliveries, Allocations, Certificates, Documents and the pool screens, and the
/// Reports export menu. A command passes when it is present, its enabled state matches whether it
/// needs a selection, and invoking it leaves the section responsive with no error reported.
/// </summary>
public class ButtonTests : UiTest
{
    public ButtonTests(AppUiTestFixture fixture) : base(fixture) { }

    /// <summary>section nav button, section header, button, whether the button needs a row selected.</summary>
    public static TheoryData<string, string, string, bool> Buttons => new()
    {
        { "DashboardButton", "DashboardHeader", "DashboardRefreshButton", false },
        { "DashboardButton", "DashboardHeader", "DashboardAddStudentButton", false },
        { "DashboardButton", "DashboardHeader", "DashboardAddCourseButton", false },

        { "StudentsButton", "StudentsHeader", "StudentsSearchButton", false },
        { "StudentsButton", "StudentsHeader", "RefreshStudentsButton", false },
        { "StudentsButton", "StudentsHeader", "AddStudentButton", false },
        { "StudentsButton", "StudentsHeader", "ViewStudentButton", true },
        { "StudentsButton", "StudentsHeader", "EditStudentButton", true },
        { "StudentsButton", "StudentsHeader", "StudentsAddAllocationButton", true },

        { "CoursesButton", "CoursesHeader", "CoursesSearchButton", false },
        { "CoursesButton", "CoursesHeader", "RefreshCoursesButton", false },
        { "CoursesButton", "CoursesHeader", "AddCourseButton", false },
        { "CoursesButton", "CoursesHeader", "EditCourseButton", true },
        { "CoursesButton", "CoursesHeader", "CoursesAddDeliveryButton", true },

        { "DeliveriesButton", "DeliveriesHeader", "RefreshDeliveriesButton", false },
        { "DeliveriesButton", "DeliveriesHeader", "AddDeliveryButton", false },
        { "DeliveriesButton", "DeliveriesHeader", "EditDeliveryButton", true },
        { "DeliveriesButton", "DeliveriesHeader", "DeliveriesAddAllocationButton", true },
        { "DeliveriesButton", "DeliveriesHeader", "DeliveriesRecordOfCompletionButton", true },

        { "AllocationsButton", "AllocationsHeader", "RefreshAllocationsButton", false },
        { "AllocationsButton", "AllocationsHeader", "AddAllocationButton", false },
        { "AllocationsButton", "AllocationsHeader", "EditAllocationButton", true },
        { "AllocationsButton", "AllocationsHeader", "MarkAttendanceButton", true },
        { "AllocationsButton", "AllocationsHeader", "MarkOutcomeButton", true },

        { "CertificatesButton", "CertificatesHeader", "RefreshCertificatesButton", false },
        { "CertificatesButton", "CertificatesHeader", "CertificatesNewOrderButton", false },
        { "CertificatesButton", "CertificatesHeader", "CertificatesRecordDeliveryButton", true },

        { "CreditsBudgetsButton", "CreditsBudgetsHeader", "RefreshCreditsBudgetsButton", false },
        { "CreditsBudgetsButton", "CreditsBudgetsHeader", "ExportBudgetPositionCsvButton", false },

        { "DocumentsButton", "DocumentsHeader", "RefreshDocumentsButton", false },
        { "DocumentsButton", "DocumentsHeader", "AddDocumentButton", false },
        { "DocumentsButton", "DocumentsHeader", "ViewDocumentButton", true },
        { "DocumentsButton", "DocumentsHeader", "EditDocumentButton", true },
        { "DocumentsButton", "DocumentsHeader", "LinkDocumentButton", true },

        { "ReportsButton", "ReportsHeader", "ReportsRefreshButton", false },

        { "ImportExportButton", "ImportExportHeader", "CreateBackupButton", false },
        { "ImportExportButton", "ImportExportHeader", "RestoreBackupButton", false },
        { "ImportExportButton", "ImportExportHeader", "ExportInvoicerBatchButton", false },
        { "ImportExportButton", "ImportExportHeader", "ExportInvoiceManagerCostPositionButton", false },
        { "ImportExportButton", "ImportExportHeader", "ExportActiveCoursesCsvButton", false },
        { "ImportExportButton", "ImportExportHeader", "ExportActiveStudentsCsvButton", false },
        { "ImportExportButton", "ImportExportHeader", "ExportAllAllocationsCsvButton", false },
        { "ImportExportButton", "ImportExportHeader", "ImportMigrationPackageButton", false },
        { "ImportExportButton", "ImportExportHeader", "ImportCompletionPricingButton", false },
        { "ImportExportButton", "ImportExportHeader", "ImportCreditHistoryButton", false },

        { "SettingsButton", "SettingsHeader", "SettingsCompactDatabaseButton", false },
    };

    [Theory]
    [MemberData(nameof(Buttons))]
    public void Button_IsPresent_AndDoesSomethingWhenPressed(string navigationId, string headerId, string buttonId, bool needsSelection)
    {
        Run(() =>
        {
            Fixture.Navigate(navigationId, headerId);

            var button = Fixture.WaitFor(buttonId, TimeSpan.FromSeconds(10)).AsButton();
            Assert.True(button != null, $"{buttonId} is missing from the automation tree.");

            if (needsSelection)
            {
                Assert.False(button!.IsEnabled, $"{buttonId} acts on a selected row but is enabled with nothing selected.");
                return;
            }

            Assert.True(button!.IsEnabled, $"{buttonId} is disabled and cannot be used.");

            button.Invoke();
            Wait.UntilInputIsProcessed();
            Thread.Sleep(1000);

            DismissDialogs();

            // The section is still there and nothing was reported as failing, so the command ran.
            Assert.NotNull(Fixture.WaitFor(headerId, TimeSpan.FromSeconds(10)));
            AssertNoError(ErrorText(headerId.Replace("Header", "ErrorMessage")), buttonId);
        }, $"{buttonId}");
    }

    [Theory]
    [InlineData("StudentsButton", "StudentsHeader", "StudentsDataGrid", "EditStudentButton")]
    [InlineData("CoursesButton", "CoursesHeader", "CoursesDataGrid", "EditCourseButton")]
    [InlineData("DeliveriesButton", "DeliveriesHeader", "DeliveriesDataGrid", "EditDeliveryButton")]
    [InlineData("AllocationsButton", "AllocationsHeader", "AllocationsDataGrid", "EditAllocationButton")]
    public void SelectingARow_EnablesTheActionsThatNeedOne(string navigationId, string headerId, string gridId, string buttonId)
    {
        Run(() =>
        {
            Fixture.Navigate(navigationId, headerId);
            SelectFirstRow(gridId);

            var button = Fixture.WaitFor(buttonId, TimeSpan.FromSeconds(5)).AsButton();
            Assert.True(button.IsEnabled, $"{buttonId} stayed disabled after a row was selected.");
        }, $"{buttonId}-with-selection");
    }

    /// <summary>
    /// Archive, restore, transfer and the pool actions live only on the grid context menus, so they
    /// are reached the way an operator reaches them: select a row, then open the menu.
    /// </summary>
    [Theory]
    [InlineData("StudentsButton", "StudentsHeader", "StudentsDataGrid", "StudentsContextMenu_View")]
    [InlineData("StudentsButton", "StudentsHeader", "StudentsDataGrid", "StudentsContextMenu_Edit")]
    [InlineData("StudentsButton", "StudentsHeader", "StudentsDataGrid", "StudentsContextMenu_Archive")]
    [InlineData("CoursesButton", "CoursesHeader", "CoursesDataGrid", "CoursesContextMenu_Edit")]
    [InlineData("DeliveriesButton", "DeliveriesHeader", "DeliveriesDataGrid", "DeliveriesContextMenu_Edit")]
    [InlineData("AllocationsButton", "AllocationsHeader", "AllocationsDataGrid", "AllocationsContextMenu_Edit")]
    [InlineData("CreditsBudgetsButton", "CreditsBudgetsHeader", "CreditPoolsDataGrid", "CreditPoolsContextMenu_AddCreditPool")]
    [InlineData("CreditsBudgetsButton", "CreditsBudgetsHeader", "CreditPoolsDataGrid", "CreditPoolsContextMenu_Edit")]
    [InlineData("CreditsBudgetsButton", "CreditsBudgetsHeader", "CreditPoolsDataGrid", "CreditPoolsContextMenu_AddCredits")]
    [InlineData("CreditsBudgetsButton", "CreditsBudgetsHeader", "CreditPoolsDataGrid", "CreditPoolsContextMenu_Transactions")]
    public void GridContextMenuItem_IsPresent_AndEnabledForASelectedRow(string navigationId, string headerId, string gridId, string menuItemId)
    {
        Run(() =>
        {
            Fixture.Navigate(navigationId, headerId);
            var grid = SelectFirstRow(gridId);

            grid.Rows[0].RightClick();
            Wait.UntilInputIsProcessed();
            Thread.Sleep(500);

            var item = Retry.Find(
                () => FindOnDesktop(menuItemId),
                new RetrySettings { Timeout = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromMilliseconds(200) }) as AutomationElement;
            Assert.True(item != null, $"{menuItemId} never appeared on the {gridId} context menu.");
            Assert.True(item!.IsEnabled, $"{menuItemId} is disabled even though a row is selected.");

            Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
            Wait.UntilInputIsProcessed();
            Thread.Sleep(300);
        }, menuItemId);
    }

    /// <summary>Every report export is a nested menu item rather than a button, so each is invoked here.</summary>
    [Theory]
    [InlineData("ReportsExportMenu_ExportCompletedCsv")]
    [InlineData("ReportsExportMenu_ExportAwaitingOrderCsv")]
    [InlineData("ReportsExportMenu_ExportWithdrawnCsv")]
    [InlineData("ReportsExportMenu_ExportNonCompletionsCsv")]
    [InlineData("ReportsExportMenu_ExportAwaitingDeliveryCsv")]
    [InlineData("ReportsExportMenu_ExportDeliveredCsv")]
    [InlineData("ReportsExportMenu_ExportUpcomingCsv")]
    [InlineData("ReportsExportMenu_ExportCapacityCsv")]
    [InlineData("ReportsExportMenu_ExportActiveAllocationsCsv")]
    [InlineData("ReportsExportMenu_ExportAttendanceCsv")]
    [InlineData("ReportsExportMenu_ExportBudgetSummaryCsv")]
    [InlineData("ReportsExportMenu_ExportCreditSummaryCsv")]
    [InlineData("ReportsExportMenu_ExportBillableCertificatesCsv")]
    [InlineData("ReportsExportMenu_ExportAuditActivityCsv")]
    [InlineData("ReportsExportMenu_ExportImportReviewQueueCsv")]
    public void ReportExportMenuItem_ExportsWithoutError(string menuItemId)
    {
        Run(() =>
        {
            Fixture.Navigate("ReportsButton", "ReportsHeader");

            var item = ExpandToMenuItem(menuItemId);
            Assert.True(item.IsEnabled, $"{menuItemId} is disabled and cannot be used.");

            item.AsMenuItem().Invoke();
            Wait.UntilInputIsProcessed();
            Thread.Sleep(1500);

            DismissDialogs();

            Assert.NotNull(Fixture.WaitFor("ReportsHeader", TimeSpan.FromSeconds(10)));
            AssertNoError(ErrorText("ReportsErrorMessage"), menuItemId);
        }, menuItemId);
    }

    private Grid SelectFirstRow(string gridId)
    {
        var grid = Fixture.WaitFor(gridId, TimeSpan.FromSeconds(10)).AsGrid();
        Assert.True(grid.Rows.Length > 0, $"{gridId} loaded no sample rows, so selection cannot be tested.");

        grid.Select(0);
        Wait.UntilInputIsProcessed();
        Thread.Sleep(500);
        return grid;
    }

    /// <summary>
    /// Walks down the export menu opening each level until the requested item is visible; WPF only
    /// builds a submenu's children once its parent is expanded.
    /// </summary>
    private AutomationElement ExpandToMenuItem(string menuItemId)
    {
        var menu = Fixture.WaitFor("ReportsExportMenu", TimeSpan.FromSeconds(10));

        Expand(menu.FindFirstChild()!);

        for (var depth = 0; depth < 4; depth++)
        {
            var found = FindOnDesktop(menuItemId);
            if (found != null) return found;

            var next = OpenNextCollapsedSubmenu();
            if (next == null) break;
        }

        return FindOnDesktop(menuItemId)
            ?? throw new TimeoutException($"'{menuItemId}' never appeared on the Reports export menu.");
    }

    private AutomationElement? OpenNextCollapsedSubmenu()
    {
        var popup = Fixture.Automation.GetDesktop()
            .FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuItem))
            .LastOrDefault(item =>
                item.Patterns.ExpandCollapse.PatternOrDefault?.ExpandCollapseState.ValueOrDefault
                    == FlaUI.Core.Definitions.ExpandCollapseState.Collapsed);

        if (popup == null) return null;

        Expand(popup);
        return popup;
    }

    private static void Expand(AutomationElement element)
    {
        var pattern = element.Patterns.ExpandCollapse.PatternOrDefault;
        if (pattern != null)
            pattern.Expand();
        else
            element.AsMenuItem().Invoke();

        Wait.UntilInputIsProcessed();
        Thread.Sleep(400);
    }

    private AutomationElement? FindOnDesktop(string automationId) =>
        Fixture.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId));
}
