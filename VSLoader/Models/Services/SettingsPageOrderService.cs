using VSLoader.Models;

namespace VSLoader.Services;

public static class SettingsPageOrderService
{
    public static IReadOnlyList<string> DefaultPageOrder { get; } =
    [
        SettingsPageIds.General,
        SettingsPageIds.AdminUi,
        SettingsPageIds.WebUi,
        SettingsPageIds.Updates,
        SettingsPageIds.Hotkeys,
        SettingsPageIds.ContextMenuCapabilities
    ];

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? configuredOrder)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuredId in configuredOrder ?? [])
        {
            var canonicalId = DefaultPageOrder.FirstOrDefault(defaultId =>
                string.Equals(defaultId, configuredId?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (canonicalId is not null && seen.Add(canonicalId))
            {
                result.Add(canonicalId);
            }
        }

        foreach (var defaultId in DefaultPageOrder)
        {
            if (seen.Add(defaultId))
            {
                result.Add(defaultId);
            }
        }

        return result;
    }

    public static string GetDisplayName(string pageId)
    {
        return pageId switch
        {
            SettingsPageIds.General => "常规路径",
            SettingsPageIds.AdminUi => "AdminUI",
            SettingsPageIds.WebUi => "WebUI",
            SettingsPageIds.Updates => "更新",
            SettingsPageIds.Hotkeys => "快捷键",
            SettingsPageIds.ContextMenuCapabilities => "右键菜单能力",
            SettingsPageIds.PageOrder => "页面顺序",
            _ => "未知页面"
        };
    }
}
