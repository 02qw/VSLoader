using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class FactoryMapServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FactoryMapService _service = new();

    public FactoryMapServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void Load_reads_nodes_and_edges_from_json()
    {
        var jsonPath = WriteMapJson();

        var result = _service.Load(jsonPath);

        Assert.True(result.Success);
        Assert.Equal(3, result.Config.Nodes.Count);
        Assert.Equal(2, result.Config.Edges.Count);
        Assert.Equal("印刷机", result.Config.Nodes[0].Key);
        Assert.Equal(1200, result.Config.Canvas.Width);
    }

    [Fact]
    public void BuildMap_matches_shortcuts_to_configured_type_nodes()
    {
        var jsonPath = WriteMapJson();
        var config = _service.Load(jsonPath).Config;
        var shortcuts = new[]
        {
            new ShortcutItem { Name = "矩子3D-AOI_007", TargetPath = @"C:\A" },
            new ShortcutItem { Name = "矩子3D-AOI_008", TargetPath = @"C:\B" },
            new ShortcutItem { Name = "InvalidName", TargetPath = @"C:\C" }
        };

        var map = _service.BuildMap(config, shortcuts);

        var aoiNode = Assert.Single(map.Nodes, node => node.Key == "矩子3D-AOI");
        Assert.Equal(2, aoiNode.Machines.Count);
        Assert.Equal("007", aoiNode.Machines[0].No);
        Assert.Equal(1, map.UnmatchedShortcutCount);
    }

    [Fact]
    public void BuildMap_ignores_edges_that_reference_missing_nodes()
    {
        var config = new FactoryMapConfig
        {
            Nodes =
            [
                new FactoryMapNode { Key = "印刷机", Label = "印刷机", X = 80, Y = 120 }
            ],
            Edges =
            [
                new FactoryMapEdge { From = "印刷机", To = "不存在" }
            ]
        };

        var map = _service.BuildMap(config, []);

        Assert.Empty(map.Edges);
        Assert.Equal(1, map.InvalidEdgeCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    private string WriteMapJson()
    {
        var path = Path.Combine(_rootPath, "factory-map.json");
        File.WriteAllText(path, """
{
  "canvas": { "width": 1200, "height": 720 },
  "nodes": [
    { "key": "印刷机", "label": "印刷机", "x": 80, "y": 120 },
    { "key": "SPI", "label": "SPI", "x": 260, "y": 120 },
    { "key": "矩子3D-AOI", "label": "矩子3D-AOI", "x": 440, "y": 80 }
  ],
  "edges": [
    { "from": "印刷机", "to": "SPI" },
    { "from": "SPI", "to": "矩子3D-AOI", "points": [ { "x": 360, "y": 120 }, { "x": 360, "y": 80 } ] }
  ]
}
""");
        return path;
    }
}
