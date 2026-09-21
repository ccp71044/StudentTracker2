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
        new(@"\[RelayCommand[^\]]*\]\s*(?:private|public|internal|protected)\s+(?:async\s+)?[A-Za-z0-9_<>?\.]+\s+(?<name>\w+)\s*\(",
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
        var names = RelayCommandMethod.Matches(source)
            .Select(m => m.Groups["name"].Value)
            .Select(name => name.EndsWith("Async", StringComparison.Ordinal) ? name[..^"Async".Length] : name)
            .Select(name => name + "Command")
            .ToList();
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
    public void NoSectionViewModelLoadsItsDataFromItsConstructor()
    {
        // MainViewModel builds every section up front, so a load started in a section's constructor
        // races the others over the shared database context and, being fire-and-forget, fails in
        // silence. Sections load through InitialiseAsync on first navigation instead.
        var sections = File.ReadAllText(Path.Combine(WpfRoot, "ViewModels", "MainViewModel.cs"));

        foreach (var (name, source) in ViewModelSources())
        {
            if (!sections.Contains($"{name} ")) continue;

            var body = ConstructorBody(source, name[..^"ViewModel".Length] + "ViewModel");
            Assert.False(
                body.Contains("LoadAsync()") || body.Contains("RefreshAsync()"),
                $"{name} starts a fire-and-forget load in its constructor; failures there are never seen.");
        }
    }

    private static string ConstructorBody(string source, string typeName)
    {
        var start = source.IndexOf($"public {typeName}(", StringComparison.Ordinal);
        if (start < 0) return string.Empty;

        var open = source.IndexOf('{', start);
        if (open < 0) return string.Empty;

        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[open..i];
        }

        return string.Empty;
    }
}
