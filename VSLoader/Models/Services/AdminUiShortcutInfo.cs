using VSLoader.Models;

namespace VSLoader.Services;

public sealed class AdminUiShortcutInfo
{
    public ShortcutItem Shortcut { get; init; } = new();

    public string InstanceName { get; init; } = string.Empty;

    public string Port { get; init; } = string.Empty;

    public string ServiceName { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string LocalJnlpPath { get; init; } = string.Empty;
}
