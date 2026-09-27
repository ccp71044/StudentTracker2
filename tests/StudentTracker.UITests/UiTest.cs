using System.Runtime.CompilerServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Xunit;

namespace StudentTracker.UITests;

/// <summary>
/// Shared behaviour for the UI suites: a screenshot and the application log are captured whenever
/// a step fails, because a failure on someone else's Windows machine is otherwise unreadable.
/// </summary>
public abstract class UiTest : IClassFixture<AppUiTestFixture>
{
    protected readonly AppUiTestFixture Fixture;

    protected UiTest(AppUiTestFixture fixture) => Fixture = fixture;

    protected void Run(Action body, [CallerMemberName] string testName = "")
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            var screenshot = Fixture.Screenshot(testName);
            throw new UiTestFailure(
                $"{testName} failed.\n{ex.Message}\n\nScreenshot: {(screenshot.Length == 0 ? "(not captured)" : screenshot)}\n\nApplication log:\n{Fixture.LogText()}",
                ex);
        }
    }

    /// <summary>
    /// Closes whatever a command opened - an edit dialog, or a native file picker - so the next
    /// step starts from the section it left.
    /// </summary>
    protected void DismissDialogs()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var modal = Fixture.ModalDialog(TimeSpan.FromMilliseconds(750));
            if (modal == null) return;

            var cancel = modal.FindFirstDescendant(cf => cf.ByName("Cancel"))
                ?? modal.FindFirstDescendant(cf => cf.ByName("Close"));
            if (cancel != null)
            {
                cancel.AsButton().Invoke();
            }
            else
            {
                modal.Focus();
                Keyboard.Press(VirtualKeyShort.ESCAPE);
            }

            Wait.UntilInputIsProcessed();
            Thread.Sleep(750);

            try
            {
                if (!modal.IsOffscreen) modal.Close();
            }
            catch { /* already gone */ }
        }
    }

    /// <summary>The text of a section's error label, which stays empty while commands succeed.</summary>
    protected string ErrorText(string automationId)
    {
        var element = Fixture.Find(automationId, TimeSpan.FromSeconds(2));
        return element?.AsLabel().Text ?? string.Empty;
    }

    protected static void AssertNoError(string errorText, string context) =>
        Assert.True(string.IsNullOrWhiteSpace(errorText), $"{context} reported: {errorText}");
}

public class UiTestFailure : Exception
{
    public UiTestFailure(string message, Exception inner) : base(message, inner) { }
}
