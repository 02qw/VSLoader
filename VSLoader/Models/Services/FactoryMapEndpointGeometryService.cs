using VSLoader.Models;
using WpfPoint = System.Windows.Point;
using WpfVector = System.Windows.Vector;

namespace VSLoader.Services;

internal static class FactoryMapEndpointGeometryService
{
    public const double DeviceWidth = FactoryMapNodeGeometryService.MinimumWidth;
    public const double DeviceHeight = FactoryMapNodeGeometryService.MinimumHeight;
    public const double ConnectorRadius = 5;

    public static WpfPoint GetCenter(FactoryMapEndpointViewData endpoint)
    {
        if (endpoint.Device is not null)
        {
            var width = FactoryMapNodeGeometryService.GetWidth(endpoint.Device);
            var height = FactoryMapNodeGeometryService.GetHeight(endpoint.Device);
            return new WpfPoint(
                endpoint.Device.X + width / 2,
                endpoint.Device.Y + height / 2);
        }

        return new WpfPoint(endpoint.X, endpoint.Y);
    }

    public static WpfPoint GetEdgeStart(FactoryMapEndpointViewData endpoint, FactoryMapEndpointViewData other)
    {
        return GetPortPoint(endpoint, InferOutgoingPort(endpoint, other));
    }

    public static WpfPoint GetEdgeEnd(FactoryMapEndpointViewData endpoint, FactoryMapEndpointViewData other)
    {
        return GetPortPoint(endpoint, InferIncomingPort(other, endpoint));
    }

    public static WpfPoint GetPortPoint(FactoryMapEndpointViewData endpoint, string port)
    {
        var normalizedPort = NormalizePort(port);
        if (endpoint.Device is null)
        {
            var center = GetCenter(endpoint);
            return normalizedPort switch
            {
                FactoryMapPortKinds.Top => new WpfPoint(center.X, center.Y - ConnectorRadius),
                FactoryMapPortKinds.Right => new WpfPoint(center.X + ConnectorRadius, center.Y),
                FactoryMapPortKinds.Bottom => new WpfPoint(center.X, center.Y + ConnectorRadius),
                FactoryMapPortKinds.Left => new WpfPoint(center.X - ConnectorRadius, center.Y),
                _ => center
            };
        }

        var width = FactoryMapNodeGeometryService.GetWidth(endpoint.Device);
        var height = FactoryMapNodeGeometryService.GetHeight(endpoint.Device);
        return normalizedPort switch
        {
            FactoryMapPortKinds.Top => new WpfPoint(endpoint.Device.X + width / 2, endpoint.Device.Y),
            FactoryMapPortKinds.Right => new WpfPoint(endpoint.Device.X + width, endpoint.Device.Y + height / 2),
            FactoryMapPortKinds.Bottom => new WpfPoint(endpoint.Device.X + width / 2, endpoint.Device.Y + height),
            FactoryMapPortKinds.Left => new WpfPoint(endpoint.Device.X, endpoint.Device.Y + height / 2),
            _ => GetCenter(endpoint)
        };
    }

    public static string InferOutgoingPort(FactoryMapEndpointViewData from, FactoryMapEndpointViewData to)
    {
        var fromCenter = GetCenter(from);
        var toCenter = GetCenter(to);
        var dx = toCenter.X - fromCenter.X;
        var dy = toCenter.Y - fromCenter.Y;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0
                ? FactoryMapPortKinds.Right
                : FactoryMapPortKinds.Left;
        }

        return dy >= 0
            ? FactoryMapPortKinds.Bottom
            : FactoryMapPortKinds.Top;
    }

    public static string InferIncomingPort(FactoryMapEndpointViewData from, FactoryMapEndpointViewData to)
    {
        return OppositePort(InferOutgoingPort(from, to));
    }

    public static string NormalizePort(string? port, string fallback = FactoryMapPortKinds.Right)
    {
        return port?.Trim().ToLowerInvariant() switch
        {
            FactoryMapPortKinds.Top => FactoryMapPortKinds.Top,
            FactoryMapPortKinds.Right => FactoryMapPortKinds.Right,
            FactoryMapPortKinds.Bottom => FactoryMapPortKinds.Bottom,
            FactoryMapPortKinds.Left => FactoryMapPortKinds.Left,
            _ => fallback
        };
    }

    public static bool IsKnownPort(string? port)
    {
        return port?.Trim().ToLowerInvariant() switch
        {
            FactoryMapPortKinds.Top or FactoryMapPortKinds.Right or FactoryMapPortKinds.Bottom or FactoryMapPortKinds.Left => true,
            _ => false
        };
    }

    public static string OppositePort(string? port)
    {
        return NormalizePort(port) switch
        {
            FactoryMapPortKinds.Top => FactoryMapPortKinds.Bottom,
            FactoryMapPortKinds.Right => FactoryMapPortKinds.Left,
            FactoryMapPortKinds.Bottom => FactoryMapPortKinds.Top,
            FactoryMapPortKinds.Left => FactoryMapPortKinds.Right,
            _ => FactoryMapPortKinds.Left
        };
    }

    public static bool TryGetOutwardDirection(string? port, out WpfVector direction)
    {
        direction = port?.Trim().ToLowerInvariant() switch
        {
            FactoryMapPortKinds.Top => new WpfVector(0, -1),
            FactoryMapPortKinds.Right => new WpfVector(1, 0),
            FactoryMapPortKinds.Bottom => new WpfVector(0, 1),
            FactoryMapPortKinds.Left => new WpfVector(-1, 0),
            _ => default
        };
        return direction != default;
    }
}
