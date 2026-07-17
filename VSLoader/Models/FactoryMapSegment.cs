namespace VSLoader.Models;

public sealed class FactoryMapSegment
{
    public string Id { get; set; } = string.Empty;

    public string FromPointId { get; set; } = string.Empty;

    public string ToPointId { get; set; } = string.Empty;

    public int ZIndex { get; set; }
}
