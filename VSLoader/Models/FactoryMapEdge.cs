namespace VSLoader.Models;

public sealed class FactoryMapEdge
{
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public List<FactoryMapPoint> Points { get; set; } = [];
}
