using System.Text.Json.Serialization;

namespace VSLoader.Models;

public sealed class FactoryMapLayoutConfig
{
    public int Version { get; set; }

    public FactoryMapCanvas Canvas { get; set; } = new() { Width = 1600, Height = 900 };

    public List<FactoryMapDeviceNode> Devices { get; set; } = [];

    public List<FactoryMapConnectionPoint> ConnectionPoints { get; set; } = [];

    public List<FactoryMapSegment> Segments { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<FactoryMapConnectorNode>? Connectors { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<FactoryMapDeviceEdge>? Edges { get; set; }
}
