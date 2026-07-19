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
    public void BuildDefaultExportFileName_uses_stable_file_name_without_timestamp()
    {
        var fileName = GlobalConfigPackageService.BuildDefaultExportFileName(
            new DateTime(2026, 7, 7, 10, 30, 0));

        Assert.Equal("VSLoader_GlobalConfig.json", fileName);
    }

    [Fact]
    public void Export_writes_schema2_workspace_snapshot_without_legacy_update_fields()
    {
        var layoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(layoutPath, new FactoryMapLayoutConfig
        {
            Version = 5,
            Devices = [new FactoryMapDeviceNode { Id = "node-a", Key = @"C:\Line\A", Name = "设备A" }]
        });
        WriteJson(Path.Combine(_workspacePath, "workspace.json"), new WorkspaceMetadata
        {
            Id = "work1",
            Name = "一号产线"
        });
        WriteJson(Path.Combine(_workspacePath, "window-layout.json"), new WindowLayoutConfig
        {
            MainWindow = new WindowBoundsConfig { Left = 10, Top = 20, Width = 1200, Height = 800 },
            FactoryMapWindowState = FactoryMapWindowStateKinds.WorkspaceMaximized,
            FactoryMapView = new FactoryMapViewStateConfig
            {
                FitScale = 0.8,
                UserScale = 1.25,
                OffsetX = -120,
                OffsetY = 80
            },
            ShortcutGridColumns = new ShortcutGridColumnLayoutConfig
            {
                Name = 220,
                Description = 300,
                SourceModuleName = 180,
                UpdatedAt = 160
            }
        });
        var config = new AppConfig
        {
            VSCodePath = @"C:\legacy\Code.exe",
            Shortcuts = [new ShortcutItem { Name = "设备A", TargetPath = @"C:\Line\A" }],
            AdminUi = new AdminUiConfig { ProtectedPassword = "plain-password" },
            UpdateCheck = new UpdateCheckConfig
            {
                GlobalConfigPackagePath = @"\\server\VSLoader_GlobalConfig.json",
                RulesFilePath = @"\\server\rules.csv",
                MapFilePath = @"\\server\map.json",
                SoftwareVersionFilePath = @"\\server\version.txt"
            }
        };
        var settings = new AppSettings
        {
            VSCodePath = @"C:\Tools\Code.exe",
            SoftwareUpdateManifestPath = @"\\server\manifest.json",
            SettingsPageOrder = ["hotkeys", "general", "adminUi", "webUi", "updates", "contextMenuCapabilities"]
        };

        var result = _service.Export(_packagePath, config, settings, layoutPath);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains(result.Warnings, warning => warning.Contains("明文密码", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(File.ReadAllText(_packagePath));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("SchemaVersion").GetInt32());
        Assert.False(root.TryGetProperty("ProgramSettings", out _));
        Assert.False(root.TryGetProperty("WorkspaceConfig", out _));
        Assert.False(root.TryGetProperty("FactoryMapLayout", out _));

        var workspace = root.GetProperty("Workspace");
        Assert.Equal("一号产线", workspace.GetProperty("Source").GetProperty("Name").GetString());
        Assert.Single(workspace.GetProperty("Settings").GetProperty("Shortcuts").EnumerateArray());
        var updateCheck = workspace.GetProperty("Settings").GetProperty("UpdateCheck");
        Assert.Equal(@"\\server\VSLoader_GlobalConfig.json", updateCheck.GetProperty("GlobalConfigPackagePath").GetString());
        Assert.False(updateCheck.TryGetProperty("RulesFilePath", out _));
        Assert.False(updateCheck.TryGetProperty("MapFilePath", out _));
        Assert.False(updateCheck.TryGetProperty("SoftwareVersionFilePath", out _));

        var preferences = workspace.GetProperty("InterfacePreferences");
        Assert.Equal(1.25, preferences.GetProperty("FactoryMapView").GetProperty("UserScale").GetDouble());
        Assert.Equal(220, preferences.GetProperty("ShortcutGridColumns").GetProperty("Name").GetDouble());
        Assert.Equal("hotkeys", preferences.GetProperty("SettingsPageOrder")[0].GetString());
        Assert.Equal(@"C:\Tools\Code.exe", root.GetProperty("MachineSettings").GetProperty("VSCodePath").GetString());
    }

    [Fact]
    public void Import_schema2_merges_portable_interface_preferences_without_overwriting_window_bounds()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        var currentWindowLayoutPath = Path.Combine(_workspacePath, "window-layout.json");
        WriteJson(currentConfigPath, new AppConfig());
        WriteJson(currentWindowLayoutPath, new WindowLayoutConfig
        {
            MainWindow = new WindowBoundsConfig { Left = 100, Top = 110, Width = 1000, Height = 700 },
            FactoryMapWindow = new WindowBoundsConfig { Left = 200, Top = 210, Width = 900, Height = 650 },
            WasFactoryMapOpen = true,
            FactoryMapWindowState = FactoryMapWindowStateKinds.Normal,
            FactoryMapView = new FactoryMapViewStateConfig { FitScale = 1, UserScale = 1, OffsetX = 0, OffsetY = 0 }
        });
        WriteJson(_packagePath, new
        {
            SchemaVersion = 2,
            AppName = "VSLoader",
            ExportedAt = DateTimeOffset.Now.ToString("O"),
            Workspace = new
            {
                Source = new { Id = "source", Name = "来源工作区" },
                Settings = new
                {
                    Shortcuts = new[] { new ShortcutItem { Name = "新设备", TargetPath = @"C:\New" } },
                    AdminUi = new AdminUiConfig(),
                    Hotkey = new HotkeyConfig(),
                    MapHotkey = new MapHotkeyConfig(),
                    BatchImport = new BatchImportConfig(),
                    WebUi = new WebUiConfig(),
                    UpdateCheck = new { GlobalConfigPackagePath = string.Empty },
                    ContextMenuCapabilities = new ContextMenuCapabilityCollectionConfig()
                },
                FactoryMapLayout = new FactoryMapLayoutConfig { Version = 5 },
                InterfacePreferences = new
                {
                    SettingsPageOrder = new[] { "hotkeys", "general", "adminUi", "webUi", "updates", "contextMenuCapabilities" },
                    FactoryMapWindowState = FactoryMapWindowStateKinds.WorkspaceMaximized,
                    FactoryMapView = new FactoryMapViewStateConfig
                    {
                        FitScale = 0.9,
                        UserScale = 1.4,
                        OffsetX = -300,
                        OffsetY = 120
                    },
                    ShortcutGridColumns = new ShortcutGridColumnLayoutConfig { Name = 260, Description = 320 }
                }
            },
            MachineSettings = new GlobalProgramSettings()
        });
        var appSettings = new AppSettings();

        var result = _service.Import(_packagePath, currentConfigPath, currentLayoutPath, appSettings, _ => null);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("新设备", ReadJson<AppConfig>(currentConfigPath).Shortcuts.Single().Name);
        var importedLayout = ReadJson<WindowLayoutConfig>(currentWindowLayoutPath);
        Assert.Equal(100, importedLayout.MainWindow!.Left);
        Assert.Equal(900, importedLayout.FactoryMapWindow!.Width);
        Assert.True(importedLayout.WasFactoryMapOpen);
        Assert.Equal(FactoryMapWindowStateKinds.WorkspaceMaximized, importedLayout.FactoryMapWindowState);
        Assert.Equal(1.4, importedLayout.FactoryMapView!.UserScale);
        Assert.Equal(260, importedLayout.ShortcutGridColumns!.Name);
        Assert.Equal("hotkeys", appSettings.SettingsPageOrder[0]);
        Assert.Contains("工作区界面偏好", result.ImportedItems);
    }

    [Fact]
    public void Import_schema2_rejects_missing_workspace_settings_without_overwriting_current_config()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(currentConfigPath, new AppConfig
        {
            Shortcuts = [new ShortcutItem { Name = "保留项", TargetPath = @"C:\Keep" }]
        });
        WriteJson(_packagePath, new
        {
            SchemaVersion = 2,
            AppName = "VSLoader",
            ExportedAt = DateTimeOffset.Now.ToString("O"),
            Workspace = new { Source = new { Id = "source", Name = "来源" } },
            MachineSettings = new GlobalProgramSettings()
        });

        var result = _service.Import(
            _packagePath,
            currentConfigPath,
            currentLayoutPath,
            new AppSettings(),
            _ => null);

        Assert.False(result.Success);
        Assert.Contains("workspace.settings", result.ErrorMessage);
        Assert.Equal("保留项", ReadJson<AppConfig>(currentConfigPath).Shortcuts.Single().Name);
    }

    [Fact]
    public void Import_schema2_without_interface_preferences_preserves_current_window_layout()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        var currentWindowLayoutPath = Path.Combine(_workspacePath, "window-layout.json");
        WriteJson(currentConfigPath, new AppConfig());
        WriteJson(currentWindowLayoutPath, new WindowLayoutConfig
        {
            FactoryMapWindowState = FactoryMapWindowStateKinds.WorkspaceMaximized,
            FactoryMapView = new FactoryMapViewStateConfig
            {
                FitScale = 0.8,
                UserScale = 1.3,
                OffsetX = -200,
                OffsetY = 50
            }
        });
        WriteJson(_packagePath, new
        {
            SchemaVersion = 2,
            AppName = "VSLoader",
            ExportedAt = DateTimeOffset.Now.ToString("O"),
            Workspace = new
            {
                Source = new { Id = "source", Name = "来源" },
                Settings = new GlobalConfigWorkspaceSettings()
            },
            MachineSettings = new GlobalProgramSettings()
        });

        var result = _service.Import(
            _packagePath,
            currentConfigPath,
            currentLayoutPath,
            new AppSettings(),
            _ => null);

        Assert.True(result.Success, result.ErrorMessage);
        var layout = ReadJson<WindowLayoutConfig>(currentWindowLayoutPath);
        Assert.Equal(FactoryMapWindowStateKinds.WorkspaceMaximized, layout.FactoryMapWindowState);
        Assert.Equal(1.3, layout.FactoryMapView!.UserScale);
        Assert.DoesNotContain("工作区界面偏好", result.ImportedItems);
    }

    [Fact]
    public void Export_writes_workspace_settings_machine_settings_and_factory_map_layout()
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
            },
            UpdateCheck = new UpdateCheckConfig
            {
                GlobalConfigPackagePath = @"\\server\VSLoader_GlobalConfig.json"
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
        Assert.Equal(2, package.SchemaVersion);
        Assert.Equal("VSLoader", package.AppName);
        Assert.Equal(@"C:\Tools\Code.exe", package.MachineSettings!.VSCodePath);
        Assert.Equal(@"\\server\manifest.json", package.MachineSettings.SoftwareUpdateManifestPath);
        Assert.Single(package.Workspace!.Settings!.Shortcuts);
        Assert.Equal(@"\\server\rules.csv", package.Workspace.Settings.BatchImport.LastCsvPath);
        Assert.Equal(@"\\server\VSLoader_GlobalConfig.json", package.Workspace.Settings.UpdateCheck.GlobalConfigPackagePath);
        Assert.NotNull(package.Workspace.FactoryMapLayout);
        Assert.Single(package.Workspace.FactoryMapLayout!.Devices);
    }

    [Fact]
    public void Export_and_import_preserve_version5_junction_topology_without_legacy_fields()
    {
        var layoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(layoutPath, new FactoryMapLayoutConfig
        {
            Version = 5,
            Devices = [new FactoryMapDeviceNode { Id = "node-a", Key = "A", Name = "设备A", X = 100, Y = 100 }],
            ConnectionPoints =
            [
                new FactoryMapConnectionPoint
                {
                    Id = "junction-1",
                    Kind = FactoryMapConnectionPointKinds.Junction,
                    JunctionAxis = FactoryMapJunctionAxes.Horizontal,
                    X = 400,
                    Y = 129
                }
            ],
            Segments = [new FactoryMapSegment { Id = "segment-1", FromPointId = "node-a:right", ToPointId = "junction-1", ZIndex = 8 }]
        });

        var exported = _service.Export(_packagePath, new AppConfig(), new AppSettings(), layoutPath);

        Assert.True(exported.Success, exported.ErrorMessage);
        var packageJson = File.ReadAllText(_packagePath);
        using var packageDocument = JsonDocument.Parse(packageJson);
        var layout = packageDocument.RootElement.GetProperty("Workspace").GetProperty("FactoryMapLayout");
        Assert.Equal(5, layout.GetProperty("Version").GetInt32());
        Assert.Equal("horizontal", layout.GetProperty("ConnectionPoints")[0].GetProperty("JunctionAxis").GetString());
        Assert.False(layout.TryGetProperty("Connectors", out _));
        Assert.False(layout.TryGetProperty("Edges", out _));

        var currentConfigPath = Path.Combine(_otherWorkspacePath, "config.json");
        var importedLayoutPath = Path.Combine(_otherWorkspacePath, "factory-map.layout.json");
        WriteJson(currentConfigPath, new AppConfig());
        var imported = _service.Import(
            _packagePath,
            currentConfigPath,
            importedLayoutPath,
            new AppSettings(),
            _ => null);

        Assert.True(imported.Success, imported.ErrorMessage);
        var importedLayout = ReadJson<FactoryMapLayoutConfig>(importedLayoutPath);
        Assert.Equal("segment-1", Assert.Single(importedLayout.Segments).Id);
        Assert.Equal(8, importedLayout.Segments.Single().ZIndex);
        var importedJunction = Assert.Single(importedLayout.ConnectionPoints);
        Assert.Equal(FactoryMapConnectionPointKinds.Junction, importedJunction.Kind);
        Assert.Equal(FactoryMapJunctionAxes.Horizontal, importedJunction.JunctionAxis);
    }

    [Fact]
    public void Import_rejects_package_with_future_factory_map_layout_version()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(currentConfigPath, new AppConfig());
        WritePackage(new GlobalConfigPackage
        {
            WorkspaceConfig = new AppConfig(),
            FactoryMapLayout = new FactoryMapLayoutConfig { Version = 99 }
        });

        var result = _service.Import(
            _packagePath,
            currentConfigPath,
            currentLayoutPath,
            new AppSettings(),
            _ => null);

        Assert.False(result.Success);
        Assert.Contains("地图布局版本", result.ErrorMessage);
        Assert.False(File.Exists(currentLayoutPath));
    }

    [Fact]
    public void Export_writes_map_hotkey_config()
    {
        var config = new AppConfig
        {
            MapHotkey = new MapHotkeyConfig
            {
                Enabled = true,
                Ctrl = true,
                Alt = true,
                Shift = false,
                Key = "K"
            }
        };

        var result = _service.Export(_packagePath, config, new AppSettings(), string.Empty);

        Assert.True(result.Success, result.ErrorMessage);
        var package = ReadJson<GlobalConfigPackage>(_packagePath);
        Assert.True(package.Workspace!.Settings!.MapHotkey.Enabled);
        Assert.True(package.Workspace.Settings.MapHotkey.Ctrl);
        Assert.True(package.Workspace.Settings.MapHotkey.Alt);
        Assert.False(package.Workspace.Settings.MapHotkey.Shift);
        Assert.Equal("K", package.Workspace.Settings.MapHotkey.Key);
    }

    [Fact]
    public void Export_and_import_preserve_context_menu_capabilities()
    {
        var customCapability = new ContextMenuCapabilityDefinition
        {
            Id = "custom-web",
            Name = "查询设备",
            Kind = ContextMenuCapabilityKinds.Web,
            Order = 40,
            ShowInShortcutList = true,
            ShowInFactoryMap = false,
            RequiresExistingTargetPath = false,
            Web = new WebCapabilityConfig
            {
                UrlTemplate = "https://example.com/?name={ShortcutName}"
            }
        };
        var config = new AppConfig
        {
            ContextMenuCapabilities = new ContextMenuCapabilityCollectionConfig
            {
                Items = ContextMenuCapabilityDefaults.Create().Items
                    .Concat([customCapability])
                    .Select(item => item.Clone())
                    .ToList()
            }
        };

        var exported = _service.Export(_packagePath, config, new AppSettings(), string.Empty);

        Assert.True(exported.Success, exported.ErrorMessage);
        var package = ReadJson<GlobalConfigPackage>(_packagePath);
        Assert.Contains(package.Workspace!.Settings!.ContextMenuCapabilities.Items, item => item.Id == "custom-web");

        var currentConfigPath = Path.Combine(_otherWorkspacePath, "config.json");
        WriteJson(currentConfigPath, new AppConfig());
        var imported = _service.Import(
            _packagePath,
            currentConfigPath,
            Path.Combine(_otherWorkspacePath, "factory-map.layout.json"),
            new AppSettings(),
            _ => null);

        Assert.True(imported.Success, imported.ErrorMessage);
        var importedConfig = ReadJson<AppConfig>(currentConfigPath);
        var importedCapability = Assert.Single(
            importedConfig.ContextMenuCapabilities.Items,
            item => item.Id == "custom-web");
        Assert.False(importedCapability.ShowInFactoryMap);
        Assert.Equal("https://example.com/?name={ShortcutName}", importedCapability.Web.UrlTemplate);
    }

    [Fact]
    public void Import_writes_map_hotkey_config_to_current_workspace()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(currentConfigPath, new AppConfig());
        WritePackage(new GlobalConfigPackage
        {
            WorkspaceConfig = new AppConfig
            {
                MapHotkey = new MapHotkeyConfig
                {
                    Enabled = true,
                    Ctrl = false,
                    Alt = true,
                    Shift = true,
                    Key = "L"
                }
            }
        });

        var result = _service.Import(_packagePath, currentConfigPath, currentLayoutPath, new AppSettings(), _ => null);

        Assert.True(result.Success, result.ErrorMessage);
        var imported = ReadJson<AppConfig>(currentConfigPath);
        Assert.True(imported.MapHotkey.Enabled);
        Assert.False(imported.MapHotkey.Ctrl);
        Assert.True(imported.MapHotkey.Alt);
        Assert.True(imported.MapHotkey.Shift);
        Assert.Equal("L", imported.MapHotkey.Key);
    }

    [Fact]
    public void Import_migrates_legacy_single_key_map_hotkey()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(currentConfigPath, new AppConfig());
        WritePackage(new GlobalConfigPackage
        {
            WorkspaceConfig = new AppConfig
            {
                MapHotkey = new MapHotkeyConfig
                {
                    Enabled = true,
                    Ctrl = false,
                    Alt = false,
                    Shift = false,
                    Key = "N"
                }
            }
        });

        var result = _service.Import(_packagePath, currentConfigPath, currentLayoutPath, new AppSettings(), _ => null);

        Assert.True(result.Success, result.ErrorMessage);
        var imported = ReadJson<AppConfig>(currentConfigPath);
        Assert.True(imported.MapHotkey.Enabled);
        Assert.False(imported.MapHotkey.Ctrl);
        Assert.True(imported.MapHotkey.Alt);
        Assert.False(imported.MapHotkey.Shift);
        Assert.Equal("N", imported.MapHotkey.Key);
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

    [Fact]
    public void Import_preflights_map_before_replacing_current_workspace_files()
    {
        var currentConfigPath = Path.Combine(_workspacePath, "config.json");
        var currentLayoutPath = Path.Combine(_workspacePath, "factory-map.layout.json");
        WriteJson(currentConfigPath, new AppConfig
        {
            Shortcuts = [new ShortcutItem { Name = "旧", TargetPath = "OLD" }]
        });
        WriteJson(currentLayoutPath, new FactoryMapLayoutConfig
        {
            Version = 6,
            Devices = [new FactoryMapDeviceNode { Id = "old-node", Key = "OLD", Name = "旧", X = 100, Y = 100, Width = 160, Height = 60 }]
        });
        WritePackage(new GlobalConfigPackage
        {
            WorkspaceConfig = new AppConfig
            {
                Shortcuts = [new ShortcutItem { Name = "新", TargetPath = "A" }]
            },
            FactoryMapLayout = new FactoryMapLayoutConfig
            {
                Version = 6,
                Devices = [new FactoryMapDeviceNode { Id = "node-a", Key = "A", Name = "新", X = 100, Y = 100, Width = 160, Height = 60 }],
                ConnectionPoints =
                [
                    new FactoryMapConnectionPoint
                    {
                        Id = "node-a:right",
                        Kind = FactoryMapConnectionPointKinds.Free,
                        X = 400,
                        Y = 130
                    }
                ]
            }
        });

        var result = _service.Import(
            _packagePath,
            currentConfigPath,
            currentLayoutPath,
            new AppSettings(),
            _ => null);

        Assert.False(result.Success);
        Assert.Contains("地图布局预检失败", result.ErrorMessage);
        Assert.Equal("旧", ReadJson<AppConfig>(currentConfigPath).Shortcuts.Single().Name);
        Assert.Equal("OLD", ReadJson<FactoryMapLayoutConfig>(currentLayoutPath).Devices.Single().Key);
        Assert.Empty(Directory.EnumerateFiles(_workspacePath, "*.import-backup.*.json"));
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
