using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class GlobalConfigPackageService
{
    private const int CurrentSchemaVersion = 1;
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
            var package = new GlobalConfigPackage
            {
                SchemaVersion = CurrentSchemaVersion,
                AppName = ExpectedAppName,
                ExportedAt = DateTimeOffset.Now.ToString("O"),
                ProgramSettings = new GlobalProgramSettings
                {
                    VSCodePath = appSettings.VSCodePath ?? string.Empty,
                    SoftwareUpdateManifestPath = appSettings.SoftwareUpdateManifestPath ?? string.Empty
                },
                WorkspaceConfig = CloneWorkspaceConfig(workspaceConfig),
                FactoryMapLayout = LoadFactoryMapLayout(factoryMapLayoutPath, result.Warnings)
            };

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
        Func<AppSettings, string?> resolveVSCodePath)
    {
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

        var result = GlobalConfigImportResult.Ok();
        NormalizeWorkspaceConfig(package.WorkspaceConfig);
        AddPreflightWarnings(package, result);

        try
        {
            BackupIfExists(currentConfigPath, "config.import-backup");
            WriteJson(currentConfigPath, package.WorkspaceConfig);
            result.ImportedItems.Add("工作区配置");
        }
        catch (Exception ex)
        {
            return GlobalConfigImportResult.Fail($"当前工作区配置写入失败：{ex.Message}");
        }

        if (package.FactoryMapLayout is not null)
        {
            try
            {
                BackupIfExists(currentFactoryMapLayoutPath, "factory-map.layout.import-backup");
                WriteJson(currentFactoryMapLayoutPath, package.FactoryMapLayout);
                result.ImportedItems.Add("地图布局");
                result.RequiresMapWindowReload = true;
                AddFactoryMapWarnings(package, result);
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

        ApplyProgramSettings(package.ProgramSettings, appSettings, result, resolveVSCodePath);
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

        if (package.SchemaVersion != CurrentSchemaVersion)
        {
            return $"配置包版本不支持：{package.SchemaVersion}。";
        }

        if (package.WorkspaceConfig is null)
        {
            return "配置包格式无效，缺少 workspaceConfig。";
        }

        return null;
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

            NormalizeFactoryMapLayout(layout);
            return layout;
        }
        catch (Exception ex)
        {
            warnings.Add($"当前地图布局读取失败，本次未包含地图布局：{ex.Message}");
            return null;
        }
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

    private static void AddPreflightWarnings(GlobalConfigPackage package, GlobalConfigImportResult result)
    {
        var config = package.WorkspaceConfig;
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

    private static void AddFactoryMapWarnings(GlobalConfigPackage package, GlobalConfigImportResult result)
    {
        if (package.FactoryMapLayout is null)
        {
            return;
        }

        var shortcutKeys = package.WorkspaceConfig.Shortcuts
            .Select(shortcut => shortcut.TargetPath?.Trim() ?? string.Empty)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unmatchedNodeCount = package.FactoryMapLayout.Devices.Count(device =>
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

    private static AppConfig CloneWorkspaceConfig(AppConfig config)
    {
        NormalizeWorkspaceConfig(config);
        return new AppConfig
        {
            VSCodePath = config.VSCodePath,
            Shortcuts = config.Shortcuts.Select(shortcut => shortcut.Clone()).ToList(),
            AdminUi = config.AdminUi.Clone(),
            Hotkey = config.Hotkey.Clone(),
            MapHotkey = config.MapHotkey.Clone(),
            BatchImport = config.BatchImport.Clone(),
            WebUi = config.WebUi.Clone(),
            UpdateCheck = config.UpdateCheck.Clone()
        };
    }

    private static void NormalizeWorkspaceConfig(AppConfig config)
    {
        config.Shortcuts ??= [];
        config.AdminUi ??= new AdminUiConfig();
        config.Hotkey ??= new HotkeyConfig();
        config.MapHotkey ??= new MapHotkeyConfig();
        config.BatchImport ??= new BatchImportConfig();
        config.WebUi ??= new WebUiConfig();
        config.UpdateCheck ??= new UpdateCheckConfig();
        config.VSCodePath ??= string.Empty;
        NormalizeMapHotkeyConfig(config.MapHotkey);

        foreach (var shortcut in config.Shortcuts)
        {
            shortcut.Name ??= string.Empty;
            shortcut.TargetPath ??= string.Empty;
            shortcut.Description ??= string.Empty;
            shortcut.SourceModuleName ??= string.Empty;
        }
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
        layout.Edges ??= [];

        foreach (var device in layout.Devices)
        {
            device.Key ??= string.Empty;
            device.Name ??= string.Empty;
        }

        foreach (var edge in layout.Edges)
        {
            edge.From ??= string.Empty;
            edge.To ??= string.Empty;
        }
    }
}
