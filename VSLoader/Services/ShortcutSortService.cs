using System.Collections;
using System.Text.RegularExpressions;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ShortcutSortService : IComparer
{
    private static readonly Regex NameWithNoRegex = new(@"^(?<Name>.+)_(?<No>\d+)$", RegexOptions.Compiled);

    public int Compare(object? x, object? y)
    {
        return Compare(x as ShortcutItem, y as ShortcutItem);
    }

    public static int Compare(ShortcutItem? x, ShortcutItem? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        var xKey = ParseKey(x.Name);
        var yKey = ParseKey(y.Name);

        var nameCompare = string.Compare(xKey.Name, yKey.Name, StringComparison.CurrentCultureIgnoreCase);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        var noCompare = Nullable.Compare(xKey.No, yKey.No);
        if (noCompare != 0)
        {
            if (xKey.No is null)
            {
                return 1;
            }

            if (yKey.No is null)
            {
                return -1;
            }

            return noCompare;
        }

        return string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
    }

    private static ShortcutSortKey ParseKey(string name)
    {
        var trimmedName = name.Trim();
        var match = NameWithNoRegex.Match(trimmedName);

        if (!match.Success || !int.TryParse(match.Groups["No"].Value, out var no))
        {
            return new ShortcutSortKey(trimmedName, null);
        }

        return new ShortcutSortKey(match.Groups["Name"].Value, no);
    }

    private sealed record ShortcutSortKey(string Name, int? No);
}
