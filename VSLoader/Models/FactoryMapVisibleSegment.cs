using WpfPoint = System.Windows.Point;

namespace VSLoader.Models;

public sealed class FactoryMapVisibleSegment
{
    public WpfPoint Start { get; init; }

    public WpfPoint End { get; init; }

    public IReadOnlyList<string> SourceSegmentIds { get; init; } = [];

    public string TopSegmentId { get; init; } = string.Empty;
}
