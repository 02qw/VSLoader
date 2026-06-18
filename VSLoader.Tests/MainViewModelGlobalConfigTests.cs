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
        Assert.Equal("导出项", package.WorkspaceConfig.Shortcuts.Single().Name);
        Assert.Contains("全局配置导出完成", dialogService.LastInfoMessage);
    }

    private MainViewModel CreateViewModel(
        AppSettings appSettings,
        RecordingDialogService dialogService,
        string layoutPath,
        ConfigService? configService = null)
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
