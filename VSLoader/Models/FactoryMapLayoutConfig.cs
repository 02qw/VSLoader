namespace VSLoader.Models;

public sealed class FactoryMapLayoutConfig
{
    public int Version { get; set; } = 2;

    public FactoryMapCanvas Canvas { get; set; } = new() { Width = 1600, Height = 900 };

    public List<FactoryMapDeviceNode> Devices { get; set; } = [];

    public List<FactoryMapDeviceEdge> Edges { get; set; } = [];
}
