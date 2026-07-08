using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class MainViewModelUpdateCheckLoopStopTests : IDisposable
{
    private readonly string _rootPath;

    public MainViewModelUpdateCheckLoopStopTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task StopUpdateCheckLoopAsync_is_safe_when_loop_was_not_started()
    {
        var viewModel = CreateViewModel();

        await viewModel.StopUpdateCheckLoopAsync(TimeSpan.FromMilliseconds(50));

        Assert.False(viewModel.IsUpdateCheckLoopRunning);
    }

    [Fact]
    public async Task StopUpdateCheckLoopAsync_cancels_running_loop()
    {
        var viewModel = CreateViewModel();

        viewModel.StartUpdateCheckLoop();
        await viewModel.StopUpdateCheckLoopAsync(TimeSpan.FromSeconds(3));

        Assert.False(viewModel.IsUpdateCheckLoopRunning);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private MainViewModel CreateViewModel()
    {
        var configService = new ConfigService(_rootPath);
        configService.Save(new AppConfig());
        return new MainViewModel(
            new AppSettings(),
            new AppSettingsService(_rootPath),
            configService,
            new VSCodeLauncherService(),
            new DialogService(),
            new BatchImportService(),
            new AdminUiService(),
            new WebUiService(),
            new ShortcutSearchService(),
            new PasswordProtectionService(),
            new ClipboardService(),
            updateTimePath: Path.Combine(_rootPath, "updateTime.json"),
            softwareUpdatesRoot: Path.Combine(_rootPath, "Updates"),
            factoryMapLayoutPath: Path.Combine(_rootPath, "factory-map.layout.json"));
    }
}
