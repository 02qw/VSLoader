using System.Windows;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class FactoryMapMarqueeSelectionService
{
    public IReadOnlyList<FactoryMapObjectRef> GetSelection(
        FactoryMapDeviceViewData map,
        Rect selectionRect,
        double pointSize)
    {
        if (selectionRect.IsEmpty
            || pointSize <= 0)
        {
            return [];
        }

        var selected = new List<FactoryMapObjectRef>();
        foreach (var device in map.Devices)
        {
            var bounds = new Rect(
                device.X,
                device.Y,
                FactoryMapNodeGeometryService.GetWidth(device),
                FactoryMapNodeGeometryService.GetHeight(device));
            if (FactoryMapEditMath.RectIntersects(selectionRect, bounds))
            {
                selected.Add(new FactoryMapObjectRef(FactoryMapObjectKind.Device, device.Id));
            }
        }

        var pointRadius = pointSize / 2;
        foreach (var point in map.ConnectionPoints.Where(point =>
                     point.Kind == FactoryMapConnectionPointKinds.Free))
        {
            var bounds = new Rect(
                point.X - pointRadius,
                point.Y - pointRadius,
                pointSize,
                pointSize);
            if (FactoryMapEditMath.RectIntersects(selectionRect, bounds))
            {
                selected.Add(new FactoryMapObjectRef(FactoryMapObjectKind.ConnectionPoint, point.Id));
            }
        }

        return selected;
    }
}
