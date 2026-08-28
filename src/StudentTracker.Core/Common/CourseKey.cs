using System.Text;
using System.Text.RegularExpressions;

namespace StudentTracker.Core.Common;

/// <summary>
/// Builds a stable matching key for a course described in free text, so the register, the
/// provider price list and the provider credit history all resolve to the same course.
/// </summary>
public static partial class CourseKey
{
    /// <summary>
    /// A national unit code (HLTAID011, RIIWHS202E, CPPFES2005) or an accredited course code
    /// (11124NAT, 22578VIC) appearing at the start of the text.
    /// </summary>
    [GeneratedRegex(@"^\s*(?<code>(?:[A-Z]{3,6}\d{3,5}[A-Z]?)|(?:\d{4,5}[A-Z]{2,3}))\b", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingCodeRegex();

    [GeneratedRegex(@"^\s*course\s*set\b[\s\-:]*", RegexOptions.IgnoreCase)]
    private static partial Regex CourseSetPrefixRegex();

    /// <summary>
    /// Splits free text into a display code and title. Course sets have no single code, so they
    /// keep "Course Set" as the code and carry the rest as the title.
    /// </summary>
    public static (string Code, string Title) Split(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return (string.Empty, string.Empty);

        var setMatch = CourseSetPrefixRegex().Match(trimmed);
        if (setMatch.Success)
            return ("Course Set", trimmed[setMatch.Length..].Trim());

        var codeMatch = LeadingCodeRegex().Match(trimmed);
        if (codeMatch.Success)
        {
            var code = codeMatch.Groups["code"].Value.ToUpperInvariant();
            var title = trimmed[codeMatch.Length..].TrimStart(' ', '-', ':', '–').Trim();
            return (code, title.Length > 0 ? title : code);
        }

        return (trimmed, trimmed);
    }

    /// <summary>
    /// The key used to match the same course across sources. Unit codes match on the code alone;
    /// course sets match on their normalised description, since they have no code of their own.
    /// </summary>
    public static string Build(string text)
    {
        var (code, title) = Split(text);
        if (code.Length == 0)
            return string.Empty;

        return code == "Course Set" ? "SET:" + Normalise(title) : code;
    }

    /// <summary>
    /// Lowercases and strips everything but letters and digits, so "HLTAID014 &amp; HLTAID015"
    /// and "HLTAID014 and HLTAID015 " collapse to the same text.
    /// </summary>
    public static string Normalise(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToLowerInvariant(ch));
        }
        return builder.ToString();
    }
}
