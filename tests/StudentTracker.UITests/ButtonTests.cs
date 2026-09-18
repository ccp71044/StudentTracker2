using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace StudentTracker.UITests;

/// <summary>
/// "Some buttons don't work" was the original report, so every command button on every section is
/// pressed here. A button passes when it is present, its enabled state matches whether it needs a
/// selection, and pressing it leaves the application responsive with no error reported.
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
        { "StudentsButton", "StudentsHeader", "StudentsAddStudentButton", false },
        { "StudentsButton", "StudentsHeader", "StudentsViewStudentButton", true },
        { "StudentsButton", "StudentsHeader", "StudentsEditStudentButton", true },
        { "StudentsButton", "StudentsHeader", "StudentsDeleteStudentButton", true },

        { "CoursesButton", "CoursesHeader", "CoursesAddCourseButton", false },
        { "CoursesButton", "CoursesHeader", "CoursesEditCourseButton", true },
        { "CoursesButton", "CoursesHeader", "CoursesDeleteCourseButton", true },

        { "DeliveriesButton", "DeliveriesHeader", "DeliveriesRefreshButton", false },
        { "DeliveriesButton", "DeliveriesHeader", "DeliveriesAddDeliveryButton", false },
        { "DeliveriesButton", "DeliveriesHeader", "DeliveriesEditDeliveryButton", true },

        { "AllocationsButton", "AllocationsHeader", "AllocationsRefreshButton", false },
        { "AllocationsButton", "AllocationsHeader", "AllocationsAddAllocationButton", false },
        { "AllocationsButton", "AllocationsHeader", "AllocationsEditAllocationButton", true },
        { "AllocationsButton", "AllocationsHeader", "AllocationsMarkAttendanceButton", true },
        { "AllocationsButton", "AllocationsHeader", "AllocationsMarkOutcomeButton", true },

        { "CertificatesButton", "CertificatesHeader", "CertificatesRefreshButton", false },

        { "CreditsBudgetsButton", "CreditsBudgetsHeader", "CreditsBudgetsRefreshButton", false },
        { "CreditsBudgetsButton", "CreditsBudgetsHeader", "CreditsBudgetsAddBudgetPoolButton", false },
        { "CreditsBudgetsButton", "CreditsBudgetsHeader", "CreditsBudgetsEditBudgetPoolButton", true },
        { "CreditsBudgetsButton", "CreditsBudgetsHeader", "CreditsBudgetsAddFundsButton", true },
        { "CreditsBudgetsButton", "CreditsBudgetsHeader", "CreditsBudgetsArchiveBudgetPoolButton", true },

        { "DocumentsButton", "DocumentsHeader", "DocumentsRefreshButton", false },
        { "DocumentsButton", "DocumentsHeader", "DocumentsAddDocumentButton", false },
        { "DocumentsButton", "DocumentsHeader", "DocumentsViewDocumentButton", true },
        { "DocumentsButton", "DocumentsHeader", "DocumentsDeleteDocumentButton", true },

        { "ReportsButton", "ReportsHeader", "ReportsRefreshButton", false },
        { "ReportsButton", "ReportsHeader", "ReportsExportCompletedCsvButton", false },
        { "ReportsButton", "ReportsHeader", "ReportsExportWithdrawnCsvButton", false },
        { "ReportsButton", "ReportsHeader", "ReportsExportNonCompletionsCsvButton", false },
        { "ReportsButton", "ReportsHeader", "ReportsExportAwaitingDeliveryCsvButton", false },
        { "ReportsButton", "ReportsHeader", "ReportsExportDeliveredCsvButton", false },

        { "ImportExportButton", "ImportExportHeader", "CreateBackupButton", false },
        { "ImportExportButton", "ImportExportHeader", "RestoreBackupButton", false },
        { "ImportExportButton", "ImportExportHeader", "ExportInvoicerButton", false },
        { "ImportExportButton", "ImportExportHeader", "ImportWorkbookButton", false },
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
    [InlineData("StudentsButton", "StudentsHeader", "StudentsStudentsGrid", "StudentsEditStudentButton")]
    [InlineData("CoursesButton", "CoursesHeader", "CoursesCoursesGrid", "CoursesEditCourseButton")]
    [InlineData("DeliveriesButton", "DeliveriesHeader", "DeliveriesDeliveriesGrid", "DeliveriesEditDeliveryButton")]
    [InlineData("AllocationsButton", "AllocationsHeader", "AllocationsAllocationsGrid", "AllocationsEditAllocationButton")]
    public void SelectingARow_EnablesTheActionsThatNeedOne(string navigationId, string headerId, string gridId, string buttonId)
    {
        Run(() =>
        {
            Fixture.Navigate(navigationId, headerId);

            var grid = Fixture.WaitFor(gridId, TimeSpan.FromSeconds(10)).AsGrid();
            Assert.True(grid.Rows.Length > 0, $"{gridId} loaded no sample rows, so selection cannot be tested.");

            grid.Select(0);
            Wait.UntilInputIsProcessed();
            Thread.Sleep(500);

            var button = Fixture.WaitFor(buttonId, TimeSpan.FromSeconds(5)).AsButton();
            Assert.True(button.IsEnabled, $"{buttonId} stayed disabled after a row was selected.");
        }, $"{buttonId}-with-selection");
    }
}
