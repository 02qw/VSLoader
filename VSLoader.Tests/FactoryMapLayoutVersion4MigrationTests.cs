using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapLayoutVersion4MigrationTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "VSLoader.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly FactoryMapLayoutService service = new();

    public FactoryMapLayoutVersion4MigrationTests()
    {
        Directory.CreateDirectory(rootPath);
    }

    [Fact]
    public void SaveToFile_writes_version6_topology_without_legacy_edge_fields()
    {
        var layoutPath = Path.Combine(rootPath, "factory-map.layout.json");
        var device = new FactoryMapDeviceViewNode
        {
            Id = "node-a",
            Key = "A",
            Name = "设备A",
            X = 100,
            Y = 100
        };
        var map = new FactoryMapDeviceViewData
        {
            Devices = [device],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint
                {
                    Id = "free-1",
                    Kind = FactoryMapConnectionPointKinds.Free,
                    X = 320,
                    Y = 130
                }
            ],
            Segments =
            [
                new FactoryMapSegment
                {
                    Id = "segment-1",
                    FromPointId = "node-a:right",
                    ToPointId = "free-1",
                    ZIndex = 2
                }
            ]
        };

        var result = service.SaveToFile(layoutPath, map);

        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(File.ReadAllText(layoutPath));
        var root = document.RootElement;
        Assert.Equal(6, root.GetProperty("version").GetInt32());
        Assert.Equal("node-a", root.GetProperty("devices")[0].GetProperty("id").GetString());
        Assert.Equal("free-1", root.GetProperty("connectionPoints")[0].GetProperty("id").GetString());
        Assert.Contains(
            root.GetProperty("segments").EnumerateArray(),
            segment => segment.GetProperty("fromPointId").GetString() == "node-a:right"
                || segment.GetProperty("toPointId").GetString() == "node-a:right");
        Assert.False(root.TryGetProperty("connectors", out _));
        Assert.False(root.TryGetProperty("edges", out _));

        var loaded = service.LoadFromFile(layoutPath, [new ShortcutItem { Name = "设备A", TargetPath = "A" }]);
        Assert.True(loaded.Success, loaded.ErrorMessage);
        Assert.Contains(loaded.Map.ConnectionPoints, point => point.Id == "node-a:right" && point.Kind == FactoryMapConnectionPointKinds.Attached);
        Assert.Contains(loaded.Map.ConnectionPoints, point => point.Id == "free-1" && point.Kind == FactoryMapConnectionPointKinds.Free);
        Assert.True(AreConnected(loaded.Map, "node-a:right", "free-1"));
    }

    [Fact]
    public void LoadFromFile_migrates_version3_edges_connectors_and_points_to_version4_topology()
    {
        var layoutPath = Path.Combine(rootPath, "factory-map.layout.json");
        File.WriteAllText(layoutPath, """
        {
          "version": 3,
          "canvas": { "width": 1600, "height": 900 },
          "devices": [
            { "key": "A", "name": "设备A", "x": 100, "y": 100 },
            { "key": "B", "name": "设备B", "x": 500, "y": 100 }
          ],
          "connectors": [
            { "id": "cp-1", "x": 360, "y": 130 }
          ],
          "edges": [
            {
              "from": "A", "fromKind": "device", "fromPort": "right",
              "to": "cp-1", "toKind": "connector", "toPort": "left",
              "points": [{ "x": 300, "y": 129 }]
            },
            {
              "from": "cp-1", "fromKind": "connector", "fromPort": "right",
              "to": "B", "toKind": "device", "toPort": "left",
              "points": []
            }
          ]
        }
        """);

        var result = service.LoadFromFile(layoutPath,
        [
            new ShortcutItem { Name = "设备A", TargetPath = "A" },
            new ShortcutItem { Name = "设备B", TargetPath = "B" }
        ]);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.All(result.Map.Devices, device => Assert.False(string.IsNullOrWhiteSpace(device.Id)));
        Assert.Equal(8, result.Map.ConnectionPoints.Count(point => point.Kind == FactoryMapConnectionPointKinds.Attached));
        Assert.Contains(result.Map.ConnectionPoints, point => point.Id == "cp-1" && point.Kind == FactoryMapConnectionPointKinds.Free);
        Assert.NotEmpty(result.Map.Segments);
        Assert.All(result.Map.Segments, segment =>
        {
            Assert.NotEqual(segment.FromPointId, segment.ToPointId);
            Assert.Contains(result.Map.ConnectionPoints, point => point.Id == segment.FromPointId);
            Assert.Contains(result.Map.ConnectionPoints, point => point.Id == segment.ToPointId);
        });

        Assert.Equal(3, JsonDocument.Parse(File.ReadAllText(layoutPath)).RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public void LoadFromFile_skips_version4_segments_with_missing_or_zero_length_endpoints()
    {
        var layoutPath = Path.Combine(rootPath, "factory-map.layout.json");
        File.WriteAllText(layoutPath, """
        {
          "version": 4,
          "canvas": { "width": 1600, "height": 900 },
          "devices": [{ "id": "node-a", "key": "A", "name": "设备A", "x": 100, "y": 100 }],
          "connectionPoints": [
            { "id": "free-1", "kind": "free", "x": 300, "y": 129 },
            { "id": "free-2", "kind": "free", "x": 300, "y": 129 }
          ],
          "segments": [
            { "id": "valid", "fromPointId": "node-a:right", "toPointId": "free-1" },
            { "id": "missing", "fromPointId": "free-1", "toPointId": "not-found" },
            { "id": "zero", "fromPointId": "free-1", "toPointId": "free-2" }
          ]
        }
        """);

        var result = service.LoadFromFile(layoutPath, [new ShortcutItem { Name = "设备A", TargetPath = "A" }]);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(AreConnected(result.Map, "node-a:right", "free-1"));
        Assert.DoesNotContain(result.Map.Segments, segment => segment.Id is "missing" or "zero");
        Assert.Equal(2, result.Map.InvalidSegmentCount);
    }

    [Fact]
    public void LoadFromFile_treats_missing_version_as_legacy_and_rejects_future_version()
    {
        var legacyPath = Path.Combine(rootPath, "legacy-without-version.json");
        File.WriteAllText(legacyPath, """
        {
          "devices": [
            { "key": "A", "name": "设备A", "x": 100, "y": 100 },
            { "key": "B", "name": "设备B", "x": 400, "y": 100 }
          ],
          "edges": [{ "from": "A", "to": "B" }]
        }
        """);

        var legacy = service.LoadFromFile(legacyPath,
        [
            new ShortcutItem { Name = "设备A", TargetPath = "A" },
            new ShortcutItem { Name = "设备B", TargetPath = "B" }
        ]);

        Assert.True(legacy.Success, legacy.ErrorMessage);
        Assert.NotEmpty(legacy.Map.Segments);

        var futurePath = Path.Combine(rootPath, "future.json");
        File.WriteAllText(futurePath, """
        { "version": 99, "devices": [], "connectionPoints": [], "segments": [] }
        """);

        var future = service.LoadFromFile(futurePath, []);

        Assert.False(future.Success);
        Assert.Contains("不支持", future.ErrorMessage);
    }

    [Fact]
    public void SaveToFile_preserves_segment_ids_and_zindex_on_second_version6_save()
    {
        var layoutPath = Path.Combine(rootPath, "stable-v5.json");
        File.WriteAllText(layoutPath, """
        {
          "version": 4,
          "devices": [{ "id": "node-a", "key": "A", "name": "设备A", "x": 100, "y": 100 }],
          "connectionPoints": [{ "id": "free-1", "kind": "free", "x": 400, "y": 129 }],
          "segments": [{ "id": "stable-segment", "fromPointId": "node-a:right", "toPointId": "free-1", "zIndex": 42 }]
        }
        """);
        var shortcuts = new[] { new ShortcutItem { Name = "设备A", TargetPath = "A" } };
        var loaded = service.LoadFromFile(layoutPath, shortcuts);

        var firstSaved = service.SaveToFile(layoutPath, loaded.Map);
        var firstReloaded = service.LoadFromFile(layoutPath, shortcuts);
        var firstSegments = firstReloaded.Map.Segments
            .OrderBy(segment => segment.Id, StringComparer.OrdinalIgnoreCase)
            .Select(segment => (segment.Id, segment.FromPointId, segment.ToPointId, segment.ZIndex))
            .ToArray();

        var secondSaved = service.SaveToFile(layoutPath, firstReloaded.Map);
        var secondReloaded = service.LoadFromFile(layoutPath, shortcuts);
        var secondSegments = secondReloaded.Map.Segments
            .OrderBy(segment => segment.Id, StringComparer.OrdinalIgnoreCase)
            .Select(segment => (segment.Id, segment.FromPointId, segment.ToPointId, segment.ZIndex))
            .ToArray();

        Assert.True(firstSaved.Success, firstSaved.ErrorMessage);
        Assert.True(secondSaved.Success, secondSaved.ErrorMessage);
        Assert.True(AreConnected(secondReloaded.Map, "node-a:right", "free-1"));
        Assert.Equal(firstSegments, secondSegments);
    }

    [Fact]
    public void SaveToFile_rejects_invalid_topology_without_overwriting_existing_file()
    {
        var layoutPath = Path.Combine(rootPath, "protected.json");
        File.WriteAllText(layoutPath, "original-content");
        var map = new FactoryMapDeviceViewData
        {
            TopologyAuthoritative = true,
            Devices = [new FactoryMapDeviceViewNode { Id = "node-a", Key = "A", Name = "设备A", X = 100, Y = 100 }],
            Segments =
            [
                new FactoryMapSegment
                {
                    Id = "invalid",
                    FromPointId = "node-a:right",
                    ToPointId = "missing"
                }
            ]
        };

        var result = service.SaveToFile(layoutPath, map);

        Assert.False(result.Success);
        Assert.Contains("不存在的连接点", result.ErrorMessage);
        Assert.Equal("original-content", File.ReadAllText(layoutPath));
    }

    private static bool AreConnected(FactoryMapDeviceViewData map, string startPointId, string endPointId)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in map.Segments)
        {
            AddNeighbor(adjacency, segment.FromPointId, segment.ToPointId);
            AddNeighbor(adjacency, segment.ToPointId, segment.FromPointId);
        }

        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue(startPointId);
        visited.Add(startPointId);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (string.Equals(current, endPointId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors.Where(visited.Add))
            {
                pending.Enqueue(neighbor);
            }
        }

        return false;
    }

    private static void AddNeighbor(Dictionary<string, List<string>> adjacency, string pointId, string neighborId)
    {
        if (!adjacency.TryGetValue(pointId, out var neighbors))
        {
            neighbors = [];
            adjacency[pointId] = neighbors;
        }

        neighbors.Add(neighborId);
    }

    [Fact]
    public void LoadFromFile_rejects_persisted_point_that_collides_with_reserved_attached_id()
    {
        var layoutPath = Path.Combine(rootPath, "attached-id-collision.json");
        File.WriteAllText(layoutPath, """
        {
          "version": 4,
          "devices": [{ "id": "node-a", "key": "A", "name": "设备A", "x": 100, "y": 100 }],
          "connectionPoints": [
            { "id": "node-a:right", "kind": "free", "x": 500, "y": 500 }
          ],
          "segments": []
        }
        """);
        var original = File.ReadAllText(layoutPath);

        var result = service.LoadFromFile(
            layoutPath,
            [new ShortcutItem { Name = "设备A", TargetPath = "A" }]);

        Assert.False(result.Success);
        Assert.Contains("连接点 ID", result.ErrorMessage);
        Assert.Equal(original, File.ReadAllText(layoutPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }
}
