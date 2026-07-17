using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapNodeGeometryServiceTests
{
    [Fact]
    public void NormalizeDevice_uses_grid_aligned_minimum_size_and_position()
    {
        var device = new FactoryMapDeviceViewNode
        {
            Name = "设备A",
            Key = "A",
            X = 103,
            Y = 117
        };

        FactoryMapNodeGeometryService.NormalizeDevice(device, 10);

        Assert.Equal(100, device.X);
        Assert.Equal(120, device.Y);
        Assert.Equal(160, device.Width);
        Assert.Equal(60, device.Height);
        Assert.Equal(0, device.Width % 20);
        Assert.Equal(0, device.Height % 20);
    }

    [Fact]
    public void NormalizeDevice_expands_long_content_in_double_grid_units()
    {
        var device = new FactoryMapDeviceViewNode
        {
            Name = "这是一个需要自动扩展节点尺寸的特别长设备名称_测试设备_001",
            Key = "\\\\server\\instances\\12345_LONG_DEVICE_CODE_001"
        };

        FactoryMapNodeGeometryService.NormalizeDevice(device, 10);

        Assert.True(device.Width > 160 || device.Height > 60);
        Assert.InRange(device.Width, 160, 320);
        Assert.Equal(0, device.Width % 20);
        Assert.Equal(0, device.Height % 20);

        var map = new FactoryMapDeviceViewData { Devices = [device] };
        FactoryMapNodeGeometryService.SynchronizeAttachedPoints(map);
        Assert.Equal(4, map.ConnectionPoints.Count);
        Assert.All(map.ConnectionPoints, point =>
        {
            Assert.Equal(0, point.X % 10);
            Assert.Equal(0, point.Y % 10);
        });
    }

    [Fact]
    public void NormalizeDevice_wraps_very_long_device_code_by_expanding_height()
    {
        var device = new FactoryMapDeviceViewNode
        {
            Name = "设备A",
            Key = new string('A', 160) + "12345"
        };

        FactoryMapNodeGeometryService.NormalizeDevice(device, 10);

        Assert.Equal(320, device.Width);
        Assert.True(device.Height > 60);
        Assert.Equal(0, device.Height % 20);
    }

    [Fact]
    public void NormalizeDevice_preserves_larger_saved_size_after_quantizing_upward()
    {
        var device = new FactoryMapDeviceViewNode
        {
            Name = "设备A",
            Width = 173,
            Height = 61
        };

        FactoryMapNodeGeometryService.NormalizeDevice(device, 10);

        Assert.Equal(180, device.Width);
        Assert.Equal(80, device.Height);
    }

    [Fact]
    public void NormalizeDevices_preserves_one_grid_gap_after_nodes_expand()
    {
        var left = new FactoryMapDeviceViewNode
        {
            Id = "left",
            Name = "左侧设备",
            X = 100,
            Y = 100,
            Width = 150,
            Height = 58
        };
        var right = new FactoryMapDeviceViewNode
        {
            Id = "right",
            Name = "右侧设备",
            X = 260,
            Y = 100,
            Width = 150,
            Height = 58
        };

        var changed = FactoryMapNodeGeometryService.NormalizeDevices([left, right], 10);

        Assert.True(changed);
        Assert.Equal(160, left.Width);
        Assert.Equal(160, right.Width);
        Assert.Equal(left.X + left.Width + 10, right.X);
        Assert.Equal(0, right.X % 10);
    }
}
