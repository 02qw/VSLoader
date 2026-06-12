namespace VSLoader.Models;

public sealed class FactoryMapViewData
{
    public FactoryMapCanvas Canvas { get; set; } = new();

    public List<FactoryMapNodeViewData> Nodes { get; set; } = [];

    public List<FactoryMapEdgeViewData> Edges { get; set; } = [];

    public int UnmatchedShortcutCount { get; set; }

    public int InvalidEdgeCount { get; set; }
}
