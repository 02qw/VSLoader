using VSLoader.Models;
using WpfPoint = System.Windows.Point;

namespace VSLoader.Services;

public sealed class FactoryMapRenderIndexService
{
    private const double Precision = 1000;
    private const double Epsilon = 0.001;

    public IReadOnlyList<FactoryMapVisibleSegment> Build(
        IReadOnlyList<FactoryMapConnectionPoint> points,
        IReadOnlyList<FactoryMapSegment> segments,
        string? selectedSegmentId = null)
    {
        var pointById = points
            .Where(point => !string.IsNullOrWhiteSpace(point.Id))
            .GroupBy(point => point.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var normalized = new List<IndexedSegment>();
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            if (!pointById.TryGetValue(segment.FromPointId, out var from)
                || !pointById.TryGetValue(segment.ToPointId, out var to))
            {
                continue;
            }

            if (NearlyEqual(from.Y, to.Y) && !NearlyEqual(from.X, to.X))
            {
                normalized.Add(new IndexedSegment(
                    segment,
                    index,
                    true,
                    ToAxisKey(from.Y),
                    Math.Min(from.X, to.X),
                    Math.Max(from.X, to.X)));
            }
            else if (NearlyEqual(from.X, to.X) && !NearlyEqual(from.Y, to.Y))
            {
                normalized.Add(new IndexedSegment(
                    segment,
                    index,
                    false,
                    ToAxisKey(from.X),
                    Math.Min(from.Y, to.Y),
                    Math.Max(from.Y, to.Y)));
            }
        }

        return normalized
            .GroupBy(segment => (segment.IsHorizontal, segment.AxisKey))
            .SelectMany(group => BuildAxis(group.ToList(), selectedSegmentId))
            .OrderBy(segment => segment.Start.Y)
            .ThenBy(segment => segment.Start.X)
            .ThenBy(segment => segment.End.Y)
            .ThenBy(segment => segment.End.X)
            .ToList();
    }

    private static IEnumerable<FactoryMapVisibleSegment> BuildAxis(
        IReadOnlyList<IndexedSegment> segments,
        string? selectedSegmentId)
    {
        var boundaries = segments
            .SelectMany(segment => new[] { segment.From, segment.To })
            .Distinct()
            .OrderBy(value => value)
            .ToList();
        var visible = new List<FactoryMapVisibleSegment>();
        for (var index = 0; index < boundaries.Count - 1; index++)
        {
            var from = boundaries[index];
            var to = boundaries[index + 1];
            if (to - from <= Epsilon)
            {
                continue;
            }

            var midpoint = from + ((to - from) / 2);
            var sources = segments
                .Where(segment => segment.From - Epsilon <= midpoint && segment.To + Epsilon >= midpoint)
                .ToList();
            if (sources.Count == 0)
            {
                continue;
            }

            var top = sources
                .OrderByDescending(segment => string.Equals(
                    segment.Segment.Id,
                    selectedSegmentId,
                    StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(segment => segment.Segment.ZIndex)
                .ThenByDescending(segment => segment.SourceIndex)
                .First();
            var sourceIds = sources
                .OrderBy(segment => segment.SourceIndex)
                .Select(segment => segment.Segment.Id)
                .ToList();
            var axis = segments[0].AxisKey / Precision;
            var item = segments[0].IsHorizontal
                ? new FactoryMapVisibleSegment
                {
                    Start = new WpfPoint(from, axis),
                    End = new WpfPoint(to, axis),
                    SourceSegmentIds = sourceIds,
                    TopSegmentId = top.Segment.Id
                }
                : new FactoryMapVisibleSegment
                {
                    Start = new WpfPoint(axis, from),
                    End = new WpfPoint(axis, to),
                    SourceSegmentIds = sourceIds,
                    TopSegmentId = top.Segment.Id
                };

            if (visible.Count > 0 && CanMerge(visible[^1], item))
            {
                var previous = visible[^1];
                visible[^1] = new FactoryMapVisibleSegment
                {
                    Start = previous.Start,
                    End = item.End,
                    SourceSegmentIds = previous.SourceSegmentIds,
                    TopSegmentId = previous.TopSegmentId
                };
            }
            else
            {
                visible.Add(item);
            }
        }

        return visible;
    }

    private static bool CanMerge(FactoryMapVisibleSegment first, FactoryMapVisibleSegment second)
    {
        return NearlyEqual(first.End.X, second.Start.X)
            && NearlyEqual(first.End.Y, second.Start.Y)
            && string.Equals(first.TopSegmentId, second.TopSegmentId, StringComparison.OrdinalIgnoreCase)
            && first.SourceSegmentIds.SequenceEqual(second.SourceSegmentIds, StringComparer.OrdinalIgnoreCase);
    }

    private static long ToAxisKey(double value)
    {
        return (long)Math.Round(value * Precision, MidpointRounding.AwayFromZero);
    }

    private static bool NearlyEqual(double first, double second)
    {
        return Math.Abs(first - second) <= Epsilon;
    }

    private sealed record IndexedSegment(
        FactoryMapSegment Segment,
        int SourceIndex,
        bool IsHorizontal,
        long AxisKey,
        double From,
        double To);
}
