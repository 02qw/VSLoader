namespace VSLoader.Models;

public sealed class ContextMenuCapabilityExecutionContext
{
    public ShortcutItem Shortcut { get; init; } = new();

    public string WorkspaceId { get; init; } = string.Empty;

    public string WorkspaceDirectory { get; init; } = string.Empty;

    public string AppBaseDirectory { get; init; } = string.Empty;

    public string Surface { get; init; } = string.Empty;
}
