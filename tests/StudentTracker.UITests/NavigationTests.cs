using Xunit;

namespace StudentTracker.UITests;

/// <summary>Every section in the navigation bar opens and renders its header.</summary>
public class NavigationTests : UiTest
{
    public NavigationTests(AppUiTestFixture fixture) : base(fixture) { }

    [Theory]
    [InlineData("DashboardButton", "DashboardHeader")]
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
        Run(() =>
        {
            Fixture.Navigate(buttonAutomationId, expectedHeaderAutomationId);
            Assert.NotNull(Fixture.WaitFor(expectedHeaderAutomationId, TimeSpan.FromSeconds(10)));
        }, buttonAutomationId);
    }

    [Fact]
    public void MainWindow_IsVisible_WithCorrectTitle() => Run(() =>
        Assert.Contains("Student Tracker", Fixture.GetMainWindow().Title, StringComparison.OrdinalIgnoreCase));
}
