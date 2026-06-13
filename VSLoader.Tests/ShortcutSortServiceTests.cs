using System.ComponentModel;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ShortcutSortServiceTests
{
    [Fact]
    public void Compare_sorts_by_source_module_name()
    {
        var comparer = new ShortcutSortService(ShortcutSortField.SourceModuleName, ListSortDirection.Ascending);
        var first = new ShortcutItem { Name = "B", SourceModuleName = "eap-sic-B" };
        var second = new ShortcutItem { Name = "A", SourceModuleName = "eap-sic-A" };

        Assert.True(comparer.Compare(first, second) > 0);
    }
}
