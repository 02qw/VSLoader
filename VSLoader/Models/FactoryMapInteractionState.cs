namespace VSLoader.Models;

public sealed class FactoryMapInteractionState
{
    public FactoryMapMode Mode { get; private set; } = FactoryMapMode.Browse;

    public FactoryMapInteractionKind Kind { get; private set; } = FactoryMapInteractionKind.Idle;

    public FactoryMapConnectionDraft? ConnectionDraft { get; private set; }

    public string? PendingConnectionPointId =>
        ConnectionDraft?.OriginKind == FactoryMapConnectionOriginKinds.Point
            ? ConnectionDraft.PointId
            : null;

    public void SetMode(FactoryMapMode mode)
    {
        Mode = mode;
        Kind = FactoryMapInteractionKind.Idle;
        ConnectionDraft = null;
    }

    public void Begin(FactoryMapInteractionKind kind)
    {
        Kind = kind;
        if (kind != FactoryMapInteractionKind.Panning)
        {
            ConnectionDraft = null;
        }
    }

    public void Complete() => Kind = FactoryMapInteractionKind.Idle;

    public void Cancel()
    {
        Kind = FactoryMapInteractionKind.Idle;
        ConnectionDraft = null;
    }

    public bool BeginConnectionDraft(string pointId)
    {
        if (Mode != FactoryMapMode.Edit || string.IsNullOrWhiteSpace(pointId))
        {
            return false;
        }

        ConnectionDraft = FactoryMapConnectionDraft.FromPoint(pointId);
        return true;
    }

    public bool BeginSegmentConnectionDraft(string segmentId, double x, double y)
    {
        if (Mode != FactoryMapMode.Edit
            || string.IsNullOrWhiteSpace(segmentId)
            || !double.IsFinite(x)
            || !double.IsFinite(y))
        {
            return false;
        }

        ConnectionDraft = FactoryMapConnectionDraft.FromSegment(segmentId, x, y);
        return true;
    }

    public void CancelConnectionDraft() => ConnectionDraft = null;
}
