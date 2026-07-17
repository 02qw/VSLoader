using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class FactoryMapLayoutService
{
    private const int DefaultColumns = 6;
    internal const int CurrentLayoutVersion = 6;
    private const double LegacyDeviceWidth = 150;
    private const double LegacyDeviceHeight = 58;
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

            EnsureDeviceIds(map.Devices);
            var geometryChanged = FactoryMapNodeGeometryService.NormalizeDevices(map.Devices);

            if (map.TopologyAuthoritative)
            {
                var attachedBefore = map.ConnectionPoints
                    .Where(point => point.Kind == FactoryMapConnectionPointKinds.Attached)
                    .ToDictionary(point => point.Id, point => (point.X, point.Y), StringComparer.OrdinalIgnoreCase);
                FactoryMapNodeGeometryService.SynchronizeAttachedPoints(map);
                geometryChanged |= map.ConnectionPoints
                    .Where(point => point.Kind == FactoryMapConnectionPointKinds.Attached)
                    .Any(point => !attachedBefore.TryGetValue(point.Id, out var before)
                        || before != (point.X, point.Y));
                if (geometryChanged && map.Segments.Count > 0)
                {
                    var arrangement = new FactoryMapLineArrangementService().ArrangeAll(
                        map,
                        FactoryMapNodeGeometryService.GridSize);
                    if (!arrangement.Success)
                    {
                        return new FactoryMapLayoutSaveResult(
                            false,
                            $"节点几何变化后线路整理失败：{arrangement.ErrorMessage}");
                    }
                }
            }
            var topology = !map.TopologyAuthoritative && (map.Edges.Count > 0 || map.Connectors.Count > 0)
                ? FactoryMapLayoutTopologyConverter.BuildFromLegacy(map.Devices, map.Connectors, map.Edges)
                : new FactoryMapLayoutTopologyConverter.ConversionResult(
                    BuildCompletePointSet(map),
                    map.Segments.Select(CloneSegment).ToList(),
                    map.InvalidSegmentCount);
            var topologyValidation = new FactoryMapTopologyService().ValidateTopology(new FactoryMapDeviceViewData
            {
                Devices = map.Devices,
                ConnectionPoints = topology.Points,
                Segments = topology.Segments
            });
            if (!topologyValidation.IsValid)
            {
                return new FactoryMapLayoutSaveResult(
                    false,
                    $"工厂地图拓扑无效，未保存：{string.Join("；", topologyValidation.Errors)}");
            }
            var config = new FactoryMapLayoutConfig
            {
                Version = CurrentLayoutVersion,
                Canvas = map.Canvas ?? new FactoryMapCanvas { Width = 1600, Height = 900 },
                Devices = map.Devices
                    .Where(device => !string.IsNullOrWhiteSpace(device.Key))
                    .Select(device => new FactoryMapDeviceNode
                    {
                        Id = device.Id,
                        Key = device.Key,
                        Name = device.Name,
                        X = device.X,
                        Y = device.Y,
                        Width = device.Width,
                        Height = device.Height
                    })
                    .ToList(),
                ConnectionPoints = topology.Points
                    .Where(point => point.Kind != FactoryMapConnectionPointKinds.Attached)
                    .Select(CloneConnectionPoint)
                    .ToList(),
                Segments = topology.Segments.Select(CloneSegment).ToList(),
                Connectors = null,
                Edges = null
            };

            var json = JsonSerializer.Serialize(config, jsonOptions);
            BackupVersion3LayoutIfNeeded(layoutPath);
            WriteAtomically(layoutPath, json);
            map.RequiresPersistence = false;
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

        if (config.Version > CurrentLayoutVersion)
        {
            throw new NotSupportedException($"不支持的工厂地图布局版本：{config.Version}。");
        }

        config.Canvas ??= new FactoryMapCanvas { Width = 1600, Height = 900 };
        config.Devices ??= [];
        config.ConnectionPoints ??= [];
        config.Segments ??= [];
        config.Connectors ??= [];
        config.Edges ??= [];
        foreach (var edge in config.Edges)
        {
            edge.FromKind = NormalizeEndpointKind(edge.FromKind);
            edge.ToKind = NormalizeEndpointKind(edge.ToKind);
            edge.Points ??= [];
        }

        return config;
    }

    private static FactoryMapDeviceViewData BuildMap(FactoryMapLayoutConfig config, IReadOnlyList<ShortcutItem> shortcuts)
    {
        var savedDevices = config.Devices
            .Where(device => !string.IsNullOrWhiteSpace(device.Key))
            .GroupBy(device => NormalizeKey(device.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var devices = new List<FactoryMapDeviceViewNode>();
        var usedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var geometryChanged = false;

        for (var index = 0; index < shortcuts.Count; index++)
        {
            var shortcut = shortcuts[index];
            var key = shortcut.TargetPath.Trim();
            var normalizedKey = NormalizeKey(key);
            var defaultPosition = GetDefaultPosition(index);

            if (savedDevices.TryGetValue(normalizedKey, out var savedDevice))
            {
                var nodeId = GetUniqueNodeId(savedDevice.Id, key, usedNodeIds);
                var device = new FactoryMapDeviceViewNode
                {
                    Id = nodeId,
                    Key = key,
                    Name = shortcut.Name,
                    X = savedDevice.X,
                    Y = savedDevice.Y,
                    Width = config.Version < 6 ? LegacyDeviceWidth : savedDevice.Width,
                    Height = config.Version < 6 ? LegacyDeviceHeight : savedDevice.Height,
                    Shortcut = shortcut
                };
                devices.Add(device);
                continue;
            }

            var newDevice = new FactoryMapDeviceViewNode
            {
                Id = GetUniqueNodeId(string.Empty, key, usedNodeIds),
                Key = key,
                Name = shortcut.Name,
                X = defaultPosition.X,
                Y = defaultPosition.Y,
                Shortcut = shortcut
            };
            devices.Add(newDevice);
        }

        if (config.Version >= 6)
        {
            geometryChanged |= FactoryMapNodeGeometryService.NormalizeDevices(devices);
        }

        var devicesByKey = devices.ToDictionary(device => NormalizeKey(device.Key), StringComparer.OrdinalIgnoreCase);
        var connectors = (config.Connectors ?? [])
            .Where(connector => !string.IsNullOrWhiteSpace(connector.Id)
                && double.IsFinite(connector.X)
                && double.IsFinite(connector.Y))
            .GroupBy(connector => NormalizeKey(connector.Id), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(connector => new FactoryMapConnectorViewNode
            {
                Id = NormalizeKey(connector.Id),
                X = Math.Max(0, connector.X),
                Y = Math.Max(0, connector.Y)
            })
            .ToList();
        var connectorsById = connectors.ToDictionary(connector => NormalizeKey(connector.Id), StringComparer.OrdinalIgnoreCase);
        var edges = new List<FactoryMapDeviceEdgeViewData>();
        var invalidEdgeCount = 0;
        var edgeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in config.Edges ?? [])
        {
            var fromId = NormalizeKey(edge.From);
            var toId = NormalizeKey(edge.To);
            var fromKind = NormalizeEndpointKind(edge.FromKind);
            var toKind = NormalizeEndpointKind(edge.ToKind);
            if (string.IsNullOrWhiteSpace(fromId)
                || string.IsNullOrWhiteSpace(toId)
                || (string.Equals(fromKind, toKind, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(fromId, toId, StringComparison.OrdinalIgnoreCase))
                || !TryResolveEndpoint(fromKind, fromId, devicesByKey, connectorsById, out var from)
                || !TryResolveEndpoint(toKind, toId, devicesByKey, connectorsById, out var to))
            {
                invalidEdgeCount++;
                continue;
            }

            var fromPort = NormalizeLoadedPort(edge.FromPort, FactoryMapEndpointGeometryService.InferOutgoingPort(from, to));
            var toPort = NormalizeLoadedPort(edge.ToPort, FactoryMapEndpointGeometryService.InferIncomingPort(from, to));
            var edgeKey = $"{fromKind}\u001F{fromId}\u001F{fromPort}\u001F{toKind}\u001F{toId}\u001F{toPort}";
            if (!edgeKeys.Add(edgeKey))
            {
                invalidEdgeCount++;
                continue;
            }

            edges.Add(new FactoryMapDeviceEdgeViewData
            {
                From = from,
                To = to,
                FromPort = fromPort,
                ToPort = toPort,
                Points = NormalizeEdgePoints(edge.Points)
            });
        }

        var topology = config.Version >= 4
            ? FactoryMapLayoutTopologyConverter.BuildFromVersion4(config, devices, geometryChanged)
            : FactoryMapLayoutTopologyConverter.BuildFromLegacy(devices, connectors, edges);
        if (config.Version >= 4)
        {
            var projection = FactoryMapLayoutTopologyConverter.BuildLegacyProjection(
                devices,
                topology.Points,
                topology.Segments);
            connectors = projection.Connectors;
            edges = projection.Edges;
        }

        var map = new FactoryMapDeviceViewData
        {
            TopologyAuthoritative = true,
            Canvas = config.Canvas ?? new FactoryMapCanvas { Width = 1600, Height = 900 },
            Devices = devices,
            ConnectionPoints = topology.Points,
            Segments = topology.Segments,
            Connectors = connectors,
            Edges = edges,
            InvalidEdgeCount = invalidEdgeCount + topology.InvalidSegmentCount,
            InvalidSegmentCount = topology.InvalidSegmentCount,
            RequiresPersistence = geometryChanged || config.Version < CurrentLayoutVersion
        };

        if (config.Version < 6)
        {
            geometryChanged |= FactoryMapNodeGeometryService.NormalizeDevices(map.Devices);
        }

        if (geometryChanged)
        {
            FactoryMapNodeGeometryService.SynchronizeAttachedPoints(map);
            if (map.Segments.Count > 0)
            {
                var arrangement = new FactoryMapLineArrangementService().ArrangeAll(map, FactoryMapNodeGeometryService.GridSize);
                if (!arrangement.Success)
                {
                    throw new InvalidDataException($"节点尺寸迁移后线路整理失败：{arrangement.ErrorMessage}");
                }
            }

            map.RequiresPersistence = true;
        }

        return map;
    }

    private static (double X, double Y) GetDefaultPosition(int index)
    {
        var column = index % DefaultColumns;
        var row = index / DefaultColumns;
        return (StartX + column * GapX, StartY + row * GapY);
    }

    private static string NormalizeKey(string value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string NormalizeEndpointKind(string? value)
    {
        return string.Equals(value, FactoryMapEndpointKinds.Connector, StringComparison.OrdinalIgnoreCase)
            ? FactoryMapEndpointKinds.Connector
            : FactoryMapEndpointKinds.Device;
    }

    private static string NormalizeLoadedPort(string? value, string inferredFallback)
    {
        return FactoryMapEndpointGeometryService.IsKnownPort(value)
            ? FactoryMapEndpointGeometryService.NormalizePort(value)
            : FactoryMapEndpointGeometryService.NormalizePort(inferredFallback);
    }

    private static bool TryResolveEndpoint(
        string kind,
        string id,
        IReadOnlyDictionary<string, FactoryMapDeviceViewNode> devicesByKey,
        IReadOnlyDictionary<string, FactoryMapConnectorViewNode> connectorsById,
        out FactoryMapEndpointViewData endpoint)
    {
        if (string.Equals(kind, FactoryMapEndpointKinds.Connector, StringComparison.OrdinalIgnoreCase))
        {
            if (connectorsById.TryGetValue(id, out var connector))
            {
                endpoint = FactoryMapEndpointViewData.FromConnector(connector);
                return true;
            }

            endpoint = new FactoryMapEndpointViewData();
            return false;
        }

        if (devicesByKey.TryGetValue(id, out var device))
        {
            endpoint = FactoryMapEndpointViewData.FromDevice(device);
            return true;
        }

        endpoint = new FactoryMapEndpointViewData();
        return false;
    }

    private static List<FactoryMapPoint> NormalizeEdgePoints(IEnumerable<FactoryMapPoint>? points)
    {
        if (points is null)
        {
            return [];
        }

        return points
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .Select(point => new FactoryMapPoint { X = point.X, Y = point.Y })
            .ToList();
    }

    private static void EnsureDeviceIds(IReadOnlyList<FactoryMapDeviceViewNode> devices)
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            device.Id = GetUniqueNodeId(device.Id, device.Key, usedIds);
        }
    }

    private static string GetUniqueNodeId(string? requestedId, string key, ISet<string> usedIds)
    {
        var baseId = string.IsNullOrWhiteSpace(requestedId)
            ? FactoryMapLayoutTopologyConverter.CreateStableNodeId(key)
            : requestedId.Trim();
        var id = baseId;
        var suffix = 2;
        while (!usedIds.Add(id))
        {
            id = $"{baseId}-{suffix++}";
        }

        return id;
    }

    private static FactoryMapConnectionPoint CloneConnectionPoint(FactoryMapConnectionPoint point)
    {
        return new FactoryMapConnectionPoint
        {
            Id = point.Id?.Trim() ?? string.Empty,
            Kind = FactoryMapConnectionPointKinds.Normalize(point.Kind),
            OwnerNodeId = point.OwnerNodeId?.Trim() ?? string.Empty,
            Side = point.Side?.Trim() ?? string.Empty,
            JunctionAxis = point.Kind == FactoryMapConnectionPointKinds.Junction
                ? FactoryMapJunctionAxes.Normalize(point.JunctionAxis)
                : string.Empty,
            X = Math.Max(0, point.X),
            Y = Math.Max(0, point.Y)
        };
    }

    private static List<FactoryMapConnectionPoint> BuildCompletePointSet(FactoryMapDeviceViewData map)
    {
        var points = map.ConnectionPoints
            .Where(point => point.Kind != FactoryMapConnectionPointKinds.Attached)
            .Select(CloneConnectionPoint)
            .ToList();
        points.AddRange(FactoryMapLayoutTopologyConverter.CreateAttachedPoints(map.Devices));
        return points
            .GroupBy(point => point.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static FactoryMapSegment CloneSegment(FactoryMapSegment segment)
    {
        return new FactoryMapSegment
        {
            Id = segment.Id?.Trim() ?? string.Empty,
            FromPointId = segment.FromPointId?.Trim() ?? string.Empty,
            ToPointId = segment.ToPointId?.Trim() ?? string.Empty,
            ZIndex = segment.ZIndex
        };
    }

    private static void BackupVersion3LayoutIfNeeded(string layoutPath)
    {
        if (!File.Exists(layoutPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(layoutPath));
            var versionProperty = document.RootElement
                .EnumerateObject()
                .FirstOrDefault(property => string.Equals(property.Name, "version", StringComparison.OrdinalIgnoreCase));
            if (versionProperty.Value.ValueKind != JsonValueKind.Number
                || !versionProperty.Value.TryGetInt32(out var version)
                || version >= CurrentLayoutVersion)
            {
                return;
            }

            var directory = Path.GetDirectoryName(layoutPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(layoutPath);
            var backupPath = Path.Combine(
                directory,
                $"{fileName}.v{version}-backup.{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
            File.Copy(layoutPath, backupPath, false);
        }
        catch (JsonException)
        {
            // A malformed existing file is left untouched if the following write fails.
        }
    }

    private static void WriteAtomically(string layoutPath, string json)
    {
        var temporaryPath = $"{layoutPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, layoutPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
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
