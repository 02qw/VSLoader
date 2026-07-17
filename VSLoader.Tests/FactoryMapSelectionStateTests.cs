using VSLoader.Models;

namespace VSLoader.Tests;

public sealed class FactoryMapSelectionStateTests
{
    [Fact]
    public void Select_toggle_and_clear_keep_primary_object_consistent()
    {
        var state = new FactoryMapSelectionState();
        var node = new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a");
        var point = new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free-1");

        state.Select(node);
        state.Toggle(point);

        Assert.Equal(point, state.PrimaryObject);
        Assert.Equal(2, state.SelectedObjects.Count);

        state.Toggle(point);

        Assert.DoesNotContain(point, state.SelectedObjects);
        Assert.Equal(node, state.PrimaryObject);

        state.Clear();

        Assert.Empty(state.SelectedObjects);
        Assert.Null(state.PrimaryObject);
    }

    [Fact]
    public void Replace_add_and_remove_manage_one_authoritative_selection_set()
    {
        var state = new FactoryMapSelectionState();
        var node = new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a");
        var point = new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free-1");
        var anotherNode = new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-b");

        state.ReplaceWith([node, point]);
        state.AddRange([point, anotherNode]);
        var removed = state.Remove(point);

        Assert.True(removed);
        Assert.Equal(2, state.SelectedObjects.Count);
        Assert.Contains(node, state.SelectedObjects);
        Assert.Contains(anotherNode, state.SelectedObjects);
        Assert.DoesNotContain(point, state.SelectedObjects);
        Assert.Equal(anotherNode, state.PrimaryObject);
    }

    [Fact]
    public void ReplaceWith_empty_collection_clears_primary_object()
    {
        var state = new FactoryMapSelectionState();
        state.Select(new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a"));

        state.ReplaceWith([]);

        Assert.Empty(state.SelectedObjects);
        Assert.Null(state.PrimaryObject);
    }

    [Fact]
    public void ReplaceWith_can_filter_the_current_selection_without_losing_the_source()
    {
        var state = new FactoryMapSelectionState();
        var node = new FactoryMapObjectRef(FactoryMapObjectKind.Device, "node-a");
        var point = new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, "free-1");
        state.ReplaceWith([node, point]);

        state.ReplaceWith(state.SelectedObjects.Where(objectRef =>
            objectRef.Kind != FactoryMapObjectKind.Device));

        Assert.Single(state.SelectedObjects);
        Assert.Contains(point, state.SelectedObjects);
        Assert.Equal(point, state.PrimaryObject);
    }
}
