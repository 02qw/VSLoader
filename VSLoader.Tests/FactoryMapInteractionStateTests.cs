using VSLoader.Models;

namespace VSLoader.Tests;

public sealed class FactoryMapInteractionStateTests
{
    [Fact]
    public void Defaults_to_browse_mode_with_no_active_gesture_or_connection_draft()
    {
        var state = new FactoryMapInteractionState();

        Assert.Equal(FactoryMapMode.Browse, state.Mode);
        Assert.Equal(FactoryMapInteractionKind.Idle, state.Kind);
        Assert.Null(state.PendingConnectionPointId);
    }

    [Fact]
    public void Changing_gesture_keeps_connection_draft_only_for_panning()
    {
        var state = new FactoryMapInteractionState();
        state.SetMode(FactoryMapMode.Edit);
        state.BeginConnectionDraft("point-a");

        state.Begin(FactoryMapInteractionKind.Panning);

        Assert.Equal("point-a", state.PendingConnectionPointId);
        Assert.Equal(FactoryMapInteractionKind.Panning, state.Kind);

        state.Begin(FactoryMapInteractionKind.DraggingObject);

        Assert.Null(state.PendingConnectionPointId);
        Assert.Equal(FactoryMapInteractionKind.DraggingObject, state.Kind);
    }

    [Fact]
    public void Leaving_edit_mode_clears_gesture_and_connection_draft()
    {
        var state = new FactoryMapInteractionState();
        state.SetMode(FactoryMapMode.Edit);
        state.BeginConnectionDraft("point-a");
        state.Begin(FactoryMapInteractionKind.Panning);

        state.SetMode(FactoryMapMode.Browse);

        Assert.Equal(FactoryMapMode.Browse, state.Mode);
        Assert.Equal(FactoryMapInteractionKind.Idle, state.Kind);
        Assert.Null(state.PendingConnectionPointId);
    }

    [Fact]
    public void Connection_draft_requires_edit_mode_and_non_empty_point_id()
    {
        var state = new FactoryMapInteractionState();

        Assert.False(state.BeginConnectionDraft("point-a"));

        state.SetMode(FactoryMapMode.Edit);

        Assert.False(state.BeginConnectionDraft(" "));
        Assert.True(state.BeginConnectionDraft("point-a"));
    }

    [Fact]
    public void Segment_connection_draft_preserves_segment_and_projected_position()
    {
        var state = new FactoryMapInteractionState();
        state.SetMode(FactoryMapMode.Edit);

        var started = state.BeginSegmentConnectionDraft("segment-a", 125, 240);

        Assert.True(started);
        Assert.Equal(FactoryMapConnectionOriginKinds.Segment, state.ConnectionDraft?.OriginKind);
        Assert.Equal("segment-a", state.ConnectionDraft?.SegmentId);
        Assert.Equal(125, state.ConnectionDraft?.SegmentX);
        Assert.Equal(240, state.ConnectionDraft?.SegmentY);
        Assert.Null(state.PendingConnectionPointId);
    }
}
