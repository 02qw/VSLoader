namespace VSLoader.Models;

public sealed class FactoryMapEdgeViewData
{
    public FactoryMapNodeViewData From { get; set; } = new();

    public FactoryMapNodeViewData To { get; set; } = new();

    public List<FactoryMapPoint> Points { get; set; } = [];
}
