using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class FactoryMapService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FactoryMapLoadResult Load(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                return new FactoryMapLoadResult(new FactoryMapConfig(), false, $"未找到工厂地图配置文件：{configPath}");
            }

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<FactoryMapConfig>(json, _jsonOptions);
            if (config is null)
            {
                return new FactoryMapLoadResult(new FactoryMapConfig(), false, "工厂地图配置为空或格式无效。");
            }

            config.Canvas ??= new FactoryMapCanvas();
            config.Nodes ??= [];
            config.Edges ??= [];
            return new FactoryMapLoadResult(config, true, null);
        }
        catch (Exception ex)
        {
            return new FactoryMapLoadResult(new FactoryMapConfig(), false, $"工厂地图配置读取失败：{ex.Message}");
        }
    }

    public FactoryMapViewData BuildMap(FactoryMapConfig config, IEnumerable<ShortcutItem> shortcuts)
    {
        var nodes = config.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Key))
            .Select(node => new FactoryMapNodeViewData
            {
                Key = node.Key.Trim(),
                Label = string.IsNullOrWhiteSpace(node.Label) ? node.Key.Trim() : node.Label.Trim(),
                X = node.X,
                Y = node.Y
            })
            .ToList();
        var nodesByKey = nodes.ToDictionary(node => node.Key, StringComparer.OrdinalIgnoreCase);
        var unmatchedCount = 0;

        foreach (var shortcut in shortcuts)
        {
            if (!TryParseShortcutName(shortcut.Name, out var typeName, out var no)
                || !nodesByKey.TryGetValue(typeName, out var node))
            {
                unmatchedCount++;
                continue;
            }

            node.Machines.Add(new FactoryMapMachineNode
            {
                Name = shortcut.Name,
                No = no,
                Shortcut = shortcut
            });
        }

        foreach (var node in nodes)
        {
            node.Machines = node.Machines
                .OrderBy(machine => int.TryParse(machine.No, out var no) ? no : int.MaxValue)
                .ThenBy(machine => machine.No, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var edges = new List<FactoryMapEdgeViewData>();
        var invalidEdgeCount = 0;
        foreach (var edge in config.Edges)
        {
            if (!nodesByKey.TryGetValue(edge.From, out var fromNode)
                || !nodesByKey.TryGetValue(edge.To, out var toNode))
            {
                invalidEdgeCount++;
                continue;
            }

            edges.Add(new FactoryMapEdgeViewData
            {
                From = fromNode,
                To = toNode,
                Points = edge.Points ?? []
            });
        }

        return new FactoryMapViewData
        {
            Canvas = config.Canvas ?? new FactoryMapCanvas(),
            Nodes = nodes,
            Edges = edges,
            UnmatchedShortcutCount = unmatchedCount,
            InvalidEdgeCount = invalidEdgeCount
        };
    }

    private static bool TryParseShortcutName(string shortcutName, out string typeName, out string no)
    {
        typeName = string.Empty;
        no = string.Empty;
        var match = Regex.Match(shortcutName.Trim(), @"^(?<Type>.+)_(?<No>\d+)$");
        if (!match.Success)
        {
            return false;
        }

        typeName = match.Groups["Type"].Value.Trim();
        no = match.Groups["No"].Value.Trim();
        return !string.IsNullOrWhiteSpace(typeName) && !string.IsNullOrWhiteSpace(no);
    }
}
