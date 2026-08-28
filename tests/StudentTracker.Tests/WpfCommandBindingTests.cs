using System.Text.RegularExpressions;

namespace StudentTracker.Tests;

/// <summary>
/// The WPF layer only builds on Windows tooling, so these tests read its sources instead: they
/// catch buttons wired to commands that no longer exist and dialogs with no view to show, which
/// otherwise surface as a button that silently does nothing.
/// </summary>
public class WpfCommandBindingTests
{
    private static readonly Regex CommandBinding =
        new(@"Command=""\{Binding (?<name>[A-Za-z0-9_]+)\}""", RegexOptions.Compiled);

    private static readonly Regex RelayCommandMethod =
        new(@"\[RelayCommand[^\]]*\]\s*private\s+(?:async\s+)?[A-Za-z0-9_<>?\.]+\s+(?<name>\w+)\s*\(",
            RegexOptions.Compiled);

    private static readonly Regex CommandProperty =
        new(@"public\s+I?(?:Async)?RelayCommand[^\s]*\s+(?<name>\w+)\s*(?:\{|=>)", RegexOptions.Compiled);

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

    private static IReadOnlyDictionary<string, string> ViewModelSources() =>
        Directory.GetFiles(Path.Combine(WpfRoot, "ViewModels"), "*.cs")
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f)!, File.ReadAllText);

    private static HashSet<string> CommandNames(string source)
    {
        var names = RelayCommandMethod.Matches(source).Select(m => m.Groups["name"].Value + "Command").ToList();
        names.AddRange(CommandProperty.Matches(source).Select(m => m.Groups["name"].Value));
        return names.ToHashSet(StringComparer.Ordinal);
    }

    public static IEnumerable<object[]> Views()
    {
        yield return new object[] { Path.Combine(WpfRoot, "MainWindow.xaml"), "MainViewModel" };

        foreach (var view in Directory.GetFiles(Path.Combine(WpfRoot, "Views"), "*.xaml"))
        {
            var name = Path.GetFileNameWithoutExtension(view);
            if (name == "DialogWindow") continue;

            // StudentEditView pairs with StudentEditViewModel; StudentView pairs with StudentViewViewModel.
            var viewModel = ViewModelSources().ContainsKey(name + "Model") ? name + "Model" : name + "ViewModel";
            yield return new object[] { view, viewModel };
        }
    }

    [Theory]
    [MemberData(nameof(Views))]
    public void EveryBoundCommandExistsOnItsViewModel(string viewPath, string viewModelName)
    {
        var sources = ViewModelSources();
        Assert.True(sources.ContainsKey(viewModelName), $"{Path.GetFileName(viewPath)} has no view model {viewModelName}.");

        var commands = CommandNames(sources[viewModelName]);
        var bound = CommandBinding.Matches(File.ReadAllText(viewPath))
            .Select(m => m.Groups["name"].Value)
            .Distinct();

        foreach (var name in bound)
        {
            Assert.True(commands.Contains(name), $"{Path.GetFileName(viewPath)} binds to {name}, which {viewModelName} does not expose.");
        }
    }

    [Fact]
    public void EveryDialogViewModelHasAViewToShow()
    {
        var views = Directory.GetFiles(Path.Combine(WpfRoot, "Views"), "*.xaml")
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (name, source) in ViewModelSources().Where(vm => vm.Value.Contains(": ViewModelBase, ICloseable")))
        {
            var stem = name[..^"ViewModel".Length];
            Assert.True(
                views.Contains(stem + "View") || views.Contains(stem),
                $"{name} is shown as a dialog but no matching view exists (DialogService throws at runtime).");
        }
    }

    [Fact]
    public void NoViewModelLoadsItsDataFromItsConstructor()
    {
        foreach (var (name, source) in ViewModelSources())
        {
            Assert.False(
                source.Contains("().ConfigureAwait(false);"),
                $"{name} starts a fire-and-forget load in its constructor; failures there are never seen.");
        }
    }
}
