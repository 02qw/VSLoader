using VSLoader.Services;

namespace VSLoader.Models;

public sealed class FactoryMapDeviceViewNode
{
    public string Id { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; } = FactoryMapNodeGeometryService.MinimumWidth;

    public double Height { get; set; } = FactoryMapNodeGeometryService.MinimumHeight;

    public ShortcutItem Shortcut { get; set; } = new();
}
