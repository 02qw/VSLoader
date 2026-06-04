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
        var viewModel = CreateViewModel(configService);

        Assert.Equal("3 / 3", viewModel.ShortcutCountText);

        viewModel.SearchText = "rt";

        Assert.Equal("2 / 3", viewModel.ShortcutCountText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDirectory))
        {
            Directory.Delete(_configDirectory, true);
        }
    }

    private static MainViewModel CreateViewModel(ConfigService configService)
    {
        return new MainViewModel(
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
