using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class MainWindowUpdateBaselineScopeTests
{
    [Fact]
    public void Program_update_baseline_uses_local_application_data()
    {
        var expectedDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VSLoader");

        Assert.Equal(
            Path.Combine(expectedDirectory, "updateTime.json"),
            UpdateTimePathService.GlobalUpdateTimePath);
    }

    [Fact]
    public void Main_window_uses_shared_baseline_and_migrates_registered_workspaces()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.Contains("_updateTimePath = UpdateTimePathService.GlobalUpdateTimePath;", code);
        Assert.Contains("MigrateLegacyUpdateTimeFiles();", code);
        Assert.Contains("_appSettings.Workspaces", code);
        Assert.Contains("_updateCheckService.MigrateLegacyUpdateTimeFiles(_updateTimePath, legacySources);", code);
        Assert.Contains("_updateCheckService,\n            _updateTimePath,", code.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.DoesNotContain("new UpdateCheckService(),\n            _workspaceContext.UpdateTimePath,", code.Replace("\r\n", "\n", StringComparison.Ordinal));
    }
}
