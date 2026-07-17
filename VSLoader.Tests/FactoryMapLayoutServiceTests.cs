using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapLayoutServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FactoryMapLayoutService _service = new();

    public FactoryMapLayoutServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void LoadOrCreate_generates_device_nodes_from_shortcuts_when_layout_missing()
    {
        var layoutPath = Path.Combine(_rootPath, "factory-map.layout.json");
        var shortcuts = new[]
        {
            new ShortcutItem { Name = "热贴机_001", TargetPath = @"\\server\instances\1001_TSSM001" },
            new ShortcutItem { Name = "印刷机_002", TargetPath = @"\\server\instances\1002_TPMA002" }
        };

        var result = _service.LoadOrCreate(layoutPath, shortcuts);

        Assert.True(result.Success);
        Assert.Equal(2, result.Map.Devices.Count);
        Assert.Contains(result.Map.Devices, device => device.Key == shortcuts[0].TargetPath && device.Name == "热贴机_001");
        Assert.Contains(result.Map.Devices, device => device.Key == shortcuts[1].TargetPath && device.Name == "印刷机_002");
        Assert.All(result.Map.Devices, device => Assert.NotNull(device.Shortcut));
    }

    [Fact]
    public void LoadOrCreate_uses_saved_position_and_adds_missing_shortcuts()
    {
        var layoutPath = WriteLayoutJson("""
{
  "version": 2,
  "canvas": { "width": 1600, "height": 900 },
  "devices": [
    { "key": "\\\\server\\instances\\1001_TSSM001", "name": "旧名称", "x": 320, "y": 240 }
  ],
  "edges": []
}
""");
        var shortcuts = new[]
        {
            new ShortcutItem { Name = "热贴机_001", TargetPath = @"\\server\instances\1001_TSSM001" },
            new ShortcutItem { Name = "印刷机_002", TargetPath = @"\\server\instances\1002_TPMA002" }
        };

        var result = _service.LoadOrCreate(layoutPath, shortcuts);

        Assert.True(result.Success);
        Assert.Equal(2, result.Map.Devices.Count);
        var savedDevice = Assert.Single(result.Map.Devices, device => device.Key == shortcuts[0].TargetPath);
        Assert.Equal("热贴机_001", savedDevice.Name);
        Assert.Equal(320, savedDevice.X);
        Assert.Equal(240, savedDevice.Y);
        Assert.Contains(result.Map.Devices, device => device.Key == shortcuts[1].TargetPath);
    }

    [Fact]
    public void LoadOrCreate_counts_edges_that_reference_missing_devices()
    {
        var layoutPath = WriteLayoutJson("""
{
  "version": 2,
  "canvas": { "width": 1600, "height": 900 },
  "devices": [
    { "key": "A", "name": "A", "x": 100, "y": 100 }
  ],
  "edges": [
    { "from": "A", "to": "B" }
  ]
}
""");
        var shortcuts = new[]
        {
            new ShortcutItem { Name = "A_001", TargetPath = "A" }
        };

        var result = _service.LoadOrCreate(layoutPath, shortcuts);

        Assert.True(result.Success);
        Assert.Empty(result.Map.Edges);
        Assert.Equal(1, result.Map.InvalidEdgeCount);
    }

    [Fact]
    public void LoadFromFile_keeps_multiple_edges_from_same_device()
    {
        var layoutPath = WriteLayoutJson("""
{
  "version": 3,
  "canvas": { "width": 1600, "height": 900 },
  "devices": [
    { "key": "A", "name": "旧A", "x": 100, "y": 100 },
    { "key": "B", "name": "旧B", "x": 300, "y": 100 },
    { "key": "C", "name": "旧C", "x": 300, "y": 220 }
  ],
  "edges": [
    { "from": "A", "to": "B" },
    { "from": "A", "to": "C" }
  ]
}
""");
        var shortcuts = new[]
        {
            new ShortcutItem { Name = "设备A", TargetPath = "A" },
            new ShortcutItem { Name = "设备B", TargetPath = "B" },
            new ShortcutItem { Name = "设备C", TargetPath = "C" }
        };

        var result = _service.LoadFromFile(layoutPath, shortcuts);

        Assert.True(result.Success);
        Assert.Equal(2, result.Map.Edges.Count);
        Assert.All(result.Map.Edges, edge => Assert.Equal("A", edge.From.Key));
        Assert.Contains(result.Map.Edges, edge => edge.To.Key == "B");
        Assert.Contains(result.Map.Edges, edge => edge.To.Key == "C");
    }

    [Fact]
    public void LoadFromFile_skips_edges_that_reference_missing_current_shortcuts()
    {
        var layoutPath = WriteLayoutJson("""
{
  "version": 3,
  "canvas": { "width": 1600, "height": 900 },
  "devices": [
    { "key": "A", "name": "A", "x": 100, "y": 100 },
    { "key": "B", "name": "B", "x": 300, "y": 100 },
    { "key": "Missing", "name": "Missing", "x": 500, "y": 100 }
  ],
  "edges": [
    { "from": "A", "to": "B" },
    { "from": "A", "to": "Missing" }
  ]
}
""");
        var shortcuts = new[]
        {
            new ShortcutItem { Name = "设备A", TargetPath = "A" },
            new ShortcutItem { Name = "设备B", TargetPath = "B" }
        };

        var result = _service.LoadFromFile(layoutPath, shortcuts);

        Assert.True(result.Success);
        var edge = Assert.Single(result.Map.Edges);
        Assert.Equal("A", edge.From.Key);
        Assert.Equal("B", edge.To.Key);
        Assert.Equal(1, result.SkippedDeviceCount);
        Assert.Equal(1, result.SkippedEdgeCount);
    }

    [Fact]
    public void LoadFromFile_removes_duplicate_edges()
    {
        var layoutPath = WriteLayoutJson("""
{
  "version": 3,
  "canvas": { "width": 1600, "height": 900 },
  "devices": [
    { "key": "A", "name": "A", "x": 100, "y": 100 },
    { "key": "B", "name": "B", "x": 300, "y": 100 }
  ],
  "edges": [
    { "from": "A", "to": "B" },
    { "from": "A", "to": "B" },
    { "from": "B", "to": "A" }
  ]
}
""");
        var shortcuts = new[]
        {
            new ShortcutItem { Name = "设备A", TargetPath = "A" },
            new ShortcutItem { Name = "设备B", TargetPath = "B" }
        };

        var result = _service.LoadFromFile(layoutPath, shortcuts);

        Assert.True(result.Success);
        Assert.Equal(2, result.Map.Edges.Count);
        Assert.Single(result.Map.Segments);
        Assert.Contains(result.Map.Edges, edge => edge.From.Key == "A" && edge.To.Key == "B");
        Assert.Contains(result.Map.Edges, edge => edge.From.Key == "B" && edge.To.Key == "A");
        Assert.Equal(2, result.SkippedEdgeCount);
    }

    [Fact]
    public void SaveToFile_and_LoadFromFile_roundtrip_edges()
    {
        var layoutPath = Path.Combine(_rootPath, "exported-map.json");
        var shortcutA = new ShortcutItem { Name = "设备A", TargetPath = "A" };
        var shortcutB = new ShortcutItem { Name = "设备B", TargetPath = "B" };
        var nodeA = new FactoryMapDeviceViewNode { Key = "A", Name = "设备A", X = 100, Y = 120, Shortcut = shortcutA };
        var nodeB = new FactoryMapDeviceViewNode { Key = "B", Name = "设备B", X = 320, Y = 120, Shortcut = shortcutB };
        var map = new FactoryMapDeviceViewData
        {
            Canvas = new FactoryMapCanvas { Width = 1600, Height = 900 },
            Devices = [nodeA, nodeB],
            Edges = [new FactoryMapDeviceEdgeViewData { From = nodeA, To = nodeB }]
        };

        var saveResult = _service.SaveToFile(layoutPath, map);
        var loadResult = _service.LoadFromFile(layoutPath, [shortcutA, shortcutB]);

        Assert.True(saveResult.Success);
        Assert.True(loadResult.Success);
        var edge = Assert.Single(loadResult.Map.Edges);
        Assert.Equal("A", edge.From.Key);
        Assert.Equal("B", edge.To.Key);
        var loadedA = Assert.Single(loadResult.Map.Devices, device => device.Key == "A");
        Assert.Equal(100, loadedA.X);
        Assert.Equal(120, loadedA.Y);
    }

    [Fact]
    public void SaveToFile_and_LoadFromFile_roundtrip_edge_points()
    {
        var layoutPath = Path.Combine(_rootPath, "edge-points-map.json");
        var shortcutA = new ShortcutItem { Name = "设备A", TargetPath = "A" };
        var shortcutB = new ShortcutItem { Name = "设备B", TargetPath = "B" };
        var nodeA = new FactoryMapDeviceViewNode { Key = "A", Name = "设备A", X = 100, Y = 120, Shortcut = shortcutA };
        var nodeB = new FactoryMapDeviceViewNode { Key = "B", Name = "设备B", X = 320, Y = 120, Shortcut = shortcutB };
        var map = new FactoryMapDeviceViewData
        {
            Canvas = new FactoryMapCanvas { Width = 1600, Height = 900 },
            Devices = [nodeA, nodeB],
            Edges =
            [
                new FactoryMapDeviceEdgeViewData
                {
                    From = nodeA,
                    To = nodeB,
                    Points =
                    [
                        new FactoryMapPoint { X = 250, Y = 240 },
                        new FactoryMapPoint { X = 320, Y = 240 }
                    ]
                }
            ]
        };

        var saveResult = _service.SaveToFile(layoutPath, map);
        var loadResult = _service.LoadFromFile(layoutPath, [shortcutA, shortcutB]);

        Assert.True(saveResult.Success);
        using var document = JsonDocument.Parse(File.ReadAllText(layoutPath));
        var points = document.RootElement.GetProperty("connectionPoints");
        Assert.NotEmpty(points.EnumerateArray());
        Assert.All(points.EnumerateArray(), point => Assert.Equal("bend", point.GetProperty("kind").GetString()));
        Assert.True(document.RootElement.GetProperty("segments").GetArrayLength() >= 3);

        Assert.True(loadResult.Success);
        var edge = Assert.Single(loadResult.Map.Edges);
        Assert.True(edge.Points.Count >= 2);
        Assert.Contains(edge.Points, point => point.X == 250);
        Assert.Contains(edge.Points, point => point.Y == 240);
    }

    [Fact]
    public void SaveToFile_and_LoadFromFile_roundtrip_edge_ports()
    {
        var layoutPath = Path.Combine(_rootPath, "edge-ports-map.json");
        var shortcutA = new ShortcutItem { Name = "设备A", TargetPath = "A" };
        var shortcutB = new ShortcutItem { Name = "设备B", TargetPath = "B" };
        var nodeA = new FactoryMapDeviceViewNode { Key = "A", Name = "设备A", X = 100, Y = 120, Shortcut = shortcutA };
        var nodeB = new FactoryMapDeviceViewNode { Key = "B", Name = "设备B", X = 120, Y = 320, Shortcut = shortcutB };
        var map = new FactoryMapDeviceViewData
        {
            Canvas = new FactoryMapCanvas { Width = 1600, Height = 900 },
            Devices = [nodeA, nodeB],
            Edges =
            [
                new FactoryMapDeviceEdgeViewData
                {
                    From = nodeA,
                    FromPort = FactoryMapPortKinds.Bottom,
                    To = nodeB,
                    ToPort = FactoryMapPortKinds.Top
                }
            ]
        };

        var saveResult = _service.SaveToFile(layoutPath, map);
        var loadResult = _service.LoadFromFile(layoutPath, [shortcutA, shortcutB]);

        Assert.True(saveResult.Success);
        using var document = JsonDocument.Parse(File.ReadAllText(layoutPath));
        var savedSegments = document.RootElement.GetProperty("segments").EnumerateArray().ToList();
        Assert.Contains(savedSegments, segment =>
            segment.GetProperty("fromPointId").GetString()!.EndsWith(":bottom", StringComparison.Ordinal)
            || segment.GetProperty("toPointId").GetString()!.EndsWith(":bottom", StringComparison.Ordinal));
        Assert.Contains(savedSegments, segment =>
            segment.GetProperty("fromPointId").GetString()!.EndsWith(":top", StringComparison.Ordinal)
            || segment.GetProperty("toPointId").GetString()!.EndsWith(":top", StringComparison.Ordinal));

        Assert.True(loadResult.Success);
        var loadedEdge = Assert.Single(loadResult.Map.Edges);
        Assert.Equal(FactoryMapPortKinds.Bottom, loadedEdge.FromPort);
        Assert.Equal(FactoryMapPortKinds.Top, loadedEdge.ToPort);
    }

    [Fact]
    public void LoadFromFile_infers_ports_for_legacy_edge_without_ports()
    {
        var layoutPath = WriteLayoutJson("""
{
  "version": 3,
  "canvas": { "width": 1600, "height": 900 },
  "devices": [
    { "key": "A", "name": "A", "x": 100, "y": 100 },
    { "key": "B", "name": "B", "x": 110, "y": 320 }
  ],
  "edges": [
    { "from": "A", "to": "B" }
  ]
}
""");

        var result = _service.LoadFromFile(layoutPath, [
            new ShortcutItem { Name = "设备A", TargetPath = "A" },
            new ShortcutItem { Name = "设备B", TargetPath = "B" }
        ]);

        Assert.True(result.Success);
        var edge = Assert.Single(result.Map.Edges);
        Assert.Equal(FactoryMapPortKinds.Bottom, edge.FromPort);
        Assert.Equal(FactoryMapPortKinds.Top, edge.ToPort);
    }

    [Fact]
    public void LoadFromFile_normalizes_invalid_ports_to_inferred_ports()
    {
        var layoutPath = WriteLayoutJson("""
{
  "version": 3,
  "canvas": { "width": 1600, "height": 900 },
  "devices": [
    { "key": "A", "name": "A", "x": 400, "y": 100 },
    { "key": "B", "name": "B", "x": 100, "y": 120 }
  ],
  "edges": [
    { "from": "A", "fromPort": "bad", "to": "B", "toPort": "bad" }
  ]
}
""");

        var result = _service.LoadFromFile(layoutPath, [
            new ShortcutItem { Name = "设备A", TargetPath = "A" },
            new ShortcutItem { Name = "设备B", TargetPath = "B" }
        ]);

        Assert.True(result.Success);
        var edge = Assert.Single(result.Map.Edges);
        Assert.Equal(FactoryMapPortKinds.Left, edge.FromPort);
        Assert.Equal(FactoryMapPortKinds.Right, edge.ToPort);
    }

    [Fact]
    public void SaveToFile_and_LoadFromFile_roundtrip_connectors_and_connector_edges()
    {
        var layoutPath = Path.Combine(_rootPath, "connector-map.json");
        var shortcutA = new ShortcutItem { Name = "设备A", TargetPath = "A" };
        var shortcutB = new ShortcutItem { Name = "设备B", TargetPath = "B" };
        var nodeA = new FactoryMapDeviceViewNode { Key = "A", Name = "设备A", X = 100, Y = 120, Shortcut = shortcutA };
        var nodeB = new FactoryMapDeviceViewNode { Key = "B", Name = "设备B", X = 320, Y = 120, Shortcut = shortcutB };
        var connector = new FactoryMapConnectorViewNode { Id = "cp_test001", X = 240, Y = 180 };
        var map = new FactoryMapDeviceViewData
        {
            Canvas = new FactoryMapCanvas { Width = 1600, Height = 900 },
            Devices = [nodeA, nodeB],
            Connectors = [connector],
            Edges =
            [
                new FactoryMapDeviceEdgeViewData { From = nodeA, To = connector },
                new FactoryMapDeviceEdgeViewData { From = connector, To = nodeB }
            ]
        };

        var saveResult = _service.SaveToFile(layoutPath, map);
        var loadResult = _service.LoadFromFile(layoutPath, [shortcutA, shortcutB]);

        Assert.True(saveResult.Success);
        using var document = JsonDocument.Parse(File.ReadAllText(layoutPath));
        Assert.Equal(6, document.RootElement.GetProperty("version").GetInt32());
        var savedConnector = document.RootElement.GetProperty("connectionPoints")
            .EnumerateArray()
            .Single(point => point.GetProperty("id").GetString() == "cp_test001");
        Assert.Equal("cp_test001", savedConnector.GetProperty("id").GetString());
        Assert.Equal("free", savedConnector.GetProperty("kind").GetString());
        Assert.True(document.RootElement.GetProperty("segments").GetArrayLength() >= 2);

        Assert.True(loadResult.Success);
        var loadedConnector = Assert.Single(loadResult.Map.Connectors);
        Assert.Equal("cp_test001", loadedConnector.Id);
        Assert.Equal(240, loadedConnector.X);
        Assert.Equal(2, loadResult.Map.Edges.Count);
        Assert.All(loadResult.Map.Edges, edge => Assert.True(
            edge.From.Kind == FactoryMapEndpointKinds.Connector
            || edge.To.Kind == FactoryMapEndpointKinds.Connector));
        Assert.Contains(loadResult.Map.Edges, edge => edge.From.Key == "A" || edge.To.Key == "A");
        Assert.Contains(loadResult.Map.Edges, edge => edge.From.Key == "B" || edge.To.Key == "B");
    }

    [Fact]
    public void LoadFromFile_counts_edges_that_reference_missing_connectors()
    {
        var layoutPath = WriteLayoutJson("""
{
  "version": 3,
  "canvas": { "width": 1600, "height": 900 },
  "devices": [
    { "key": "A", "name": "A", "x": 100, "y": 100 }
  ],
  "connectors": [],
  "edges": [
    { "from": "A", "fromKind": "device", "to": "cp_missing", "toKind": "connector" }
  ]
}
""");

        var result = _service.LoadFromFile(layoutPath, [
            new ShortcutItem { Name = "设备A", TargetPath = "A" }
        ]);

        Assert.True(result.Success);
        Assert.Empty(result.Map.Edges);
        Assert.Equal(1, result.Map.InvalidEdgeCount);
    }

    [Fact]
    public void LoadFromFile_treats_null_edge_points_as_empty()
    {
        var layoutPath = WriteLayoutJson("""
{
  "version": 3,
  "canvas": { "width": 1600, "height": 900 },
  "devices": [
    { "key": "A", "name": "A", "x": 100, "y": 100 },
    { "key": "B", "name": "B", "x": 300, "y": 100 }
  ],
  "edges": [
    { "from": "A", "to": "B", "points": null }
  ]
}
""");

        var result = _service.LoadFromFile(layoutPath, [
            new ShortcutItem { Name = "设备A", TargetPath = "A" },
            new ShortcutItem { Name = "设备B", TargetPath = "B" }
        ]);

        Assert.True(result.Success);
        var edge = Assert.Single(result.Map.Edges);
        Assert.Empty(edge.Points);
    }

    [Fact]
    public void SaveToFile_filters_invalid_edge_points()
    {
        var layoutPath = Path.Combine(_rootPath, "filtered-edge-points-map.json");
        var nodeA = new FactoryMapDeviceViewNode { Key = "A", Name = "设备A", X = 100, Y = 120, Shortcut = new ShortcutItem { Name = "设备A", TargetPath = "A" } };
        var nodeB = new FactoryMapDeviceViewNode { Key = "B", Name = "设备B", X = 320, Y = 120, Shortcut = new ShortcutItem { Name = "设备B", TargetPath = "B" } };
        var map = new FactoryMapDeviceViewData
        {
            Devices = [nodeA, nodeB],
            Edges =
            [
                new FactoryMapDeviceEdgeViewData
                {
                    From = nodeA,
                    To = nodeB,
                    Points =
                    [
                        new FactoryMapPoint { X = 180, Y = 120 },
                        new FactoryMapPoint { X = double.NaN, Y = 160 },
                        new FactoryMapPoint { X = 220, Y = double.PositiveInfinity }
                    ]
                }
            ]
        };

        var result = _service.SaveToFile(layoutPath, map);

        Assert.True(result.Success);
        using var document = JsonDocument.Parse(File.ReadAllText(layoutPath));
        Assert.False(document.RootElement.TryGetProperty("edges", out _));
        Assert.All(document.RootElement.GetProperty("connectionPoints").EnumerateArray(), point =>
        {
            Assert.True(double.IsFinite(point.GetProperty("x").GetDouble()));
            Assert.True(double.IsFinite(point.GetProperty("y").GetDouble()));
        });
    }

    [Fact]
    public void Save_writes_device_positions_to_layout_json()
    {
        var layoutPath = Path.Combine(_rootPath, "factory-map.layout.json");
        var map = new FactoryMapDeviceViewData
        {
            Canvas = new FactoryMapCanvas { Width = 1600, Height = 900 },
            Devices =
            [
                new FactoryMapDeviceViewNode
                {
                    Key = "A",
                    Name = "设备A_001",
                    X = 123,
                    Y = 456,
                    Shortcut = new ShortcutItem { Name = "设备A_001", TargetPath = "A" }
                }
            ]
        };

        var result = _service.Save(layoutPath, map);

        Assert.True(result.Success);
        using var document = JsonDocument.Parse(File.ReadAllText(layoutPath));
        var device = document.RootElement.GetProperty("devices")[0];
        Assert.Equal("A", device.GetProperty("key").GetString());
        Assert.Equal(120, device.GetProperty("x").GetDouble());
        Assert.Equal(460, device.GetProperty("y").GetDouble());
    }

    [Fact]
    public void LoadOrCreate_returns_error_without_overwriting_damaged_json()
    {
        var layoutPath = WriteLayoutJson("{ broken json");
        var original = File.ReadAllText(layoutPath);

        var result = _service.LoadOrCreate(layoutPath, []);

        Assert.False(result.Success);
        Assert.Contains("读取失败", result.ErrorMessage);
        Assert.Equal(original, File.ReadAllText(layoutPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    private string WriteLayoutJson(string json)
    {
        var path = Path.Combine(_rootPath, "factory-map.layout.json");
        File.WriteAllText(path, json);
        return path;
    }
}
