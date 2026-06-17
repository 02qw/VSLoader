using VSLoader.Updater.Services;

namespace VSLoader.Tests;

public sealed class UpdaterApplyServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _targetDir;
    private readonly string _stagingDir;
    private readonly string _updatesRoot;
    private readonly string _errorLogRoot;

    public UpdaterApplyServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        _targetDir = Path.Combine(_rootPath, "target");
        _stagingDir = Path.Combine(_rootPath, "staging");
        _updatesRoot = Path.Combine(_rootPath, "Updates");
        _errorLogRoot = Path.Combine(_rootPath, "errorLog");
        Directory.CreateDirectory(_targetDir);
        Directory.CreateDirectory(_stagingDir);
        Directory.CreateDirectory(Path.Combine(_updatesRoot, "download"));
        Directory.CreateDirectory(Path.Combine(_updatesRoot, "staging"));
    }

    [Fact]
    public void Apply_copies_staging_files_to_target_and_creates_backup()
    {
        File.WriteAllText(Path.Combine(_targetDir, "VSLoader.exe"), "old");
        File.WriteAllText(Path.Combine(_stagingDir, "VSLoader.exe"), "new");
        File.WriteAllText(Path.Combine(_stagingDir, "VSLoader.dll"), "new dll");
        var service = new UpdaterApplyService(errorLogRoot: _errorLogRoot);

        var result = service.Apply(CreateOptions());

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_targetDir, "VSLoader.exe")));
        Assert.Equal("new dll", File.ReadAllText(Path.Combine(_targetDir, "VSLoader.dll")));
        Assert.Single(Directory.GetDirectories(Path.Combine(_updatesRoot, "backup")));
    }

    [Fact]
    public void Apply_cleans_download_and_staging_on_success()
    {
        File.WriteAllText(Path.Combine(_targetDir, "VSLoader.exe"), "old");
        File.WriteAllText(Path.Combine(_stagingDir, "VSLoader.exe"), "new");
        var service = new UpdaterApplyService(errorLogRoot: _errorLogRoot);

        var result = service.Apply(CreateOptions());

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(Directory.Exists(Path.Combine(_updatesRoot, "download")));
        Assert.False(Directory.Exists(Path.Combine(_updatesRoot, "staging")));
    }

    [Fact]
    public void Apply_does_not_clean_runner_on_success()
    {
        var runnerDirectory = Path.Combine(_updatesRoot, "runner");
        Directory.CreateDirectory(runnerDirectory);
        File.WriteAllText(Path.Combine(runnerDirectory, "VSLoader.Updater.exe"), "runner");
        File.WriteAllText(Path.Combine(_targetDir, "VSLoader.exe"), "old");
        File.WriteAllText(Path.Combine(_stagingDir, "VSLoader.exe"), "new");
        var service = new UpdaterApplyService(errorLogRoot: _errorLogRoot);

        var result = service.Apply(CreateOptions());

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(runnerDirectory, "VSLoader.Updater.exe")));
    }

    [Fact]
    public void Apply_keeps_only_latest_backup_on_success()
    {
        Directory.CreateDirectory(Path.Combine(_updatesRoot, "backup", "20260101_000000_000"));
        Directory.CreateDirectory(Path.Combine(_updatesRoot, "backup", "20260102_000000_000"));
        File.WriteAllText(Path.Combine(_targetDir, "VSLoader.exe"), "old");
        File.WriteAllText(Path.Combine(_stagingDir, "VSLoader.exe"), "new");
        var service = new UpdaterApplyService(errorLogRoot: _errorLogRoot);

        var result = service.Apply(CreateOptions());

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Single(Directory.GetDirectories(Path.Combine(_updatesRoot, "backup")));
    }

    [Fact]
    public void Apply_rolls_back_and_writes_error_log_when_copy_fails()
    {
        File.WriteAllText(Path.Combine(_targetDir, "VSLoader.exe"), "old");
        File.WriteAllText(Path.Combine(_stagingDir, "VSLoader.exe"), "new");
        File.WriteAllText(Path.Combine(_stagingDir, "fail.dll"), "boom");
        var service = new UpdaterApplyService(
            errorLogRoot: _errorLogRoot,
            shouldFailCopy: path => Path.GetFileName(path).Equals("fail.dll", StringComparison.OrdinalIgnoreCase));

        var result = service.Apply(CreateOptions());

        Assert.False(result.Success);
        Assert.True(result.RollbackSucceeded);
        Assert.Equal("old", File.ReadAllText(Path.Combine(_targetDir, "VSLoader.exe")));
        Assert.Contains(Directory.GetFiles(_errorLogRoot), path => Path.GetFileName(path).EndsWith(".log", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_skips_updater_self_files_when_copying_to_target()
    {
        File.WriteAllText(Path.Combine(_targetDir, "VSLoader.exe"), "old");
        File.WriteAllText(Path.Combine(_targetDir, "VSLoader.Updater.exe"), "old updater");
        File.WriteAllText(Path.Combine(_stagingDir, "VSLoader.exe"), "new");
        File.WriteAllText(Path.Combine(_stagingDir, "VSLoader.Updater.exe"), "new updater");
        var service = new UpdaterApplyService(errorLogRoot: _errorLogRoot);

        var result = service.Apply(CreateOptions());

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_targetDir, "VSLoader.exe")));
        Assert.Equal("old updater", File.ReadAllText(Path.Combine(_targetDir, "VSLoader.Updater.exe")));
    }

    private UpdaterOptions CreateOptions()
    {
        return new UpdaterOptions
        {
            ProcessId = 123,
            TargetDirectory = _targetDir,
            StagingDirectory = _stagingDir,
            MainExeName = "VSLoader.exe",
            UpdatesRoot = _updatesRoot
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
