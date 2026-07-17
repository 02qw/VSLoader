namespace VSLoader.Models;

public sealed class FactoryMapConnectionDraft
{
    public string OriginKind { get; init; } = string.Empty;

    public string PointId { get; init; } = string.Empty;

    public string SegmentId { get; init; } = string.Empty;

    public double SegmentX { get; init; }

    public double SegmentY { get; init; }

    public static FactoryMapConnectionDraft FromPoint(string pointId)
    {
        return new FactoryMapConnectionDraft
        {
            OriginKind = FactoryMapConnectionOriginKinds.Point,
            PointId = pointId
        };
    }

    public static FactoryMapConnectionDraft FromSegment(string segmentId, double x, double y)
    {
        return new FactoryMapConnectionDraft
        {
            OriginKind = FactoryMapConnectionOriginKinds.Segment,
            SegmentId = segmentId,
            SegmentX = x,
            SegmentY = y
        };
    }
}
