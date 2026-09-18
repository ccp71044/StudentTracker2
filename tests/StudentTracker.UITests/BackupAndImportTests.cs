using FlaUI.Core.AutomationElements;
using Xunit;

namespace StudentTracker.UITests;

/// <summary>
/// Import/Export is the section that touches the file system, so it is checked against the
/// throwaway data root: a backup must land there, and the pickers must open and close cleanly.
/// </summary>
public class BackupAndImportTests : UiTest
{
    public BackupAndImportTests(AppUiTestFixture fixture) : base(fixture) { }

    [Fact]
    public void CreateBackup_WritesAnArchiveUnderTheTestDataRoot() => Run(() =>
    {
        Fixture.Navigate("ImportExportButton", "ImportExportHeader");
        Fixture.Click(Fixture.GetMainWindow(), "CreateBackupButton");
        Thread.Sleep(2000);

        var status = Fixture.WaitFor("ImportExportStatusText", TimeSpan.FromSeconds(10)).AsLabel().Text;
        Assert.Contains("Backup created", status, StringComparison.OrdinalIgnoreCase);

        var backups = Path.Combine(Fixture.DataDirectory, "Backups");
        Assert.True(Directory.Exists(backups), $"No backup folder under {Fixture.DataDirectory}.");
        Assert.True(Directory.GetFiles(backups, "*.zip", SearchOption.AllDirectories).Length > 0, "No backup archive was written.");
    });

    [Theory]
    [InlineData("ImportWorkbookButton")]
    [InlineData("ImportCompletionPricingButton")]
    [InlineData("ImportCreditHistoryButton")]
    [InlineData("RestoreBackupButton")]
    public void FilePicker_OpensAndCancelsWithoutError(string buttonId)
    {
        Run(() =>
        {
            Fixture.Navigate("ImportExportButton", "ImportExportHeader");
            Fixture.Click(Fixture.GetMainWindow(), buttonId);
            Thread.Sleep(1500);

            var picker = Fixture.ModalDialog(TimeSpan.FromSeconds(5));
            Assert.True(picker != null, $"{buttonId} opened no file picker.");

            DismissDialogs();

            var status = Fixture.WaitFor("ImportExportStatusText", TimeSpan.FromSeconds(10)).AsLabel().Text;
            Assert.DoesNotContain("failed", status, StringComparison.OrdinalIgnoreCase);
        }, buttonId);
    }

    [Fact]
    public void ExportInvoicerBatch_ReportsWhatItDid() => Run(() =>
    {
        Fixture.Navigate("ImportExportButton", "ImportExportHeader");
        Fixture.Click(Fixture.GetMainWindow(), "ExportInvoicerButton");
        Thread.Sleep(2000);

        DismissDialogs();

        var status = Fixture.WaitFor("ImportExportStatusText", TimeSpan.FromSeconds(10)).AsLabel().Text;
        Assert.False(string.IsNullOrWhiteSpace(status), "Export Invoicer Batch said nothing at all.");
        Assert.DoesNotContain("failed", status, StringComparison.OrdinalIgnoreCase);
    });
}
