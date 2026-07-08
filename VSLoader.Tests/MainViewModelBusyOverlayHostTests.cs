using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class MainViewModelBusyOverlayHostTests : IDisposable
{
    private readonly string _rootPath;

    public MainViewModelBusyOverlayHostTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void Busy_overlay_is_visible_on_main_host_by_default()
    {
        var viewModel = CreateViewModel();

        viewModel.IsBusy = true;

        Assert.True(viewModel.IsMainBusyOverlayVisible);
        Assert.False(viewModel.IsMapBusyOverlayVisible);
    }

    [Fact]
    public void Busy_overlay_can_be_moved_to_map_host()
    {
        var viewModel = CreateViewModel();

        viewModel.BusyOverlayHost = BusyOverlayHost.Map;
        viewModel.IsBusy = true;

        Assert.False(viewModel.IsMainBusyOverlayVisible);
        Assert.True(viewModel.IsMapBusyOverlayVisible);
    }

    [Fact]
    public void Busy_overlay_is_hidden_when_not_busy()
    {
        var viewModel = CreateViewModel();

        viewModel.BusyOverlayHost = BusyOverlayHost.Map;
        viewModel.IsBusy = false;

        Assert.False(viewModel.IsMainBusyOverlayVisible);
        Assert.False(viewModel.IsMapBusyOverlayVisible);
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
        return new MainViewModel(
            new AppSettings(),
            new AppSettingsService(_rootPath),
            new ConfigService(_rootPath),
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
