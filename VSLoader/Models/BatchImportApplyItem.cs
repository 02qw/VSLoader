namespace VSLoader.Models;

public sealed class BatchImportApplyItem
{
    public bool IsUpdate { get; set; }

    public string ExistingTargetPath { get; set; } = string.Empty;

    public ShortcutItem? ExistingShortcutToUpdate { get; set; }

    public ShortcutItem Shortcut { get; set; } = new();

    public IReadOnlyList<ShortcutItem> DuplicateShortcutsToRemove { get; set; } = Array.Empty<ShortcutItem>();
}
