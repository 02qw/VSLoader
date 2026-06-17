using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;
using System.Text.Json;

namespace VSLoader.Tests;

public sealed class MainViewModelUpdateNoticeTests : IDisposable
{
    private readonly string _configDirectory;

    public MainViewModelUpdateNoticeTests()
    {
        _configDirectory = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDirectory);
    }

    [Fact]
    public void ApplyUpdateCheckResult_shows_update_notice_and_failure_independently()
    {
        var viewModel = CreateViewModel();
        var result = new UpdateCheckResult();
        result.UpdatedItems.Add("批量规则文件");
        result.UpdatedItems.Add("软件版本");
        result.Failures.Add("rules 文件不存在");

        viewModel.ApplyUpdateCheckResult(result);

        Assert.True(viewModel.HasUpdateNotice);
        Assert.Equal("检测到更新：批量规则文件、软件版本", viewModel.UpdateNoticeMessage);
        Assert.True(viewModel.HasUpdateFailure);
        Assert.Equal("更新检测失败：rules 文件不存在", viewModel.UpdateFailureMessage);
    }

    [Fact]
    public void Close_update_notice_only_hides_update_notice()
    {
        var viewModel = CreateViewModel();
        var result = new UpdateCheckResult();
        result.UpdatedItems.Add("地图配置文件");
        result.DetectedMapWriteTimeUtc = new DateTime(2026, 6, 1, 2, 0, 0, DateTimeKind.Utc);
        result.Failures.Add("软件版本文件不存在");
        viewModel.ApplyUpdateCheckResult(result);

        viewModel.CloseUpdateNoticeCommand.Execute(null);

        Assert.False(viewModel.HasUpdateNotice);
        Assert.Equal(string.Empty, viewModel.UpdateNoticeMessage);
        Assert.True(viewModel.HasUpdateFailure);
        Assert.Equal("更新检测失败：软件版本文件不存在", viewModel.UpdateFailureMessage);
    }

    [Fact]
    public void Close_update_notice_acknowledges_detected_update_baseline()
    {
        var updateTimePath = Path.Combine(_configDirectory, "updateTime.json");
        var detectedMapTime = new DateTime(2026, 6, 1, 2, 0, 0, DateTimeKind.Utc);
        var viewModel = CreateViewModel(updateTimePath);
        var result = new UpdateCheckResult();
        result.UpdatedItems.Add("地图配置文件");
        result.DetectedMapWriteTimeUtc = detectedMapTime;
        viewModel.ApplyUpdateCheckResult(result);

        viewModel.CloseUpdateNoticeCommand.Execute(null);

        var state = JsonSerializer.Deserialize<UpdateTimeState>(File.ReadAllText(updateTimePath))!;
        Assert.False(viewModel.HasUpdateNotice);
        Assert.Equal(detectedMapTime, state.Map.LastUsedWriteTimeUtc);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDirectory))
        {
            Directory.Delete(_configDirectory, true);
        }
    }

    private MainViewModel CreateViewModel(string? updateTimePath = null)
    {
        var configService = new ConfigService(_configDirectory);
        configService.Save(new AppConfig());
        return new MainViewModel(
            new AppSettings { VSCodePath = @"C:\Tools\Code.exe" },
            new AppSettingsService(_configDirectory),
            configService,
            new VSCodeLauncherService(),
            new DialogService(),
            new BatchImportService(),
            new AdminUiService(),
            new WebUiService(),
            new ShortcutSearchService(),
            new PasswordProtectionService(),
            new ClipboardService(),
            updateTimePath: updateTimePath);
    }
}
