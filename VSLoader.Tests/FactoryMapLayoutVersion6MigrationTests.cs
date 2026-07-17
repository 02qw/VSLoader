using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapLayoutVersion6MigrationTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
    private readonly FactoryMapLayoutService service = new();

    public FactoryMapLayoutVersion6MigrationTests()
    {
        Directory.CreateDirectory(rootPath);
    }

    [Fact]
    public void Save_writes_version6_and_persists_grid_aligned_node_size()
    {
        var path = Path.Combine(rootPath, "v6.json");
        var map = new FactoryMapDeviceViewData
        {
            TopologyAuthoritative = true,
            Devices =
            [
                new FactoryMapDeviceViewNode
                {
                    Id = "node-a",
                    Key = "A",
                    Name = "设备A",
                    X = 100,
                    Y = 120,
                    Width = 180,
                    Height = 80,
                    Shortcut = new ShortcutItem { Name = "设备A", TargetPath = "A" }
                }
            ]
        };

        var result = service.SaveToFile(path, map);

        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(6, document.RootElement.GetProperty("version").GetInt32());
        var device = document.RootElement.GetProperty("devices")[0];
        Assert.Equal(180, device.GetProperty("width").GetDouble());
        Assert.Equal(80, device.GetProperty("height").GetDouble());
    }

    [Fact]
    public void Load_version5_migrates_node_geometry_and_rearranges_attached_routes()
    {
        var path = Path.Combine(rootPath, "v5.json");
        File.WriteAllText(path, """
        {
          "version": 5,
          "devices": [
            { "id": "source", "key": "A", "name": "设备A", "x": 860, "y": 550 },
            { "id": "target", "key": "B", "name": "设备B", "x": 700, "y": 700 }
          ],
          "connectionPoints": [
            { "id": "old-a", "kind": "bend", "x": 935, "y": 628 },
            { "id": "old-b", "kind": "bend", "x": 775, "y": 628 }
          ],
          "segments": [
            { "id": "old-1", "fromPointId": "source:bottom", "toPointId": "old-a" },
            { "id": "old-2", "fromPointId": "old-a", "toPointId": "old-b" },
            { "id": "old-3", "fromPointId": "old-b", "toPointId": "target:top" }
          ]
        }
        """);

        var loaded = service.LoadFromFile(
            path,
            [
                new ShortcutItem { Name = "设备A", TargetPath = "A" },
                new ShortcutItem { Name = "设备B", TargetPath = "B" }
            ]);

        Assert.True(loaded.Success, loaded.ErrorMessage);
        Assert.True(loaded.Map.RequiresPersistence);
        Assert.All(loaded.Map.Devices, device =>
        {
            Assert.Equal(160, device.Width);
            Assert.Equal(60, device.Height);
            Assert.Equal(0, device.X % 10);
            Assert.Equal(0, device.Y % 10);
        });
        var attached = loaded.Map.ConnectionPoints.Where(point => point.Kind == FactoryMapConnectionPointKinds.Attached).ToArray();
        Assert.NotEmpty(attached);
        Assert.All(attached, point =>
        {
            Assert.Equal(0, point.X % 10);
            Assert.Equal(0, point.Y % 10);
        });
        Assert.True(new FactoryMapTopologyService().ValidateTopology(loaded.Map).IsValid);
        Assert.DoesNotContain(loaded.Map.ConnectionPoints, point => point.Kind == FactoryMapConnectionPointKinds.Bend && point.Y == 628);
    }

    [Fact]
    public void Load_version3_separates_nodes_whose_ports_would_overlap_after_expansion()
    {
        var path = Path.Combine(rootPath, "touching-v3.json");
        File.WriteAllText(path, """
        {
          "version": 3,
          "devices": [
            { "key": "A", "name": "设备A", "x": 100, "y": 100 },
            { "key": "B", "name": "设备B", "x": 260, "y": 100 }
          ],
          "edges": [
            {
              "from": "A", "fromKind": "device", "fromPort": "right",
              "to": "B", "toKind": "device", "toPort": "left",
              "points": []
            }
          ]
        }
        """);

        var loaded = service.LoadFromFile(
            path,
            [
                new ShortcutItem { Name = "设备A", TargetPath = "A" },
                new ShortcutItem { Name = "设备B", TargetPath = "B" }
            ]);

        Assert.True(loaded.Success, loaded.ErrorMessage);
        var left = loaded.Map.Devices.Single(device => device.Key == "A");
        var right = loaded.Map.Devices.Single(device => device.Key == "B");
        Assert.True(right.X >= left.X + left.Width + 10);
        Assert.True(new FactoryMapTopologyService().ValidateTopology(loaded.Map).IsValid);
        Assert.NotEmpty(loaded.Map.Segments);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }
}
