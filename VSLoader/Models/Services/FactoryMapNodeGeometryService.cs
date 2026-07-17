using VSLoader.Models;

namespace VSLoader.Services;

public static class FactoryMapNodeGeometryService
{
    public const double GridSize = 10;
    public const double MinimumWidth = 160;
    public const double MinimumHeight = 60;
    public const double MaximumAutomaticWidth = 320;
    public const double SizeQuantum = GridSize * 2;

    private const double HorizontalPadding = 16;
    private const double VerticalPadding = 8;
    private const double NameLineHeight = 15;
    private const double CodeLineHeight = 15;
    private const double EstimatedUnitWidth = 6.2;

    public static void NormalizeDevice(FactoryMapDeviceViewNode device, double gridSize = GridSize)
    {
        if (!double.IsFinite(gridSize) || gridSize <= 0)
        {
            gridSize = GridSize;
        }

        device.X = Snap(Math.Max(0, device.X), gridSize);
        device.Y = Snap(Math.Max(0, device.Y), gridSize);

        var sizeQuantum = gridSize * 2;
        var code = FactoryMapDeviceCodeParser.Parse(device.Shortcut?.TargetPath);
        if (string.IsNullOrWhiteSpace(code))
        {
            code = FactoryMapDeviceCodeParser.Parse(device.Key);
        }

        var nameWidth = EstimateTextWidth(device.Name);
        var codeWidth = EstimateTextWidth(code);
        var desiredWidth = Math.Max(
            MinimumWidth,
            Math.Max(codeWidth + HorizontalPadding, (nameWidth / 2) + HorizontalPadding));
        desiredWidth = Math.Min(MaximumAutomaticWidth, QuantizeUp(desiredWidth, sizeQuantum));

        var savedWidth = double.IsFinite(device.Width) && device.Width > 0
            ? QuantizeUp(device.Width, sizeQuantum)
            : 0;
        device.Width = Math.Max(desiredWidth, Math.Max(MinimumWidth, savedWidth));

        var availableNameWidth = Math.Max(gridSize, device.Width - HorizontalPadding);
        var nameLineCount = string.IsNullOrWhiteSpace(device.Name)
            ? 1
            : Math.Max(1, (int)Math.Ceiling(nameWidth / availableNameWidth));
        var codeLineCount = string.IsNullOrWhiteSpace(code)
            ? 0
            : Math.Max(1, (int)Math.Ceiling(codeWidth / availableNameWidth));
        var desiredHeight = VerticalPadding
            + (nameLineCount * NameLineHeight)
            + (codeLineCount * CodeLineHeight);
        var savedHeight = double.IsFinite(device.Height) && device.Height > 0
            ? QuantizeUp(device.Height, sizeQuantum)
            : 0;
        device.Height = Math.Max(
            QuantizeUp(Math.Max(MinimumHeight, desiredHeight), sizeQuantum),
            Math.Max(MinimumHeight, savedHeight));
    }

    public static bool NormalizeDevices(IList<FactoryMapDeviceViewNode> devices, double gridSize = GridSize)
    {
        if (!double.IsFinite(gridSize) || gridSize <= 0)
        {
            gridSize = GridSize;
        }

        var before = devices.ToDictionary(
            device => device,
            device => (device.X, device.Y, device.Width, device.Height));
        foreach (var device in devices)
        {
            NormalizeDevice(device, gridSize);
        }

        var placed = new List<FactoryMapDeviceViewNode>(devices.Count);
        foreach (var device in devices
                     .Select((device, index) => (Device: device, Index: index))
                     .OrderBy(item => item.Device.Y)
                     .ThenBy(item => item.Device.X)
                     .ThenBy(item => item.Index)
                     .Select(item => item.Device))
        {
            while (true)
            {
                var colliding = placed
                    .Where(other => RequiresSeparation(device, other, gridSize))
                    .ToArray();
                if (colliding.Length == 0)
                {
                    break;
                }

                device.X = QuantizeUp(
                    colliding.Max(other => other.X + GetWidth(other) + gridSize),
                    gridSize);
            }

            placed.Add(device);
        }

        return devices.Any(device =>
            before[device] != (device.X, device.Y, device.Width, device.Height));
    }

    public static double GetWidth(FactoryMapDeviceViewNode device) =>
        double.IsFinite(device.Width) && device.Width > 0 ? device.Width : MinimumWidth;

    public static double GetHeight(FactoryMapDeviceViewNode device) =>
        double.IsFinite(device.Height) && device.Height > 0 ? device.Height : MinimumHeight;

    public static void SynchronizeAttachedPoints(FactoryMapDeviceViewData map)
    {
        var deviceIds = map.Devices.Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        map.ConnectionPoints.RemoveAll(point =>
            point.Kind == FactoryMapConnectionPointKinds.Attached
            && !deviceIds.Contains(point.OwnerNodeId));
        foreach (var device in map.Devices)
        {
            foreach (var side in FactoryMapPortKinds.All)
            {
                var pointId = FactoryMapLayoutTopologyConverter.CreateAttachedPointId(device.Id, side);
                var position = FactoryMapEndpointGeometryService.GetPortPoint(
                    FactoryMapEndpointViewData.FromDevice(device),
                    side);
                var point = map.ConnectionPoints.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, pointId, StringComparison.OrdinalIgnoreCase));
                if (point is null)
                {
                    map.ConnectionPoints.Add(new FactoryMapConnectionPoint
                    {
                        Id = pointId,
                        Kind = FactoryMapConnectionPointKinds.Attached,
                        OwnerNodeId = device.Id,
                        Side = side,
                        X = position.X,
                        Y = position.Y
                    });
                    continue;
                }

                point.Kind = FactoryMapConnectionPointKinds.Attached;
                point.OwnerNodeId = device.Id;
                point.Side = side;
                point.X = position.X;
                point.Y = position.Y;
            }
        }
    }

    private static double EstimateTextWidth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var units = 0d;
        foreach (var character in value.Trim())
        {
            units += character <= 0x7f ? 1 : 2;
        }

        return units * EstimatedUnitWidth;
    }

    private static double QuantizeUp(double value, double quantum) =>
        Math.Ceiling(value / quantum) * quantum;

    private static bool RequiresSeparation(
        FactoryMapDeviceViewNode first,
        FactoryMapDeviceViewNode second,
        double gap)
    {
        var overlapsHorizontally = first.X < second.X + GetWidth(second) + gap
            && second.X < first.X + GetWidth(first) + gap;
        var overlapsVertically = first.Y < second.Y + GetHeight(second) + gap
            && second.Y < first.Y + GetHeight(first) + gap;
        return overlapsHorizontally && overlapsVertically;
    }

    private static double Snap(double value, double gridSize) =>
        Math.Round(value / gridSize, MidpointRounding.AwayFromZero) * gridSize;
}
