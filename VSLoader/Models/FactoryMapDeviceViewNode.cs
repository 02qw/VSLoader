namespace VSLoader.Models;

public sealed class FactoryMapDeviceViewNode
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public ShortcutItem Shortcut { get; set; } = new();
}
