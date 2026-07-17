using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class MainViewModelGlobalConfigTests : IDisposable
{
    private readonly string _rootPath;

    public MainViewModelGlobalConfigTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task ImportGlobalConfigCommand_imports_package_and_refreshes_shortcuts()
    {
        var configPath = Path.Combine(_rootPath, "config.json");
        var layoutPath = Path.Combine(_rootPath, "factory-map.layout.json");
        var packagePath = Path.Combine(_rootPath, "package.json");
        WriteJson(configPath, new AppConfig
        {
            Shortcuts = [new ShortcutItem { Name = "旧", TargetPath = @"C:\Old" }]
        });
        WriteJson(packagePath, new GlobalConfigPackage
        {
            WorkspaceConfig = new AppConfig
            {
                Shortcuts = [new ShortcutItem { Name = "新", TargetPath = @"C:\New" }]
            },
            FactoryMapLayout = new FactoryMapLayoutConfig()
        });
        var dialogService = new RecordingDialogService { SelectedJsonFile = packagePath };
        var appSettings = new AppSettings { VSCodePath = @"C:\Invalid\Code.exe" };
        var viewModel = CreateViewModel(appSettings, dialogService, layoutPath);

        await viewModel.ImportGlobalConfigCommand.ExecuteAsync(null);

        Assert.Equal("新", viewModel.Shortcuts.Single().Name);
        Assert.True(File.Exists(layoutPath));
        Assert.Contains("全局配置导入完成", dialogService.LastInfoMessage);
        Assert.True(File.Exists(Path.Combine(_rootPath, "app-settings.json")));
    }

    [Fact]
    public void ImportGlobalConfigCommand_uses_main_overlay_background_io_and_live_progress()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "ViewModels",
            "MainViewModel.cs"));
        var methodStart = code.IndexOf("private async Task ImportGlobalConfigAsync()", StringComparison.Ordinal);
        var methodEnd = code.IndexOf("private string BuildUpdaterArguments", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);
        var method = code[methodStart..methodEnd];
        Assert.Contains("BusyOverlayHost = BusyOverlayHost.Main;", method);
        Assert.Contains("await Task.Yield();", method);
        Assert.Contains("new Progress<GlobalConfigOperationProgress>", method);
        Assert.Contains("await Task.Run(() =>", method);
        Assert.Contains("BusyProgressValue = progress.Value;", method);
        Assert.Contains("BusyProgressText = progress.Message;", method);
        Assert.Contains("BusyCurrentItemText = progress.CurrentItem;", method);

        var clearBusyIndex = method.LastIndexOf("ClearBusyState();", StringComparison.Ordinal);
        var showInfoIndex = method.LastIndexOf("_dialogService.ShowInfo", StringComparison.Ordinal);
        Assert.True(clearBusyIndex >= 0);
        Assert.True(showInfoIndex > clearBusyIndex);
    }

    [Fact]
    public void Global_config_import_service_reports_ordered_operation_stages()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Models",
            "Services",
            "GlobalConfigPackageService.cs"));

        Assert.Contains("IProgress<GlobalConfigOperationProgress>? progress = null", code);
        Assert.Contains("ReportProgress(progress, 10, \"正在读取全局配置包...\"", code);
        Assert.Contains("ReportProgress(progress, 30, \"正在校验工作区配置...\"", code);
        Assert.Contains("ReportProgress(progress, 50, \"正在写入工作区配置...\"", code);
        Assert.Contains("ReportProgress(progress, 65, \"正在导入地图布局...\"", code);
        Assert.Contains("ReportProgress(progress, 78, \"正在应用界面偏好...\"", code);
        Assert.Contains("ReportProgress(progress, 88, \"正在校验本机路径...\"", code);
    }

    [Fact]
    public async Task ExportGlobalConfigCommand_uses_save_dialog_and_writes_package()
    {
        var exportPath = Path.Combine(_rootPath, "export.json");
        var layoutPath = Path.Combine(_rootPath, "factory-map.layout.json");
        var dialogService = new RecordingDialogService { SavedJsonFile = exportPath };
        var configService = new ConfigService(_rootPath);
        configService.Save(new AppConfig
        {
            Shortcuts = [new ShortcutItem { Name = "导出项", TargetPath = @"C:\A" }]
        });
        var viewModel = CreateViewModel(new AppSettings { VSCodePath = @"C:\Tools\Code.exe" }, dialogService, layoutPath, configService);

        await viewModel.ExportGlobalConfigCommand.ExecuteAsync(null);

        var package = JsonSerializer.Deserialize<GlobalConfigPackage>(File.ReadAllText(exportPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal("导出项", package.Workspace!.Settings!.Shortcuts.Single().Name);
        Assert.Contains("全局配置导出完成", dialogService.LastInfoMessage);
    }

    [Fact]
    public async Task ExportGlobalConfigCommand_writes_map_hotkey_to_package()
    {
        var exportPath = Path.Combine(_rootPath, "export-map-hotkey.json");
        var layoutPath = Path.Combine(_rootPath, "factory-map.layout.json");
        var dialogService = new RecordingDialogService { SavedJsonFile = exportPath };
        var configService = new ConfigService(_rootPath);
        configService.Save(new AppConfig
        {
            MapHotkey = new MapHotkeyConfig
            {
                Enabled = true,
                Ctrl = true,
                Alt = true,
                Shift = false,
                Key = "K"
            }
        });
        var viewModel = CreateViewModel(new AppSettings(), dialogService, layoutPath, configService);

        await viewModel.ExportGlobalConfigCommand.ExecuteAsync(null);

        var package = JsonSerializer.Deserialize<GlobalConfigPackage>(File.ReadAllText(exportPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.True(package.Workspace!.Settings!.MapHotkey.Enabled);
        Assert.True(package.Workspace.Settings.MapHotkey.Ctrl);
        Assert.True(package.Workspace.Settings.MapHotkey.Alt);
        Assert.False(package.Workspace.Settings.MapHotkey.Shift);
        Assert.Equal("K", package.Workspace.Settings.MapHotkey.Key);
    }

    [Fact]
    public async Task ExportGlobalConfigCommand_marks_exported_global_config_as_used_when_export_path_matches_configured_path()
    {
        var exportPath = Path.Combine(_rootPath, "VSLoader_GlobalConfig.json");
        var layoutPath = Path.Combine(_rootPath, "factory-map.layout.json");
        var updateTimePath = Path.Combine(_rootPath, "updateTime.json");
        var dialogService = new RecordingDialogService { SavedJsonFile = exportPath };
        var configService = new ConfigService(_rootPath);
        configService.Save(new AppConfig
        {
            UpdateCheck = new UpdateCheckConfig
            {
                GlobalConfigPackagePath = exportPath
            }
        });
        var viewModel = CreateViewModel(new AppSettings(), dialogService, layoutPath, configService, updateTimePath);

        await viewModel.ExportGlobalConfigCommand.ExecuteAsync(null);

        var package = JsonSerializer.Deserialize<GlobalConfigPackage>(File.ReadAllText(exportPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var state = JsonSerializer.Deserialize<UpdateTimeState>(File.ReadAllText(updateTimePath))!;
        Assert.Equal(DateTimeOffset.Parse(package.ExportedAt), state.GlobalConfig.LastUsedExportedAt);
        Assert.Equal(File.GetLastWriteTimeUtc(exportPath), state.GlobalConfig.LastSeenWriteTimeUtc);
    }

    [Fact]
    public async Task ImportGlobalConfigCommand_registers_main_and_map_hotkeys()
    {
        var layoutPath = Path.Combine(_rootPath, "factory-map.layout.json");
        var packagePath = Path.Combine(_rootPath, "package-hotkeys.json");
        WriteJson(packagePath, new GlobalConfigPackage
        {
            WorkspaceConfig = new AppConfig
            {
                Hotkey = new HotkeyConfig { Enabled = true, InputType = "Keyboard", Alt = true, Key = "V" },
                MapHotkey = new MapHotkeyConfig { Enabled = true, Alt = true, Key = "X" }
            },
            FactoryMapLayout = new FactoryMapLayoutConfig()
        });
        var dialogService = new RecordingDialogService { SelectedJsonFile = packagePath };
        var viewModel = CreateViewModel(new AppSettings(), dialogService, layoutPath);
        HotkeyConfig? registeredMain = null;
        MapHotkeyConfig? registeredMap = null;
        viewModel.TryRegisterHotkeys = (main, map) =>
        {
            registeredMain = main.Clone();
            registeredMap = map.Clone();
            return SaveResult.Ok();
        };

        await viewModel.ImportGlobalConfigCommand.ExecuteAsync(null);

        Assert.NotNull(registeredMain);
        Assert.NotNull(registeredMap);
        Assert.True(registeredMain!.Alt);
        Assert.Equal("V", registeredMain.Key);
        Assert.True(registeredMap!.Alt);
        Assert.Equal("X", registeredMap.Key);
    }

    [Fact]
    public async Task ImportGlobalConfigCommand_reports_hotkey_registration_failure_as_warning()
    {
        var layoutPath = Path.Combine(_rootPath, "factory-map.layout.json");
        var packagePath = Path.Combine(_rootPath, "package-hotkey-conflict.json");
        WriteJson(packagePath, new GlobalConfigPackage
        {
            WorkspaceConfig = new AppConfig
            {
                Hotkey = new HotkeyConfig { Enabled = true, InputType = "Keyboard", Alt = true, Key = "X" },
                MapHotkey = new MapHotkeyConfig { Enabled = true, Alt = true, Key = "X" }
            },
            FactoryMapLayout = new FactoryMapLayoutConfig()
        });
        var dialogService = new RecordingDialogService { SelectedJsonFile = packagePath };
        var viewModel = CreateViewModel(new AppSettings(), dialogService, layoutPath);
        viewModel.TryRegisterHotkeys = (_, _) => SaveResult.Fail("主程序快捷键和地图快捷键不能相同。");

        await viewModel.ImportGlobalConfigCommand.ExecuteAsync(null);

        Assert.Contains("全局配置导入完成，但存在需要检查的问题", dialogService.LastInfoMessage);
        Assert.Contains("快捷键注册失败", dialogService.LastInfoMessage);
        Assert.Contains("不能相同", dialogService.LastInfoMessage);
    }

    [Fact]
    public async Task ImportGlobalConfigCommand_marks_imported_global_config_as_used()
    {
        var layoutPath = Path.Combine(_rootPath, "factory-map.layout.json");
        var updateTimePath = Path.Combine(_rootPath, "updateTime.json");
        var packagePath = Path.Combine(_rootPath, "package-baseline.json");
        var exportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8));
        WriteJson(packagePath, new GlobalConfigPackage
        {
            ExportedAt = exportedAt.ToString("O"),
            WorkspaceConfig = new AppConfig(),
            FactoryMapLayout = new FactoryMapLayoutConfig()
        });
        var packageWriteTimeUtc = new DateTime(2026, 7, 6, 17, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(packagePath, packageWriteTimeUtc);
        var dialogService = new RecordingDialogService { SelectedJsonFile = packagePath };
        var viewModel = CreateViewModel(new AppSettings(), dialogService, layoutPath, updateTimePath: updateTimePath);

        await viewModel.ImportGlobalConfigCommand.ExecuteAsync(null);

        var state = JsonSerializer.Deserialize<UpdateTimeState>(File.ReadAllText(updateTimePath))!;
        Assert.Equal(exportedAt, state.GlobalConfig.LastUsedExportedAt);
        Assert.Equal(packageWriteTimeUtc, state.GlobalConfig.LastSeenWriteTimeUtc);
    }

    private MainViewModel CreateViewModel(
        AppSettings appSettings,
        RecordingDialogService dialogService,
        string layoutPath,
        ConfigService? configService = null,
        string? updateTimePath = null)
    {
        configService ??= new ConfigService(_rootPath);
        return new MainViewModel(
            appSettings,
            new AppSettingsService(_rootPath),
            configService,
            new VSCodeLauncherService(),
            dialogService,
            new BatchImportService(),
            new AdminUiService(),
            new WebUiService(),
            new ShortcutSearchService(),
            new PasswordProtectionService(),
            new ClipboardService(),
            updaterRunnerService: null,
            updateTimePath: updateTimePath,
            factoryMapLayoutPath: layoutPath);
    }

    private static void WriteJson<T>(string path, T value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    private sealed class RecordingDialogService : DialogService
    {
        public string? SelectedJsonFile { get; set; }

        public string? SavedJsonFile { get; set; }

        public string LastInfoMessage { get; private set; } = string.Empty;

        public override string? SelectJsonFile()
        {
            return SelectedJsonFile;
        }

        public override string? SaveJsonFile(string defaultFileName)
        {
            return SavedJsonFile;
        }

        public override void ShowInfo(string message)
        {
            LastInfoMessage = message;
        }

        public override void ShowError(string message)
        {
            throw new InvalidOperationException(message);
        }
    }
}
