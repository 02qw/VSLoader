using System.Collections;
using System.ComponentModel;
using System.Text.RegularExpressions;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ShortcutSortService : IComparer
{
    private static readonly Regex NameWithNoRegex = new(@"^(?<Name>.+)_(?<No>\d+)$", RegexOptions.Compiled);

    private readonly ShortcutSortField _field;
    private readonly ListSortDirection _direction;

    public ShortcutSortService(
        ShortcutSortField field = ShortcutSortField.Name,
        ListSortDirection direction = ListSortDirection.Ascending)
    {
        _field = field;
        _direction = direction;
    }

    public int Compare(object? x, object? y)
    {
        var result = Compare(x as ShortcutItem, y as ShortcutItem, _field);
        return _direction == ListSortDirection.Descending ? -result : result;
    }

    public static int Compare(ShortcutItem? x, ShortcutItem? y)
    {
        return Compare(x, y, ShortcutSortField.Name);
    }

    private static int Compare(ShortcutItem? x, ShortcutItem? y, ShortcutSortField field)
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

        return field switch
        {
            ShortcutSortField.Description => CompareText(x.Description, y.Description),
            ShortcutSortField.UpdatedAt => DateTime.Compare(x.UpdatedAt, y.UpdatedAt),
            _ => CompareName(x, y)
        };
    }

    private static int CompareName(ShortcutItem x, ShortcutItem y)
    {
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

    private static int CompareText(string x, string y)
    {
        var xIsEmpty = string.IsNullOrWhiteSpace(x);
        var yIsEmpty = string.IsNullOrWhiteSpace(y);

        if (xIsEmpty && yIsEmpty)
        {
            return 0;
        }

        if (xIsEmpty)
        {
            return 1;
        }

        if (yIsEmpty)
        {
            return -1;
        }

        return string.Compare(x.Trim(), y.Trim(), StringComparison.CurrentCultureIgnoreCase);
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
