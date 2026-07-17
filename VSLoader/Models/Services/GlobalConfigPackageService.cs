using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class GlobalConfigPackageService
{
    private const int CurrentSchemaVersion = 2;
    private const int LegacySchemaVersion = 1;
    private const int CurrentFactoryMapLayoutVersion = FactoryMapLayoutService.CurrentLayoutVersion;
    private const string ExpectedAppName = "VSLoader";
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public GlobalConfigExportResult Export(
        string packagePath,
        AppConfig workspaceConfig,
        AppSettings appSettings,
        string factoryMapLayoutPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return GlobalConfigExportResult.Fail("导出路径为空。");
            }

            var result = GlobalConfigExportResult.Ok();
            var workspaceSettings = CloneWorkspaceSettings(workspaceConfig);
            var package = new GlobalConfigPackage
            {
                SchemaVersion = CurrentSchemaVersion,
                AppName = ExpectedAppName,
                ExportedAt = DateTimeOffset.Now.ToString("O"),
                Workspace = new GlobalConfigWorkspaceSection
                {
                    Source = LoadWorkspaceSource(factoryMapLayoutPath, result.Warnings),
                    Settings = workspaceSettings,
                    FactoryMapLayout = LoadFactoryMapLayout(factoryMapLayoutPath, result.Warnings),
                    InterfacePreferences = LoadInterfacePreferences(
                        factoryMapLayoutPath,
                        appSettings.SettingsPageOrder,
                        result.Warnings)
                },
                MachineSettings = new GlobalProgramSettings
                {
                    VSCodePath = appSettings.VSCodePath ?? string.Empty,
                    SoftwareUpdateManifestPath = appSettings.SoftwareUpdateManifestPath ?? string.Empty
                }
            };

            if (!string.IsNullOrWhiteSpace(workspaceSettings.AdminUi.ProtectedPassword))
            {
                result.Warnings.Add("配置包包含 AdminUI 明文密码，请仅保存到可信位置。");
            }

            var directory = Path.GetDirectoryName(packagePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(packagePath, JsonSerializer.Serialize(package, jsonOptions));
            return result;
        }
        catch (Exception ex)
        {
            return GlobalConfigExportResult.Fail($"全局配置导出失败：{ex.Message}");
        }
    }

    public GlobalConfigImportResult Import(
        string packagePath,
        string currentConfigPath,
        string currentFactoryMapLayoutPath,
        AppSettings appSettings,
        Func<AppSettings, string?> resolveVSCodePath,
        IProgress<GlobalConfigOperationProgress>? progress = null)
    {
        ReportProgress(progress, 10, "正在读取全局配置包...", Path.GetFileName(packagePath));
        GlobalConfigPackage package;
        try
        {
            package = ReadPackage(packagePath);
        }
        catch (Exception ex)
        {
            return GlobalConfigImportResult.Fail($"配置包读取失败：{ex.Message}");
        }

        var validationError = ValidatePackage(package);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return GlobalConfigImportResult.Fail(validationError);
        }

        ReportProgress(progress, 30, "正在校验工作区配置...", "快捷项、右键菜单能力和路径");
        var resolved = ResolvePackage(package);
        var result = GlobalConfigImportResult.Ok();
        var capabilityWarnings = NormalizeWorkspaceConfig(resolved.WorkspaceConfig);
        result.Warnings.AddRange(capabilityWarnings);
        var importedPowerShellCount = resolved.WorkspaceConfig.ContextMenuCapabilities.Items.Count(item =>
            string.Equals(item.Kind, ContextMenuCapabilityKinds.PowerShell, StringComparison.Ordinal));
        if (importedPowerShellCount > 0)
        {
            result.Warnings.Add($"配置包包含 {importedPowerShellCount} 个 PowerShell 能力，首次执行时需要本机确认信任。");
        }
        AddPreflightWarnings(resolved.WorkspaceConfig, result);

        try
        {
            ReportProgress(progress, 50, "正在写入工作区配置...", "config.json");
            BackupIfExists(currentConfigPath, "config.import-backup");
            WriteJson(currentConfigPath, resolved.WorkspaceConfig);
            result.ImportedItems.Add("工作区配置");
        }
        catch (Exception ex)
        {
            return GlobalConfigImportResult.Fail($"当前工作区配置写入失败：{ex.Message}");
        }

        if (resolved.FactoryMapLayout is not null)
        {
            try
            {
                ReportProgress(progress, 65, "正在导入地图布局...", "factory-map.layout.json");
                BackupIfExists(currentFactoryMapLayoutPath, "factory-map.layout.import-backup");
                WriteJson(currentFactoryMapLayoutPath, resolved.FactoryMapLayout);
                result.ImportedItems.Add("地图布局");
                result.RequiresMapWindowReload = true;
                AddFactoryMapWarnings(resolved.WorkspaceConfig, resolved.FactoryMapLayout, result);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"地图布局导入失败：{ex.Message}");
            }
        }
        else
        {
            result.Warnings.Add("配置包不包含地图布局，已保留当前地图布局。");
        }

        ReportProgress(progress, 78, "正在应用界面偏好...", "地图视图、窗口状态和快捷项列宽");
        ApplyInterfacePreferences(
            currentConfigPath,
            currentFactoryMapLayoutPath,
            resolved.InterfacePreferences,
            appSettings,
            result);
        ReportProgress(progress, 88, "正在校验本机路径...", "VSCode 和软件更新 manifest");
        ApplyProgramSettings(resolved.MachineSettings, appSettings, result, resolveVSCodePath);
        return result;
    }

    public static string BuildDefaultExportFileName(DateTime now)
    {
        return "VSLoader_GlobalConfig.json";
    }

    private GlobalConfigPackage ReadPackage(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new InvalidOperationException("配置包路径为空。");
        }

        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("配置包文件不存在。", packagePath);
        }

        var json = File.ReadAllText(packagePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("配置包为空。");
        }

        return JsonSerializer.Deserialize<GlobalConfigPackage>(json, jsonOptions)
            ?? throw new InvalidOperationException("配置包格式无效。");
    }

    private static string? ValidatePackage(GlobalConfigPackage package)
    {
        if (!string.Equals(package.AppName, ExpectedAppName, StringComparison.Ordinal))
        {
            return "配置包不是 VSLoader 全局配置包。";
        }

        if (package.SchemaVersion != LegacySchemaVersion && package.SchemaVersion != CurrentSchemaVersion)
        {
            return $"配置包版本不支持：{package.SchemaVersion}。";
        }

        if (package.SchemaVersion == LegacySchemaVersion && package.WorkspaceConfig is null)
        {
            return "配置包格式无效，缺少 workspaceConfig。";
        }

        if (package.SchemaVersion == CurrentSchemaVersion && package.Workspace?.Settings is null)
        {
            return "配置包格式无效，缺少 workspace.settings。";
        }

        var factoryMapLayout = package.SchemaVersion == LegacySchemaVersion
            ? package.FactoryMapLayout
            : package.Workspace?.FactoryMapLayout;
        if (factoryMapLayout?.Version > CurrentFactoryMapLayoutVersion)
        {
            return $"配置包中的地图布局版本不支持：{factoryMapLayout.Version}。";
        }

        return null;
    }

    private static ResolvedGlobalConfigPackage ResolvePackage(GlobalConfigPackage package)
    {
        if (package.SchemaVersion == LegacySchemaVersion)
        {
            return new ResolvedGlobalConfigPackage(
                package.WorkspaceConfig ?? new AppConfig(),
                package.FactoryMapLayout,
                package.ProgramSettings ?? new GlobalProgramSettings(),
                null);
        }

        var workspace = package.Workspace ?? new GlobalConfigWorkspaceSection();
        return new ResolvedGlobalConfigPackage(
            ConvertWorkspaceSettings(workspace.Settings ?? new GlobalConfigWorkspaceSettings()),
            workspace.FactoryMapLayout,
            package.MachineSettings ?? new GlobalProgramSettings(),
            workspace.InterfacePreferences);
    }

    private GlobalConfigWorkspaceSource LoadWorkspaceSource(string factoryMapLayoutPath, List<string> warnings)
    {
        var workspaceDirectory = Path.GetDirectoryName(factoryMapLayoutPath);
        if (string.IsNullOrWhiteSpace(workspaceDirectory))
        {
            return new GlobalConfigWorkspaceSource();
        }

        var metadataPath = Path.Combine(workspaceDirectory, "workspace.json");
        if (!File.Exists(metadataPath))
        {
            return new GlobalConfigWorkspaceSource
            {
                Id = Path.GetFileName(workspaceDirectory),
                Name = Path.GetFileName(workspaceDirectory)
            };
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<WorkspaceMetadata>(File.ReadAllText(metadataPath), jsonOptions);
            return new GlobalConfigWorkspaceSource
            {
                Id = metadata?.Id?.Trim() ?? string.Empty,
                Name = metadata?.Name?.Trim() ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            warnings.Add($"工作区来源信息读取失败，本次仅导出业务配置：{ex.Message}");
            return new GlobalConfigWorkspaceSource();
        }
    }

    private GlobalConfigInterfacePreferences LoadInterfacePreferences(
        string factoryMapLayoutPath,
        IEnumerable<string>? settingsPageOrder,
        List<string> warnings)
    {
        var preferences = new GlobalConfigInterfacePreferences
        {
            SettingsPageOrder = SettingsPageOrderService.Normalize(settingsPageOrder).ToList()
        };
        var workspaceDirectory = Path.GetDirectoryName(factoryMapLayoutPath);
        if (string.IsNullOrWhiteSpace(workspaceDirectory))
        {
            return preferences;
        }

        var layoutPath = Path.Combine(workspaceDirectory, "window-layout.json");
        if (!File.Exists(layoutPath))
        {
            return preferences;
        }

        try
        {
            var layout = JsonSerializer.Deserialize<WindowLayoutConfig>(File.ReadAllText(layoutPath), jsonOptions);
            if (layout is null)
            {
                warnings.Add("当前窗口布局文件为空，本次未包含界面布局偏好。");
                return preferences;
            }

            preferences.FactoryMapWindowState = NormalizePortableFactoryMapWindowState(layout.FactoryMapWindowState);
            preferences.FactoryMapView = CloneFactoryMapView(layout.FactoryMapView);
            preferences.ShortcutGridColumns = CloneShortcutGridColumns(layout.ShortcutGridColumns);
        }
        catch (Exception ex)
        {
            warnings.Add($"当前窗口布局读取失败，本次未包含界面布局偏好：{ex.Message}");
        }

        return preferences;
    }

    private FactoryMapLayoutConfig? LoadFactoryMapLayout(string layoutPath, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(layoutPath) || !File.Exists(layoutPath))
        {
            warnings.Add("当前工作区没有地图布局文件，本次未包含地图布局。");
            return null;
        }

        try
        {
            var json = File.ReadAllText(layoutPath);
            var layout = JsonSerializer.Deserialize<FactoryMapLayoutConfig>(json, jsonOptions);
            if (layout is null)
            {
                warnings.Add("当前地图布局文件为空或格式无效，本次未包含地图布局。");
                return null;
            }

            if (layout.Version > CurrentFactoryMapLayoutVersion)
            {
                warnings.Add($"当前地图布局版本不支持：{layout.Version}，本次未包含地图布局。");
                return null;
            }

            NormalizeFactoryMapLayout(layout);
            return layout;
        }
        catch (Exception ex)
        {
            warnings.Add($"当前地图布局读取失败，本次未包含地图布局：{ex.Message}");
            return null;
        }
    }

    private void ApplyInterfacePreferences(
        string currentConfigPath,
        string currentFactoryMapLayoutPath,
        GlobalConfigInterfacePreferences? preferences,
        AppSettings appSettings,
        GlobalConfigImportResult result)
    {
        if (preferences is null)
        {
            return;
        }

        if (preferences.SettingsPageOrder is { Count: > 0 })
        {
            appSettings.SettingsPageOrder = SettingsPageOrderService.Normalize(preferences.SettingsPageOrder).ToList();
            result.ImportedItems.Add("设置页面顺序");
            result.Warnings.Add("设置页面顺序属于程序级界面偏好，导入后会影响所有工作区。");
        }

        var workspaceDirectory = Path.GetDirectoryName(currentFactoryMapLayoutPath);
        if (string.IsNullOrWhiteSpace(workspaceDirectory))
        {
            workspaceDirectory = Path.GetDirectoryName(currentConfigPath);
        }

        if (string.IsNullOrWhiteSpace(workspaceDirectory))
        {
            result.Warnings.Add("无法确定当前工作区目录，界面布局偏好未导入。");
            return;
        }

        var windowLayoutPath = Path.Combine(workspaceDirectory, "window-layout.json");
        try
        {
            var currentLayout = LoadWindowLayoutOrDefault(windowLayoutPath);
            currentLayout.FactoryMapWindowState = NormalizePortableFactoryMapWindowState(
                preferences.FactoryMapWindowState);

            var importedView = CloneFactoryMapView(preferences.FactoryMapView);
            if (preferences.FactoryMapView is not null && importedView is null)
            {
                result.Warnings.Add("配置包中的地图视图状态无效，已保留当前视图状态。");
            }
            else if (importedView is not null)
            {
                currentLayout.FactoryMapView = importedView;
            }

            var importedColumns = CloneShortcutGridColumns(preferences.ShortcutGridColumns);
            if (preferences.ShortcutGridColumns is not null && importedColumns is null)
            {
                result.Warnings.Add("配置包中的快捷项列宽无效，已保留当前列宽。");
            }
            else if (importedColumns is not null)
            {
                currentLayout.ShortcutGridColumns = importedColumns;
            }

            BackupIfExists(windowLayoutPath, "window-layout.import-backup");
            WriteJson(windowLayoutPath, currentLayout);
            result.ImportedItems.Add("工作区界面偏好");
            result.RequiresWindowLayoutReload = true;
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"工作区界面偏好导入失败：{ex.Message}");
        }
    }

    private WindowLayoutConfig LoadWindowLayoutOrDefault(string path)
    {
        if (!File.Exists(path))
        {
            return new WindowLayoutConfig();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WindowLayoutConfig();
        }

        return JsonSerializer.Deserialize<WindowLayoutConfig>(json, jsonOptions)
            ?? new WindowLayoutConfig();
    }

    private static string NormalizePortableFactoryMapWindowState(string? state)
    {
        return string.Equals(state, FactoryMapWindowStateKinds.WorkspaceMaximized, StringComparison.Ordinal)
            ? FactoryMapWindowStateKinds.WorkspaceMaximized
            : FactoryMapWindowStateKinds.Normal;
    }

    private static FactoryMapViewStateConfig? CloneFactoryMapView(FactoryMapViewStateConfig? source)
    {
        if (source is null
            || !IsFinitePositive(source.FitScale)
            || !IsFinitePositive(source.UserScale)
            || !double.IsFinite(source.OffsetX)
            || !double.IsFinite(source.OffsetY))
        {
            return null;
        }

        return new FactoryMapViewStateConfig
        {
            FitScale = source.FitScale,
            UserScale = source.UserScale,
            OffsetX = source.OffsetX,
            OffsetY = source.OffsetY
        };
    }

    private static ShortcutGridColumnLayoutConfig? CloneShortcutGridColumns(ShortcutGridColumnLayoutConfig? source)
    {
        if (source is null)
        {
            return null;
        }

        var clone = new ShortcutGridColumnLayoutConfig
        {
            Name = NormalizeColumnWidth(source.Name),
            Description = NormalizeColumnWidth(source.Description),
            SourceModuleName = NormalizeColumnWidth(source.SourceModuleName),
            UpdatedAt = NormalizeColumnWidth(source.UpdatedAt)
        };
        return clone.Name is null
            && clone.Description is null
            && clone.SourceModuleName is null
            && clone.UpdatedAt is null
                ? null
                : clone;
    }

    private static double? NormalizeColumnWidth(double? width)
    {
        return width is > 0 && double.IsFinite(width.Value)
            ? Math.Clamp(width.Value, 40, 2000)
            : null;
    }

    private static bool IsFinitePositive(double value)
    {
        return double.IsFinite(value) && value > 0;
    }

    private static void ApplyProgramSettings(
        GlobalProgramSettings settings,
        AppSettings appSettings,
        GlobalConfigImportResult result,
        Func<AppSettings, string?> resolveVSCodePath)
    {
        settings ??= new GlobalProgramSettings();
        var packageVSCodePath = settings.VSCodePath?.Trim() ?? string.Empty;
        if (VSCodeLauncherService.IsValidExecutablePath(packageVSCodePath))
        {
            appSettings.VSCodePath = packageVSCodePath;
            result.ImportedItems.Add("VSCode 路径");
        }
        else if (VSCodeLauncherService.IsValidExecutablePath(appSettings.VSCodePath))
        {
            result.Warnings.Add("VSCode 路径无效，已保留当前本机路径。");
        }
            else
            {
                var resolvedPath = resolveVSCodePath(appSettings) ?? string.Empty;
                if (VSCodeLauncherService.IsValidExecutablePath(resolvedPath))
                {
                appSettings.VSCodePath = resolvedPath;
                result.Warnings.Add("导入的 VSCode 路径无效，已自动识别 VSCode 路径。");
                result.ImportedItems.Add("VSCode 路径");
            }
            else
            {
                result.HasInvalidVSCodePath = true;
                result.Warnings.Add("VSCode 路径无效，且未能自动识别，请进入设置配置。");
            }
        }

        var packageManifestPath = settings.SoftwareUpdateManifestPath?.Trim() ?? string.Empty;
        if (File.Exists(packageManifestPath))
        {
            appSettings.SoftwareUpdateManifestPath = packageManifestPath;
            result.ImportedItems.Add("软件更新路径");
            result.Warnings.Add("软件更新路径属于程序级配置，导入后会影响所有工作区。");
        }
        else if (!string.IsNullOrWhiteSpace(packageManifestPath))
        {
            if (File.Exists(appSettings.SoftwareUpdateManifestPath))
            {
                result.Warnings.Add("软件更新路径无效，已保留当前本机路径。");
            }
            else
            {
                result.Warnings.Add($"软件更新路径不存在：{packageManifestPath}");
            }
        }
    }

    private static void AddPreflightWarnings(AppConfig config, GlobalConfigImportResult result)
    {
        if (!string.IsNullOrWhiteSpace(config.BatchImport.LastParentFolderPath) &&
            !Directory.Exists(config.BatchImport.LastParentFolderPath))
        {
            result.Warnings.Add($"批量识别父级文件夹不存在：{config.BatchImport.LastParentFolderPath}");
        }

        if (!string.IsNullOrWhiteSpace(config.BatchImport.LastCsvPath) &&
            !File.Exists(config.BatchImport.LastCsvPath))
        {
            result.Warnings.Add($"批量识别 CSV 文件不存在：{config.BatchImport.LastCsvPath}");
        }

        var missingShortcutCount = config.Shortcuts.Count(shortcut =>
            !string.IsNullOrWhiteSpace(shortcut.TargetPath) && !Directory.Exists(shortcut.TargetPath));
        if (missingShortcutCount > 0)
        {
            result.Warnings.Add($"有 {missingShortcutCount} 个快捷项目标路径当前不可访问。");
        }
    }

    private static void AddFactoryMapWarnings(
        AppConfig workspaceConfig,
        FactoryMapLayoutConfig factoryMapLayout,
        GlobalConfigImportResult result)
    {
        var shortcutKeys = workspaceConfig.Shortcuts
            .Select(shortcut => shortcut.TargetPath?.Trim() ?? string.Empty)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unmatchedNodeCount = factoryMapLayout.Devices.Count(device =>
            !string.IsNullOrWhiteSpace(device.Key) && !shortcutKeys.Contains(device.Key.Trim()));

        if (unmatchedNodeCount > 0)
        {
            result.Warnings.Add($"地图中有 {unmatchedNodeCount} 个节点未匹配到快捷项。");
        }
    }

    private static void AddFileWarning(string path, string label, List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
        {
            warnings.Add($"{label}：{path}");
        }
    }

    private void WriteJson<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(value, jsonOptions));
    }

    private static void ReportProgress(
        IProgress<GlobalConfigOperationProgress>? progress,
        int value,
        string message,
        string currentItem)
    {
        progress?.Report(new GlobalConfigOperationProgress(value, message, currentItem));
    }

    private static void BackupIfExists(string path, string prefix)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"当前工作区目录不存在：{directory}");
        }

        if (!File.Exists(path))
        {
            return;
        }

        var backupPath = Path.Combine(directory, $"{prefix}.{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
        File.Copy(path, backupPath, false);
    }

    private static GlobalConfigWorkspaceSettings CloneWorkspaceSettings(AppConfig config)
    {
        _ = NormalizeWorkspaceConfig(config);
        return new GlobalConfigWorkspaceSettings
        {
            Shortcuts = config.Shortcuts.Select(shortcut => shortcut.Clone()).ToList(),
            AdminUi = config.AdminUi.Clone(),
            Hotkey = config.Hotkey.Clone(),
            MapHotkey = config.MapHotkey.Clone(),
            BatchImport = config.BatchImport.Clone(),
            WebUi = config.WebUi.Clone(),
            UpdateCheck = new GlobalConfigWorkspaceUpdateSettings
            {
                GlobalConfigPackagePath = config.UpdateCheck.GlobalConfigPackagePath?.Trim() ?? string.Empty
            },
            ContextMenuCapabilities = config.ContextMenuCapabilities.Clone()
        };
    }

    private static AppConfig ConvertWorkspaceSettings(GlobalConfigWorkspaceSettings settings)
    {
        settings ??= new GlobalConfigWorkspaceSettings();
        return new AppConfig
        {
            VSCodePath = string.Empty,
            Shortcuts = settings.Shortcuts?.Select(shortcut => shortcut.Clone()).ToList() ?? [],
            AdminUi = settings.AdminUi?.Clone() ?? new AdminUiConfig(),
            Hotkey = settings.Hotkey?.Clone() ?? new HotkeyConfig(),
            MapHotkey = settings.MapHotkey?.Clone() ?? new MapHotkeyConfig(),
            BatchImport = settings.BatchImport?.Clone() ?? new BatchImportConfig(),
            WebUi = settings.WebUi?.Clone() ?? new WebUiConfig(),
            UpdateCheck = new UpdateCheckConfig
            {
                GlobalConfigPackagePath = settings.UpdateCheck?.GlobalConfigPackagePath?.Trim() ?? string.Empty
            },
            ContextMenuCapabilities = settings.ContextMenuCapabilities?.Clone()
                ?? new ContextMenuCapabilityCollectionConfig()
        };
    }

    private static IReadOnlyList<string> NormalizeWorkspaceConfig(AppConfig config)
    {
        config.Shortcuts ??= [];
        config.AdminUi ??= new AdminUiConfig();
        config.Hotkey ??= new HotkeyConfig();
        config.MapHotkey ??= new MapHotkeyConfig();
        config.BatchImport ??= new BatchImportConfig();
        config.WebUi ??= new WebUiConfig();
        config.UpdateCheck ??= new UpdateCheckConfig();
        config.ContextMenuCapabilities ??= new ContextMenuCapabilityCollectionConfig();
        config.VSCodePath ??= string.Empty;
        NormalizeMapHotkeyConfig(config.MapHotkey);
        var capabilityWarnings = new ContextMenuCapabilityConfigService().Normalize(config.ContextMenuCapabilities);

        foreach (var shortcut in config.Shortcuts)
        {
            shortcut.Name ??= string.Empty;
            shortcut.TargetPath ??= string.Empty;
            shortcut.Description ??= string.Empty;
            shortcut.SourceModuleName ??= string.Empty;
        }

        return capabilityWarnings;
    }

    private static void NormalizeMapHotkeyConfig(MapHotkeyConfig mapHotkey)
    {
        mapHotkey.Key = mapHotkey.Key?.Trim() ?? string.Empty;
        if (!mapHotkey.Enabled)
        {
            return;
        }

        if (string.Equals(mapHotkey.Key, "M", StringComparison.OrdinalIgnoreCase))
        {
            mapHotkey.Ctrl = false;
            mapHotkey.Alt = true;
            mapHotkey.Shift = false;
            mapHotkey.Key = "X";
            return;
        }

        if (mapHotkey.Ctrl || mapHotkey.Alt || mapHotkey.Shift)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(mapHotkey.Key))
        {
            mapHotkey.Key = "X";
        }

        mapHotkey.Alt = true;
    }

    private static void NormalizeFactoryMapLayout(FactoryMapLayoutConfig layout)
    {
        layout.Canvas ??= new FactoryMapCanvas { Width = 1600, Height = 900 };
        layout.Devices ??= [];
        layout.ConnectionPoints ??= [];
        layout.Segments ??= [];

        foreach (var device in layout.Devices)
        {
            device.Id ??= string.Empty;
            device.Key ??= string.Empty;
            device.Name ??= string.Empty;
        }

        foreach (var point in layout.ConnectionPoints)
        {
            point.Id ??= string.Empty;
            point.Kind = FactoryMapConnectionPointKinds.Normalize(point.Kind);
            point.OwnerNodeId ??= string.Empty;
            point.Side ??= string.Empty;
            point.JunctionAxis = point.Kind == FactoryMapConnectionPointKinds.Junction
                ? FactoryMapJunctionAxes.Normalize(point.JunctionAxis)
                : string.Empty;
        }

        foreach (var segment in layout.Segments)
        {
            segment.Id ??= string.Empty;
            segment.FromPointId ??= string.Empty;
            segment.ToPointId ??= string.Empty;
        }

        if (layout.Version >= 4)
        {
            layout.Connectors = null;
            layout.Edges = null;
            return;
        }

        layout.Connectors ??= [];
        layout.Edges ??= [];
        foreach (var connector in layout.Connectors)
        {
            connector.Id ??= string.Empty;
        }

        foreach (var edge in layout.Edges)
        {
            edge.From ??= string.Empty;
            edge.FromKind = string.Equals(edge.FromKind, FactoryMapEndpointKinds.Connector, StringComparison.OrdinalIgnoreCase)
                ? FactoryMapEndpointKinds.Connector
                : FactoryMapEndpointKinds.Device;
            edge.To ??= string.Empty;
            edge.ToKind = string.Equals(edge.ToKind, FactoryMapEndpointKinds.Connector, StringComparison.OrdinalIgnoreCase)
                ? FactoryMapEndpointKinds.Connector
                : FactoryMapEndpointKinds.Device;
        }
    }

    private sealed record ResolvedGlobalConfigPackage(
        AppConfig WorkspaceConfig,
        FactoryMapLayoutConfig? FactoryMapLayout,
        GlobalProgramSettings MachineSettings,
        GlobalConfigInterfacePreferences? InterfacePreferences);
}
