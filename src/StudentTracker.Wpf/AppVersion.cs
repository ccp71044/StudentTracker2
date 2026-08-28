using System.Reflection;

namespace StudentTracker.Wpf;

public static class AppVersion
{
    public static string Current { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "unknown";
}
