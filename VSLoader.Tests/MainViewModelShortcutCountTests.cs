using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class MainViewModelShortcutCountTests : IDisposable
{
    private readonly string _configDirectory;

    public MainViewModelShortcutCountTests()
    {
        _configDirectory = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDirectory);
    }

    [Fact]
    public void ShortcutCountText_shows_visible_count_and_total_count()
    {
        var configService = new ConfigService(_configDirectory);
        configService.Save(new AppConfig
        {
            VSCodePath = @"C:\Tools\Code.exe",
            Shortcuts =
            [
                new ShortcutItem { Name = "热贴机_001", TargetPath = @"C:\A", Description = "TSSM" },
                new ShortcutItem { Name = "热贴机_002", TargetPath = @"C:\B", Description = "TSSM" },
                new ShortcutItem { Name = "垂直炉_001", TargetPath = @"C:\C", Description = "TVF" }
            ]
        });
        var viewModel = CreateViewModel(configService, _configDirectory);

        Assert.Equal("3 / 3", viewModel.ShortcutCountText);

        viewModel.SearchText = "rt";

        Assert.Equal("2 / 3", viewModel.ShortcutCountText);
    }

    [Fact]
    public void SearchText_filters_by_source_module_name()
    {
        var configService = new ConfigService(_configDirectory);
        configService.Save(new AppConfig
        {
            VSCodePath = @"C:\Tools\Code.exe",
            Shortcuts =
            [
                new ShortcutItem { Name = "矩子3D-AOI_001", TargetPath = @"C:\A", SourceModuleName = "eap-sic-Jutze-3D-AOI" },
                new ShortcutItem { Name = "热贴机_001", TargetPath = @"C:\B", SourceModuleName = "eap-sic-SiliCool-HotBonder" }
            ]
        });
        var viewModel = CreateViewModel(configService, _configDirectory);

        viewModel.SearchText = "Jutze";

        Assert.Equal("1 / 2", viewModel.ShortcutCountText);
    }

    [Fact]
    public void SearchText_refreshes_the_view_without_raising_shortcuts_changed()
    {
        var configService = new ConfigService(_configDirectory);
        configService.Save(new AppConfig
        {
            VSCodePath = @"C:\Tools\Code.exe",
            Shortcuts =
            [
                new ShortcutItem { Name = "热贴机_001", TargetPath = @"C:\A" },
                new ShortcutItem { Name = "垂直炉_001", TargetPath = @"C:\B" }
            ]
        });
        var viewModel = CreateViewModel(configService, _configDirectory);
        var shortcutsChangedCount = 0;
        viewModel.ShortcutsChanged += (_, _) => shortcutsChangedCount++;

        viewModel.SearchText = "不存在";

        Assert.Equal("0 / 2", viewModel.ShortcutCountText);
        Assert.Equal(0, shortcutsChangedCount);
    }

    [Fact]
    public void ApplySort_refreshes_the_view_without_raising_shortcuts_changed()
    {
        var configService = new ConfigService(_configDirectory);
        configService.Save(new AppConfig
        {
            VSCodePath = @"C:\Tools\Code.exe",
            Shortcuts =
            [
                new ShortcutItem { Name = "B", TargetPath = @"C:\B" },
                new ShortcutItem { Name = "A", TargetPath = @"C:\A" }
            ]
        });
        var viewModel = CreateViewModel(configService, _configDirectory);
        var shortcutsChangedCount = 0;
        viewModel.ShortcutsChanged += (_, _) => shortcutsChangedCount++;

        viewModel.ApplySort(ShortcutSortField.UpdatedAt);

        Assert.Equal(0, shortcutsChangedCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDirectory))
        {
            Directory.Delete(_configDirectory, true);
        }
    }

    private static MainViewModel CreateViewModel(ConfigService configService, string configDirectory)
    {
        return new MainViewModel(
            new AppSettings { VSCodePath = @"C:\Tools\Code.exe" },
            new AppSettingsService(configDirectory),
            configService,
            new VSCodeLauncherService(),
            new DialogService(),
            new BatchImportService(),
            new AdminUiService(),
            new WebUiService(),
            new ShortcutSearchService(),
            new PasswordProtectionService(),
            new ClipboardService());
    }
}
