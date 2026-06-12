using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class FactoryMapLayoutService
{
    private const int DefaultColumns = 6;
    private const int CurrentLayoutVersion = 3;
    private const double StartX = 80;
    private const double StartY = 80;
    private const double GapX = 210;
    private const double GapY = 90;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public FactoryMapLayoutLoadResult LoadOrCreate(string layoutPath, IEnumerable<ShortcutItem> shortcuts)
    {
        try
        {
            var shortcutList = shortcuts
                .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.TargetPath))
                .OrderBy(shortcut => shortcut.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(shortcut => shortcut.TargetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return File.Exists(layoutPath)
                ? LoadFromFile(layoutPath, shortcutList)
                : new FactoryMapLayoutLoadResult(BuildMap(new FactoryMapLayoutConfig(), shortcutList), true, null);
        }
        catch (Exception ex)
        {
            return new FactoryMapLayoutLoadResult(new FactoryMapDeviceViewData(), false, $"工厂地图布局读取失败：{ex.Message}");
        }
    }

    public FactoryMapLayoutSaveResult Save(string layoutPath, FactoryMapDeviceViewData map)
    {
        return SaveToFile(layoutPath, map);
    }

    public FactoryMapLayoutLoadResult LoadFromFile(string layoutPath, IEnumerable<ShortcutItem> shortcuts)
    {
        try
        {
            var shortcutList = shortcuts
                .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.TargetPath))
                .OrderBy(shortcut => shortcut.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(shortcut => shortcut.TargetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var config = LoadConfig(layoutPath);
            var map = BuildMap(config, shortcutList);
            var importStats = CalculateImportStats(config, map);
            return new FactoryMapLayoutLoadResult(
                map,
                true,
                null,
                importStats.AppliedDeviceCount,
                importStats.SkippedDeviceCount,
                importStats.KeptEdgeCount,
                importStats.SkippedEdgeCount);
        }
        catch (Exception ex)
        {
            return new FactoryMapLayoutLoadResult(new FactoryMapDeviceViewData(), false, $"工厂地图布局读取失败：{ex.Message}");
        }
    }

    public FactoryMapLayoutSaveResult SaveToFile(string layoutPath, FactoryMapDeviceViewData map)
    {
        try
        {
            var directory = Path.GetDirectoryName(layoutPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var config = new FactoryMapLayoutConfig
            {
                Version = CurrentLayoutVersion,
                Canvas = map.Canvas ?? new FactoryMapCanvas { Width = 1600, Height = 900 },
                Devices = map.Devices
                    .Where(device => !string.IsNullOrWhiteSpace(device.Key))
                    .Select(device => new FactoryMapDeviceNode
                    {
                        Key = device.Key,
                        Name = device.Name,
                        X = device.X,
                        Y = device.Y
                    })
                    .ToList(),
                Edges = map.Edges
                    .Where(edge => !string.IsNullOrWhiteSpace(edge.From.Key) && !string.IsNullOrWhiteSpace(edge.To.Key))
                    .Select(edge => new FactoryMapDeviceEdge
                    {
                        From = edge.From.Key,
                        To = edge.To.Key
                    })
                    .ToList()
            };

            var json = JsonSerializer.Serialize(config, jsonOptions);
            File.WriteAllText(layoutPath, json);
            return new FactoryMapLayoutSaveResult(true, null);
        }
        catch (Exception ex)
        {
            return new FactoryMapLayoutSaveResult(false, $"工厂地图布局保存失败：{ex.Message}");
        }
    }

    private FactoryMapLayoutConfig LoadConfig(string layoutPath)
    {
        var json = File.ReadAllText(layoutPath);
        var config = JsonSerializer.Deserialize<FactoryMapLayoutConfig>(json, jsonOptions);
        if (config is null)
        {
            throw new InvalidOperationException("工厂地图布局配置为空或格式无效。");
        }

        config.Canvas ??= new FactoryMapCanvas { Width = 1600, Height = 900 };
        config.Devices ??= [];
        config.Edges ??= [];
        return config;
    }

    private static FactoryMapDeviceViewData BuildMap(FactoryMapLayoutConfig config, IReadOnlyList<ShortcutItem> shortcuts)
    {
        var savedDevices = config.Devices
            .Where(device => !string.IsNullOrWhiteSpace(device.Key))
            .GroupBy(device => NormalizeKey(device.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var devices = new List<FactoryMapDeviceViewNode>();

        for (var index = 0; index < shortcuts.Count; index++)
        {
            var shortcut = shortcuts[index];
            var key = shortcut.TargetPath.Trim();
            var normalizedKey = NormalizeKey(key);
            var defaultPosition = GetDefaultPosition(index);

            if (savedDevices.TryGetValue(normalizedKey, out var savedDevice))
            {
                devices.Add(new FactoryMapDeviceViewNode
                {
                    Key = key,
                    Name = shortcut.Name,
                    X = savedDevice.X,
                    Y = savedDevice.Y,
                    Shortcut = shortcut
                });
                continue;
            }

            devices.Add(new FactoryMapDeviceViewNode
            {
                Key = key,
                Name = shortcut.Name,
                X = defaultPosition.X,
                Y = defaultPosition.Y,
                Shortcut = shortcut
            });
        }

        var devicesByKey = devices.ToDictionary(device => NormalizeKey(device.Key), StringComparer.OrdinalIgnoreCase);
        var edges = new List<FactoryMapDeviceEdgeViewData>();
        var invalidEdgeCount = 0;
        var edgeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in config.Edges)
        {
            var fromKey = NormalizeKey(edge.From);
            var toKey = NormalizeKey(edge.To);
            var edgeKey = $"{fromKey}\u001F{toKey}";
            if (string.IsNullOrWhiteSpace(fromKey)
                || string.IsNullOrWhiteSpace(toKey)
                || string.Equals(fromKey, toKey, StringComparison.OrdinalIgnoreCase)
                || !edgeKeys.Add(edgeKey)
                || !devicesByKey.TryGetValue(fromKey, out var from)
                || !devicesByKey.TryGetValue(toKey, out var to))
            {
                invalidEdgeCount++;
                continue;
            }

            edges.Add(new FactoryMapDeviceEdgeViewData
            {
                From = from,
                To = to
            });
        }

        return new FactoryMapDeviceViewData
        {
            Canvas = config.Canvas ?? new FactoryMapCanvas { Width = 1600, Height = 900 },
            Devices = devices,
            Edges = edges,
            InvalidEdgeCount = invalidEdgeCount
        };
    }

    private static (double X, double Y) GetDefaultPosition(int index)
    {
        var column = index % DefaultColumns;
        var row = index / DefaultColumns;
        return (StartX + column * GapX, StartY + row * GapY);
    }

    private static string NormalizeKey(string value)
    {
        return value.Trim();
    }

    private static (int AppliedDeviceCount, int SkippedDeviceCount, int KeptEdgeCount, int SkippedEdgeCount) CalculateImportStats(
        FactoryMapLayoutConfig config,
        FactoryMapDeviceViewData map)
    {
        var currentKeys = map.Devices
            .Select(device => NormalizeKey(device.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var importedKeys = config.Devices
            .Where(device => !string.IsNullOrWhiteSpace(device.Key))
            .Select(device => NormalizeKey(device.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var appliedDeviceCount = importedKeys.Count(currentKeys.Contains);
        var skippedDeviceCount = importedKeys.Count - appliedDeviceCount;
        return (appliedDeviceCount, skippedDeviceCount, map.Edges.Count, map.InvalidEdgeCount);
    }
}
