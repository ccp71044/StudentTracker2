using System.Text.RegularExpressions;

namespace StudentTracker.Tests;

/// <summary>
/// The FlaUI suite drives the application by automation id, so a control that loses its id turns
/// into a UI test that fails on Windows only. These tests read the XAML and keep the ids present
/// and unique before any of that runs.
/// </summary>
public class AutomationIdTests
{
    private static readonly Regex Element =
        new(@"<(?<tag>Button|DataGrid|TextBox|ComboBox|DatePicker|CheckBox)\b[^>]*?/?>", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex AutomationId =
        new(@"AutomationProperties\.AutomationId=""(?<id>[^""]+)""", RegexOptions.Compiled);

    private static string WpfRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "StudentTracker.Wpf")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            return Path.Combine(directory!.FullName, "src", "StudentTracker.Wpf");
        }
    }

    private static IEnumerable<string> XamlFiles() =>
        Directory.GetFiles(Path.Combine(WpfRoot, "Views"), "*.xaml")
            .Append(Path.Combine(WpfRoot, "MainWindow.xaml"));

    public static IEnumerable<object[]> Xaml() => XamlFiles().Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(Xaml))]
    public void EveryCommandButtonHasAnAutomationId(string xamlPath)
    {
        var source = File.ReadAllText(xamlPath);

        foreach (var element in Element.Matches(source).Where(m => m.Groups["tag"].Value == "Button"))
        {
            var tag = element.Value;
            if (!tag.Contains("Command=\"{Binding")) continue;

            Assert.True(
                AutomationId.IsMatch(tag),
                $"{Path.GetFileName(xamlPath)} has a command button with no AutomationId: {tag}");
        }
    }

    [Fact]
    public void AutomationIdsAreUniqueWithinEachView()
    {
        foreach (var file in XamlFiles())
        {
            var ids = AutomationId.Matches(File.ReadAllText(file)).Select(m => m.Groups["id"].Value).ToList();
            var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            Assert.True(duplicates.Count == 0, $"{Path.GetFileName(file)} reuses automation id(s): {string.Join(", ", duplicates)}");
        }
    }

    [Fact]
    public void EveryViewHasAHeaderTheUiTestsCanWaitFor()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(WpfRoot, "Views"), "*View.xaml"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.EndsWith("EditView", StringComparison.Ordinal) || name == "StudentView" || name == "BudgetAddFundsView") continue;

            var expected = $"AutomationProperties.AutomationId=\"{name[..^"View".Length]}Header\"";
            Assert.True(File.ReadAllText(file).Contains(expected), $"{name} has no header carrying {expected}.");
        }
    }
}
