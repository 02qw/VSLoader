using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapLayoutVersion5MigrationTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "VSLoader.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly FactoryMapLayoutService service = new();

    public FactoryMapLayoutVersion5MigrationTests()
    {
        Directory.CreateDirectory(rootPath);
    }

    [Fact]
    public void Save_writes_version6_and_preserves_junction_axis()
    {
        var layoutPath = Path.Combine(rootPath, "junction-v5.json");
        var map = CreateMap();
        map.ConnectionPoints.Add(new FactoryMapConnectionPoint
        {
            Id = "junction-1",
            Kind = FactoryMapConnectionPointKinds.Junction,
            JunctionAxis = FactoryMapJunctionAxes.Horizontal,
            X = 350,
            Y = 130
        });
        map.Segments.AddRange(
        [
            new FactoryMapSegment { Id = "left", FromPointId = "node-a:right", ToPointId = "junction-1" },
            new FactoryMapSegment { Id = "right", FromPointId = "junction-1", ToPointId = "free-1" }
        ]);

        var saved = service.SaveToFile(layoutPath, map);

        Assert.True(saved.Success, saved.ErrorMessage);
        using var document = JsonDocument.Parse(File.ReadAllText(layoutPath));
        Assert.Equal(6, document.RootElement.GetProperty("version").GetInt32());
        var junction = document.RootElement.GetProperty("connectionPoints")
            .EnumerateArray()
            .Single(point => point.GetProperty("id").GetString() == "junction-1");
        Assert.Equal("junction", junction.GetProperty("kind").GetString());
        Assert.Equal("horizontal", junction.GetProperty("junctionAxis").GetString());
    }

    [Fact]
    public void Load_version4_converts_inline_free_point_to_horizontal_junction()
    {
        var layoutPath = Path.Combine(rootPath, "inline-v4.json");
        File.WriteAllText(layoutPath, """
        {
          "version": 4,
          "devices": [{ "id": "node-a", "key": "A", "name": "设备A", "x": 100, "y": 100 }],
          "connectionPoints": [
            { "id": "inline", "kind": "free", "x": 350, "y": 129 },
            { "id": "free-1", "kind": "free", "x": 500, "y": 129 }
          ],
          "segments": [
            { "id": "left", "fromPointId": "node-a:right", "toPointId": "inline" },
            { "id": "right", "fromPointId": "inline", "toPointId": "free-1" }
          ]
        }
        """);

        var loaded = service.LoadFromFile(
            layoutPath,
            [new ShortcutItem { Name = "设备A", TargetPath = "A" }]);

        Assert.True(loaded.Success, loaded.ErrorMessage);
        var junction = loaded.Map.ConnectionPoints.Single(point => point.Id == "inline");
        Assert.Equal(FactoryMapConnectionPointKinds.Junction, junction.Kind);
        Assert.Equal(FactoryMapJunctionAxes.Horizontal, junction.JunctionAxis);
    }

    [Fact]
    public void Load_version4_keeps_ambiguous_unconnected_free_point()
    {
        var layoutPath = Path.Combine(rootPath, "free-v4.json");
        File.WriteAllText(layoutPath, """
        {
          "version": 4,
          "devices": [{ "id": "node-a", "key": "A", "name": "设备A", "x": 100, "y": 100 }],
          "connectionPoints": [{ "id": "free-1", "kind": "free", "x": 400, "y": 220 }],
          "segments": []
        }
        """);

        var loaded = service.LoadFromFile(
            layoutPath,
            [new ShortcutItem { Name = "设备A", TargetPath = "A" }]);

        Assert.True(loaded.Success, loaded.ErrorMessage);
        var point = loaded.Map.ConnectionPoints.Single(candidate => candidate.Id == "free-1");
        Assert.Equal(FactoryMapConnectionPointKinds.Free, point.Kind);
        Assert.Equal(string.Empty, point.JunctionAxis);
    }

    private static FactoryMapDeviceViewData CreateMap()
    {
        return new FactoryMapDeviceViewData
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
                    Y = 100
                }
            ],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint
                {
                    Id = "free-1",
                    Kind = FactoryMapConnectionPointKinds.Free,
                    X = 500,
                    Y = 130
                }
            ]
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }
}
