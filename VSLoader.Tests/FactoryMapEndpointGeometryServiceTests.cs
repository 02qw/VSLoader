using System.Windows;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapEndpointGeometryServiceTests
{
    [Fact]
    public void GetCenter_returns_device_center()
    {
        var endpoint = FactoryMapEndpointViewData.FromDevice(new FactoryMapDeviceViewNode
        {
            Key = "A",
            X = 100,
            Y = 200
        });

        var center = FactoryMapEndpointGeometryService.GetCenter(endpoint);

        Assert.Equal(new Point(180, 230), center);
    }

    [Fact]
    public void Device_geometry_uses_each_nodes_grid_aligned_size()
    {
        var endpoint = FactoryMapEndpointViewData.FromDevice(new FactoryMapDeviceViewNode
        {
            Key = "A",
            X = 100,
            Y = 200,
            Width = 180,
            Height = 80
        });

        Assert.Equal(new Point(190, 240), FactoryMapEndpointGeometryService.GetCenter(endpoint));
        Assert.Equal(new Point(190, 200), FactoryMapEndpointGeometryService.GetPortPoint(endpoint, FactoryMapPortKinds.Top));
        Assert.Equal(new Point(280, 240), FactoryMapEndpointGeometryService.GetPortPoint(endpoint, FactoryMapPortKinds.Right));
        Assert.Equal(new Point(190, 280), FactoryMapEndpointGeometryService.GetPortPoint(endpoint, FactoryMapPortKinds.Bottom));
        Assert.Equal(new Point(100, 240), FactoryMapEndpointGeometryService.GetPortPoint(endpoint, FactoryMapPortKinds.Left));
    }

    [Fact]
    public void GetCenter_returns_connector_center()
    {
        var endpoint = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode
        {
            Id = "cp_1",
            X = 240,
            Y = 180
        });

        var center = FactoryMapEndpointGeometryService.GetCenter(endpoint);

        Assert.Equal(new Point(240, 180), center);
    }

    [Fact]
    public void GetEdgeStart_and_GetEdgeEnd_use_inferred_ports_for_legacy_callers()
    {
        var device = FactoryMapEndpointViewData.FromDevice(new FactoryMapDeviceViewNode
        {
            Key = "A",
            X = 100,
            Y = 200
        });
        var connector = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode
        {
            Id = "cp_1",
            X = 320,
            Y = 240
        });

        var start = FactoryMapEndpointGeometryService.GetEdgeStart(device, connector);
        var end = FactoryMapEndpointGeometryService.GetEdgeEnd(connector, device);

        Assert.Equal(new Point(260, 230), start);
        Assert.Equal(new Point(315, 240), end);
    }

    [Theory]
    [InlineData(FactoryMapPortKinds.Top, 180, 200)]
    [InlineData(FactoryMapPortKinds.Right, 260, 230)]
    [InlineData(FactoryMapPortKinds.Bottom, 180, 260)]
    [InlineData(FactoryMapPortKinds.Left, 100, 230)]
    public void GetPortPoint_returns_device_edge_center_for_each_port(string port, double expectedX, double expectedY)
    {
        var endpoint = FactoryMapEndpointViewData.FromDevice(new FactoryMapDeviceViewNode
        {
            Key = "A",
            X = 100,
            Y = 200
        });

        var point = FactoryMapEndpointGeometryService.GetPortPoint(endpoint, port);

        Assert.Equal(new Point(expectedX, expectedY), point);
    }

    [Theory]
    [InlineData(FactoryMapPortKinds.Top, 240, 175)]
    [InlineData(FactoryMapPortKinds.Right, 245, 180)]
    [InlineData(FactoryMapPortKinds.Bottom, 240, 185)]
    [InlineData(FactoryMapPortKinds.Left, 235, 180)]
    public void GetPortPoint_returns_connector_virtual_port_for_each_port(string port, double expectedX, double expectedY)
    {
        var endpoint = FactoryMapEndpointViewData.FromConnector(new FactoryMapConnectorViewNode
        {
            Id = "cp_1",
            X = 240,
            Y = 180
        });

        var point = FactoryMapEndpointGeometryService.GetPortPoint(endpoint, port);

        Assert.Equal(new Point(expectedX, expectedY), point);
    }

    [Fact]
    public void Infer_ports_use_vertical_direction_when_vertical_gap_is_larger()
    {
        var top = FactoryMapEndpointViewData.FromDevice(new FactoryMapDeviceViewNode { Key = "A", X = 100, Y = 100 });
        var bottom = FactoryMapEndpointViewData.FromDevice(new FactoryMapDeviceViewNode { Key = "B", X = 110, Y = 300 });

        Assert.Equal(FactoryMapPortKinds.Bottom, FactoryMapEndpointGeometryService.InferOutgoingPort(top, bottom));
        Assert.Equal(FactoryMapPortKinds.Top, FactoryMapEndpointGeometryService.InferIncomingPort(top, bottom));
    }

    [Fact]
    public void Infer_ports_use_horizontal_direction_when_horizontal_gap_is_larger()
    {
        var left = FactoryMapEndpointViewData.FromDevice(new FactoryMapDeviceViewNode { Key = "A", X = 100, Y = 100 });
        var right = FactoryMapEndpointViewData.FromDevice(new FactoryMapDeviceViewNode { Key = "B", X = 360, Y = 120 });

        Assert.Equal(FactoryMapPortKinds.Right, FactoryMapEndpointGeometryService.InferOutgoingPort(left, right));
        Assert.Equal(FactoryMapPortKinds.Left, FactoryMapEndpointGeometryService.InferIncomingPort(left, right));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad")]
    public void NormalizePort_falls_back_to_right_for_invalid_values(string? port)
    {
        Assert.Equal(FactoryMapPortKinds.Right, FactoryMapEndpointGeometryService.NormalizePort(port));
    }
}
