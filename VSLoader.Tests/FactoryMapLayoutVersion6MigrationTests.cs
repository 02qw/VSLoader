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
    public void Load_version5_preserves_authored_geometry_and_routes_without_implicit_arrangement()
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
        Assert.False(loaded.Map.RequiresPersistence);
        Assert.All(loaded.Map.Devices, device =>
        {
            Assert.Equal(150, device.Width);
            Assert.Equal(58, device.Height);
        });
        var attached = loaded.Map.ConnectionPoints.Where(point => point.Kind == FactoryMapConnectionPointKinds.Attached).ToArray();
        Assert.NotEmpty(attached);
        Assert.True(new FactoryMapTopologyService().ValidateTopology(loaded.Map).IsValid);
        Assert.Contains(loaded.Map.ConnectionPoints, point => point.Kind == FactoryMapConnectionPointKinds.Bend && point.Y == 628);
    }

    [Fact]
    public void Load_version5_falls_back_to_authored_geometry_when_automatic_arrangement_cannot_route()
    {
        var path = Path.Combine(rootPath, "v5-boundary.json");
        File.WriteAllText(path, """
        {
          "version": 5,
          "devices": [
            { "id": "node-a", "key": "A", "name": "设备A", "x": 100, "y": 0 }
          ],
          "connectionPoints": [
            { "id": "bend-a", "kind": "bend", "x": 175, "y": 100 },
            { "id": "free-a", "kind": "free", "x": 300, "y": 100 }
          ],
          "segments": [
            { "id": "segment-a", "fromPointId": "node-a:top", "toPointId": "bend-a" },
            { "id": "segment-b", "fromPointId": "bend-a", "toPointId": "free-a" }
          ]
        }
        """);

        var loaded = service.LoadFromFile(
            path,
            [new ShortcutItem { Name = "设备A", TargetPath = "A" }]);

        Assert.True(loaded.Success, loaded.ErrorMessage);
        var device = Assert.Single(loaded.Map.Devices);
        Assert.Equal(150, device.Width);
        Assert.Equal(58, device.Height);
        Assert.False(loaded.Map.RequiresPersistence);
        Assert.Equal(2, loaded.Map.Segments.Count);
        Assert.True(new FactoryMapTopologyService().ValidateTopology(loaded.Map).IsValid);
    }

    [Fact]
    public void Map_runtime_model_exposes_non_blocking_load_warnings()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Models",
            "FactoryMapDeviceViewData.cs"));

        Assert.Contains("public List<string> LoadWarnings { get; set; } = [];", code);
    }

    [Fact]
    public void Save_preserves_authored_geometry_without_implicit_line_arrangement()
    {
        var path = Path.Combine(rootPath, "authored.json");
        var shortcut = new ShortcutItem { Name = "设备A", TargetPath = "A" };
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
                    Y = 0,
                    Width = 150,
                    Height = 58,
                    Shortcut = shortcut
                }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint
                {
                    Id = "node-a:top",
                    Kind = FactoryMapConnectionPointKinds.Attached,
                    OwnerNodeId = "node-a",
                    Side = FactoryMapPortKinds.Top,
                    X = 175,
                    Y = 0
                },
                new FactoryMapConnectionPoint { Id = "bend-a", Kind = FactoryMapConnectionPointKinds.Bend, X = 175, Y = 100 },
                new FactoryMapConnectionPoint { Id = "free-a", Kind = FactoryMapConnectionPointKinds.Free, X = 300, Y = 100 }
            ],
            Segments =
            [
                new FactoryMapSegment { Id = "segment-a", FromPointId = "node-a:top", ToPointId = "bend-a" },
                new FactoryMapSegment { Id = "segment-b", FromPointId = "bend-a", ToPointId = "free-a" }
            ]
        };

        var saved = service.SaveToFile(path, map);

        Assert.True(saved.Success, saved.ErrorMessage);
        var config = JsonSerializer.Deserialize<FactoryMapLayoutConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(150, Assert.Single(config.Devices).Width);
        Assert.Equal(58, Assert.Single(config.Devices).Height);
        Assert.Equal(["segment-a", "segment-b"], config.Segments.Select(segment => segment.Id));
    }

    [Fact]
    public void Load_version3_preserves_authored_spacing_when_lines_exist()
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
        Assert.Equal(150, left.Width);
        Assert.Equal(150, right.Width);
        Assert.Equal(100, left.X);
        Assert.Equal(260, right.X);
        Assert.False(loaded.Map.RequiresPersistence);
        Assert.True(new FactoryMapTopologyService().ValidateTopology(loaded.Map).IsValid);
        Assert.NotEmpty(loaded.Map.Segments);
    }

    [Fact]
    public void Layout_loading_does_not_call_the_full_line_arranger()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Models",
            "Services",
            "FactoryMapLayoutService.cs"));

        Assert.DoesNotContain("new FactoryMapLineArrangementService().ArrangeAll", code);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }
}
