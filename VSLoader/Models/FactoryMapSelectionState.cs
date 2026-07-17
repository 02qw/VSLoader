namespace VSLoader.Models;

public sealed class FactoryMapSelectionState
{
    private readonly HashSet<FactoryMapObjectRef> selectedObjects = [];

    public IReadOnlySet<FactoryMapObjectRef> SelectedObjects => selectedObjects;

    public FactoryMapObjectRef? PrimaryObject { get; private set; }

    public void Select(FactoryMapObjectRef objectRef)
    {
        selectedObjects.Clear();
        selectedObjects.Add(objectRef);
        PrimaryObject = objectRef;
    }

    public void Toggle(FactoryMapObjectRef objectRef)
    {
        if (!selectedObjects.Add(objectRef))
        {
            selectedObjects.Remove(objectRef);
            PrimaryObject = selectedObjects.Count == 0 ? null : selectedObjects.Last();
            return;
        }

        PrimaryObject = objectRef;
    }

    public void ReplaceWith(IEnumerable<FactoryMapObjectRef> objectRefs)
    {
        var replacements = objectRefs.ToArray();
        selectedObjects.Clear();
        PrimaryObject = null;
        AddRange(replacements);
    }

    public void AddRange(IEnumerable<FactoryMapObjectRef> objectRefs)
    {
        foreach (var objectRef in objectRefs)
        {
            if (!selectedObjects.Add(objectRef))
            {
                continue;
            }

            PrimaryObject = objectRef;
        }
    }

    public bool Remove(FactoryMapObjectRef objectRef)
    {
        if (!selectedObjects.Remove(objectRef))
        {
            return false;
        }

        if (PrimaryObject == objectRef)
        {
            PrimaryObject = selectedObjects.Count == 0 ? null : selectedObjects.Last();
        }

        return true;
    }

    public void Clear()
    {
        selectedObjects.Clear();
        PrimaryObject = null;
    }

    public bool Contains(FactoryMapObjectRef objectRef) => selectedObjects.Contains(objectRef);
}
