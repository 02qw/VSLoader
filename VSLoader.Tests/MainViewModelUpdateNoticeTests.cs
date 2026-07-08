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
        result.UpdatedItems.Add("全局配置");
        result.UpdatedItems.Add("软件版本");
        result.Failures.Add("全局配置包不可访问");

        viewModel.ApplyUpdateCheckResult(result);

        Assert.True(viewModel.HasUpdateNotice);
        Assert.Equal("检测到更新：全局配置、软件版本", viewModel.UpdateNoticeMessage);
        Assert.True(viewModel.HasUpdateFailure);
        Assert.Equal("更新检测失败：全局配置包不可访问", viewModel.UpdateFailureMessage);
    }

    [Fact]
    public void ApplyUpdateCheckResult_sets_software_update_notice_when_software_version_detected()
    {
        var viewModel = CreateViewModel();
        var result = new UpdateCheckResult
        {
            DetectedSoftwareVersion = "3.1.0"
        };
        result.UpdatedItems.Add("软件版本");

        viewModel.ApplyUpdateCheckResult(result);

        Assert.True(viewModel.HasSoftwareUpdateNotice);
    }

    [Fact]
    public void ApplyUpdateCheckResult_does_not_set_software_update_notice_for_global_config_only()
    {
        var viewModel = CreateViewModel();
        var result = new UpdateCheckResult();
        result.UpdatedItems.Add("全局配置");

        viewModel.ApplyUpdateCheckResult(result);

        Assert.False(viewModel.HasSoftwareUpdateNotice);
    }

    [Fact]
    public void ApplyUpdateCheckResult_keeps_existing_software_update_notice_when_check_fails()
    {
        var viewModel = CreateViewModel();
        var softwareResult = new UpdateCheckResult
        {
            DetectedSoftwareVersion = "3.1.0"
        };
        softwareResult.UpdatedItems.Add("软件版本");
        viewModel.ApplyUpdateCheckResult(softwareResult);
        var failedResult = new UpdateCheckResult();
        failedResult.Failures.Add("软件更新 manifest 不可访问");

        viewModel.ApplyUpdateCheckResult(failedResult);

        Assert.True(viewModel.HasSoftwareUpdateNotice);
    }

    [Fact]
    public void Close_update_notice_clears_software_update_notice_after_acknowledge_success()
    {
        var updateTimePath = Path.Combine(_configDirectory, "updateTime.json");
        var viewModel = CreateViewModel(updateTimePath);
        var result = new UpdateCheckResult
        {
            DetectedSoftwareVersion = "3.1.0"
        };
        result.UpdatedItems.Add("软件版本");
        viewModel.ApplyUpdateCheckResult(result);

        viewModel.CloseUpdateNoticeCommand.Execute(null);

        Assert.False(viewModel.HasSoftwareUpdateNotice);
    }

    [Fact]
    public void Close_update_notice_only_hides_update_notice()
    {
        var viewModel = CreateViewModel();
        var result = new UpdateCheckResult();
        result.UpdatedItems.Add("全局配置");
        result.DetectedGlobalConfigExportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8));
        result.DetectedGlobalConfigWriteTimeUtc = new DateTime(2026, 7, 6, 17, 0, 0, DateTimeKind.Utc);
        result.Failures.Add("软件更新 manifest 不可访问");
        viewModel.ApplyUpdateCheckResult(result);

        viewModel.CloseUpdateNoticeCommand.Execute(null);

        Assert.False(viewModel.HasUpdateNotice);
        Assert.Equal(string.Empty, viewModel.UpdateNoticeMessage);
        Assert.True(viewModel.HasUpdateFailure);
        Assert.Equal("更新检测失败：软件更新 manifest 不可访问", viewModel.UpdateFailureMessage);
    }

    [Fact]
    public void Close_update_notice_acknowledges_detected_update_baseline()
    {
        var updateTimePath = Path.Combine(_configDirectory, "updateTime.json");
        var detectedExportedAt = new DateTimeOffset(2026, 7, 7, 1, 0, 0, TimeSpan.FromHours(8));
        var detectedWriteTimeUtc = new DateTime(2026, 7, 6, 17, 0, 0, DateTimeKind.Utc);
        var viewModel = CreateViewModel(updateTimePath);
        var result = new UpdateCheckResult();
        result.UpdatedItems.Add("全局配置");
        result.DetectedGlobalConfigExportedAt = detectedExportedAt;
        result.DetectedGlobalConfigWriteTimeUtc = detectedWriteTimeUtc;
        viewModel.ApplyUpdateCheckResult(result);

        viewModel.CloseUpdateNoticeCommand.Execute(null);

        var state = JsonSerializer.Deserialize<UpdateTimeState>(File.ReadAllText(updateTimePath))!;
        Assert.False(viewModel.HasUpdateNotice);
        Assert.Equal(detectedExportedAt, state.GlobalConfig.LastUsedExportedAt);
        Assert.Equal(detectedWriteTimeUtc, state.GlobalConfig.LastSeenWriteTimeUtc);
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
