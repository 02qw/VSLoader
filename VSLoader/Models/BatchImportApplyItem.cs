namespace VSLoader.Models;

public sealed class BatchImportApplyItem
{
    public bool IsUpdate { get; set; }

    public string ExistingTargetPath { get; set; } = string.Empty;

    public ShortcutItem Shortcut { get; set; } = new();
}
