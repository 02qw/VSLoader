namespace VSLoader.Models;

public sealed class FactoryMapConfig
{
    public FactoryMapCanvas Canvas { get; set; } = new();

    public List<FactoryMapNode> Nodes { get; set; } = [];

    public List<FactoryMapEdge> Edges { get; set; } = [];
}
