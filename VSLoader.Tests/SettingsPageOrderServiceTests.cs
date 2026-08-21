using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class SettingsPageOrderServiceTests
{
    [Fact]
    public void Normalize_keeps_known_custom_order_and_appends_missing_pages()
    {
        var result = SettingsPageOrderService.Normalize(
        [
            SettingsPageIds.Hotkeys,
            SettingsPageIds.AdminUi
        ]);

        Assert.Equal(
        [
            SettingsPageIds.Hotkeys,
            SettingsPageIds.AdminUi,
            SettingsPageIds.General,
            SettingsPageIds.WebUi,
            SettingsPageIds.Updates,
            SettingsPageIds.ContextMenuCapabilities,
            SettingsPageIds.CodeCompare
        ],
        result);
    }

    [Fact]
    public void Normalize_removes_unknown_duplicate_and_fixed_page_ids()
    {
        var result = SettingsPageOrderService.Normalize(
        [
            "unknown",
            SettingsPageIds.WebUi,
            SettingsPageIds.WebUi.ToUpperInvariant(),
            SettingsPageIds.PageOrder,
            SettingsPageIds.General
        ]);

        Assert.Equal(SettingsPageIds.WebUi, result[0]);
        Assert.Equal(SettingsPageIds.General, result[1]);
        Assert.Equal(SettingsPageOrderService.DefaultPageOrder.Count, result.Count);
        Assert.DoesNotContain(SettingsPageIds.PageOrder, result);
        Assert.Equal(result.Count, result.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Normalize_uses_default_order_when_configuration_is_missing()
    {
        Assert.Equal(
            SettingsPageOrderService.DefaultPageOrder,
            SettingsPageOrderService.Normalize(null));
    }
}
