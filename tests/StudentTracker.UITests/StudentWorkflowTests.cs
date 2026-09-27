using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace StudentTracker.UITests;

/// <summary>
/// End-to-end cover for the section an operator uses most: adding a student, refusing an invalid
/// one, searching for a saved one, and opening the read-only profile that used to throw.
/// </summary>
public class StudentWorkflowTests : UiTest
{
    private const string Surname = "Uitest";

    public StudentWorkflowTests(AppUiTestFixture fixture) : base(fixture) { }

    [Fact]
    public void AddStudent_WithNoName_IsRefusedAndSaysWhy() => Run(() =>
    {
        Fixture.Navigate("StudentsButton", "StudentsHeader");
        Fixture.Click(Fixture.GetMainWindow(), "AddStudentButton");

        var dialog = Fixture.ModalDialog(TimeSpan.FromSeconds(10))
            ?? throw new InvalidOperationException("Add Student did not open a dialog.");

        dialog.FindFirstDescendant(cf => cf.ByAutomationId("StudentEditSaveButton"))!.AsButton().Invoke();
        Wait.UntilInputIsProcessed();
        Thread.Sleep(750);

        var error = dialog.FindFirstDescendant(cf => cf.ByAutomationId("StudentEditErrorMessage"));
        Assert.True(error != null, "The edit dialog has no error label.");
        Assert.False(string.IsNullOrWhiteSpace(error!.AsLabel().Text), "Saving a student with no name was accepted silently.");

        DismissDialogs();
    });

    [Fact]
    public void AddStudent_ThenSearch_FindsTheNewStudent() => Run(() =>
    {
        var firstName = $"Devin{DateTime.Now:HHmmss}";

        Fixture.Navigate("StudentsButton", "StudentsHeader");
        Fixture.Click(Fixture.GetMainWindow(), "AddStudentButton");

        var dialog = Fixture.ModalDialog(TimeSpan.FromSeconds(10))
            ?? throw new InvalidOperationException("Add Student did not open a dialog.");

        Type(dialog, "StudentEditFirstNameInput", firstName);
        Type(dialog, "StudentEditLastNameInput", Surname);
        Type(dialog, "StudentEditEmailInput", $"{firstName}@example.test".ToLowerInvariant());

        dialog.FindFirstDescendant(cf => cf.ByAutomationId("StudentEditSaveButton"))!.AsButton().Invoke();
        Wait.UntilInputIsProcessed();
        Thread.Sleep(1500);

        AssertNoError(ErrorText("StudentsErrorMessage"), "Saving a valid student");

        Type(Fixture.GetMainWindow(), "StudentsSearchTextBox", firstName);
        Fixture.Click(Fixture.GetMainWindow(), "StudentsSearchButton");
        Thread.Sleep(1000);

        var grid = Fixture.WaitFor("StudentsDataGrid", TimeSpan.FromSeconds(10)).AsGrid();
        Assert.True(
            grid.Rows.Any(r => r.Cells.Any(c => c.Value?.Contains(firstName, StringComparison.OrdinalIgnoreCase) == true)),
            $"The saved student {firstName} was not found by search.");
    });

    [Fact]
    public void ViewStudent_OpensTheProfileForTheSelectedRow() => Run(() =>
    {
        Fixture.Navigate("StudentsButton", "StudentsHeader");

        var grid = Fixture.WaitFor("StudentsDataGrid", TimeSpan.FromSeconds(10)).AsGrid();
        Assert.True(grid.Rows.Length > 0, "No students were loaded, so the profile cannot be opened.");
        grid.Select(0);
        Wait.UntilInputIsProcessed();

        Fixture.Click(Fixture.GetMainWindow(), "ViewStudentButton");

        var dialog = Fixture.ModalDialog(TimeSpan.FromSeconds(10));
        Assert.True(dialog != null, "View did not open the student profile - this is the dialog lookup that used to throw.");

        DismissDialogs();
    });

    private static void Type(AutomationElement scope, string automationId, string text)
    {
        var box = scope.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
            ?? throw new InvalidOperationException($"Input '{automationId}' was not found.");

        box.Focus();
        box.AsTextBox().Text = text;
        Wait.UntilInputIsProcessed();
    }
}
