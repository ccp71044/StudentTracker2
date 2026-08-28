using ClosedXML.Excel;

namespace StudentTracker.Services;

/// <summary>
/// Shared reading of the provider's spreadsheet exports. Their headers and cells carry non-breaking
/// spaces ("Course ID\u00a0", "\u00a0Completed"), so every value is normalised before use.
/// </summary>
internal static class ProviderSheet
{
    public static string Clean(string? value) =>
        (value ?? string.Empty).Replace('\u00a0', ' ').Trim();

    /// <summary>Header text reduced to a lookup key: "First name" and "FIRSTNAME" both become "firstname".</summary>
    public static string Key(string? value) =>
        Clean(value).Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();

    public static int FindHeaderRow(IXLWorksheet worksheet, params string[] requiredHeaders)
    {
        var required = requiredHeaders.Select(Key).ToArray();

        foreach (var row in worksheet.RowsUsed())
        {
            var keys = row.CellsUsed().Select(c => Key(c.GetString())).ToHashSet(StringComparer.Ordinal);
            if (required.All(keys.Contains))
                return row.RowNumber();
        }

        return -1;
    }

    public static IReadOnlyDictionary<string, int> MapColumns(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cell in headerRow.CellsUsed())
        {
            var key = Key(cell.GetString());
            if (key.Length > 0 && !map.ContainsKey(key))
                map[key] = cell.Address.ColumnNumber;
        }
        return map;
    }

    public static string Text(IXLRow row, IReadOnlyDictionary<string, int> columns, string columnKey) =>
        columns.TryGetValue(columnKey, out var column) ? Clean(row.Cell(column).GetString()) : string.Empty;
}
