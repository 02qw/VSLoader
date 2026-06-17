using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class GlobalConfigPackageServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _workspacePath;
    private readonly string _otherWorkspacePath;
    private readonly string _packagePath;
    private readonly GlobalConfigPackageService _service = new();

    public GlobalConfigPackageServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        _workspacePath = Path.Combine(_rootPath, "work1");
        _otherWorkspacePath = Path.Combine(_rootPath, "work2");
        _packagePath = Path.Combine(_rootPath, "global-config.json");
        Directory.CreateDirectory(_workspacePath);
        Directory.CreateDirectory(_otherWorkspacePath);
    }

    [Fact]
    public void Export_writes_workspace_config_program_settings_and_factory_map_layout()
    {
        var config = new AppConfig
        {
            Shortcuts =
            [
                new ShortcutItem { Name = "热贴机_001", TargetPath = @"C:\Line\TCDE001" }
            ],
            BatchImport = new BatchImportConfig
            {
                LastParentFolderPath = @"\\server\line",
                LastCsvPath = @"\\server\rules.csv"
            }
        };
        var appSettings = new AppSettings
        {
            VSCodePath = @"C:\Tools\Code.exe",
            SoftwareUpdateManifestPath = @"\\server\manifest.json",
            Workspaces = [new WorkspaceInfo { Id = "work1", Name = "工作区1", Path = _workspacePath }],
            LastWorkspaceId = "work1"
        };
        var layoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(layoutPath, new FactoryMapLayoutConfig
        {
            Devices = [new FactoryMapDeviceNode { Key = @"C:\Line\TCDE001", Name = "热贴机_001", X = 10, Y = 20 }]
        });

        var result = _service.Export(_packagePath, config, appSettings, layoutPath);

        Assert.True(result.Success, result.ErrorMessage);
        var package = ReadJson<GlobalConfigPackage>(_packagePath);
        Assert.Equal(1, package.SchemaVersion);
        Assert.Equal("VSLoader", package.AppName);
        Assert.Equal(@"C:\Tools\Code.exe", package.ProgramSettings.VSCodePath);
        Assert.Equal(@"\\server\manifest.json", package.ProgramSettings.SoftwareUpdateManifestPath);
        Assert.Single(package.WorkspaceConfig.Shortcuts);
        Assert.Equal(@"\\server\rules.csv", package.WorkspaceConfig.BatchImport.LastCsvPath);
        Assert.NotNull(package.FactoryMapLayout);
        Assert.Single(package.FactoryMapLayout!.Devices);
    }

    [Fact]
    public void Import_writes_only_current_workspace_and_does_not_change_workspace_list()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var otherConfigPath = Path.Combine(_otherWorkspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        var otherLayoutPath = Path.Combine(_otherWorkspacePath, "factory-map.layout.json");
        WriteJson(currentConfigPath, new AppConfig { Shortcuts = [new ShortcutItem { Name = "旧", TargetPath = @"C:\Old" }] });
        WriteJson(otherConfigPath, new AppConfig { Shortcuts = [new ShortcutItem { Name = "其他", TargetPath = @"C:\Other" }] });
        WriteJson(currentLayoutPath, new FactoryMapLayoutConfig { Devices = [new FactoryMapDeviceNode { Key = @"C:\Old", Name = "旧" }] });
        WriteJson(otherLayoutPath, new FactoryMapLayoutConfig { Devices = [new FactoryMapDeviceNode { Key = @"C:\Other", Name = "其他" }] });
        var settings = new AppSettings
        {
            VSCodePath = @"C:\Missing\Code.exe",
            SoftwareUpdateManifestPath = @"C:\Missing\manifest.json",
            LastWorkspaceId = "work2",
            Workspaces =
            [
                new WorkspaceInfo { Id = "work1", Name = "工作区1", Path = _workspacePath },
                new WorkspaceInfo { Id = "work2", Name = "工作区2", Path = _otherWorkspacePath }
            ]
        };
        WritePackage(new GlobalConfigPackage
        {
            ProgramSettings = new GlobalProgramSettings
            {
                VSCodePath = @"C:\Invalid\Code.exe",
                SoftwareUpdateManifestPath = @"C:\Invalid\manifest.json"
            },
            WorkspaceConfig = new AppConfig
            {
                Shortcuts = [new ShortcutItem { Name = "新", TargetPath = @"C:\New" }]
            },
            FactoryMapLayout = new FactoryMapLayoutConfig
            {
                Devices = [new FactoryMapDeviceNode { Key = @"C:\New", Name = "新", X = 100, Y = 200 }]
            }
        });

        var result = _service.Import(_packagePath, currentConfigPath, currentLayoutPath, settings, _ => null);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("work2", settings.LastWorkspaceId);
        Assert.Equal(2, settings.Workspaces.Count);
        Assert.Equal(@"C:\Missing\Code.exe", settings.VSCodePath);
        Assert.Contains(result.Warnings, warning => warning.Contains("VSCode 路径无效", StringComparison.Ordinal));
        Assert.Equal("新", ReadJson<AppConfig>(currentConfigPath).Shortcuts.Single().Name);
        Assert.Equal("其他", ReadJson<AppConfig>(otherConfigPath).Shortcuts.Single().Name);
        Assert.Equal(@"C:\New", ReadJson<FactoryMapLayoutConfig>(currentLayoutPath).Devices.Single().Key);
        Assert.Equal(@"C:\Other", ReadJson<FactoryMapLayoutConfig>(otherLayoutPath).Devices.Single().Key);
        Assert.True(Directory.EnumerateFiles(_workspacePath, "config.import-backup.*.json").Any());
        Assert.True(Directory.EnumerateFiles(_workspacePath, "factory-map.layout.import-backup.*.json").Any());
    }

    [Fact]
    public void Import_keeps_current_valid_vscode_path_when_package_path_is_invalid()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        var validCodePath = Path.Combine(_rootPath, "Code.exe");
        File.WriteAllText(validCodePath, string.Empty);
        WriteJson(currentConfigPath, new AppConfig());
        var settings = new AppSettings { VSCodePath = validCodePath };
        WritePackage(new GlobalConfigPackage
        {
            ProgramSettings = new GlobalProgramSettings { VSCodePath = @"C:\Invalid\Code.exe" },
            WorkspaceConfig = new AppConfig()
        });

        var result = _service.Import(_packagePath, currentConfigPath, currentLayoutPath, settings, _ => null);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(validCodePath, settings.VSCodePath);
        Assert.Contains(result.Warnings, warning => warning.Contains("已保留当前本机路径", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_uses_resolved_vscode_path_when_package_and_current_paths_are_invalid()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        var resolvedCodePath = Path.Combine(_rootPath, "ResolvedCode.exe");
        File.WriteAllText(resolvedCodePath, string.Empty);
        WriteJson(currentConfigPath, new AppConfig());
        var settings = new AppSettings { VSCodePath = @"C:\Invalid\Current.exe" };
        WritePackage(new GlobalConfigPackage
        {
            ProgramSettings = new GlobalProgramSettings { VSCodePath = @"C:\Invalid\Package.exe" },
            WorkspaceConfig = new AppConfig()
        });

        var result = _service.Import(_packagePath, currentConfigPath, currentLayoutPath, settings, _ => resolvedCodePath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(resolvedCodePath, settings.VSCodePath);
        Assert.Contains(result.Warnings, warning => warning.Contains("已自动识别 VSCode 路径", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_ignores_legacy_software_version_file_path_warning()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(currentConfigPath, new AppConfig());
        WritePackage(new GlobalConfigPackage
        {
            ProgramSettings = new GlobalProgramSettings(),
            WorkspaceConfig = new AppConfig
            {
                UpdateCheck = new UpdateCheckConfig
                {
                    SoftwareVersionFilePath = Path.Combine(_rootPath, "missing-version.txt")
                }
            }
        });

        var result = _service.Import(_packagePath, currentConfigPath, currentLayoutPath, new AppSettings(), _ => null);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("软件版本文件不存在", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_rejects_invalid_package_without_writing_current_config()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(currentConfigPath, new AppConfig { Shortcuts = [new ShortcutItem { Name = "旧", TargetPath = @"C:\Old" }] });
        File.WriteAllText(_packagePath, "{ broken json");

        var result = _service.Import(_packagePath, currentConfigPath, currentLayoutPath, new AppSettings(), _ => null);

        Assert.False(result.Success);
        Assert.Contains("配置包读取失败", result.ErrorMessage);
        Assert.Equal("旧", ReadJson<AppConfig>(currentConfigPath).Shortcuts.Single().Name);
    }

    private void WritePackage(GlobalConfigPackage package)
    {
        WriteJson(_packagePath, package);
    }

    private static void WriteJson<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static T ReadJson<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
