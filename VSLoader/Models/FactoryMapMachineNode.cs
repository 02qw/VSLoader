namespace VSLoader.Models;

public sealed class FactoryMapMachineNode
{
    public string Name { get; set; } = string.Empty;

    public string No { get; set; } = string.Empty;

    public ShortcutItem Shortcut { get; set; } = new();
}
