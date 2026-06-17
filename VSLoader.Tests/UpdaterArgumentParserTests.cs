using VSLoader.Updater.Services;

namespace VSLoader.Tests;

public sealed class UpdaterArgumentParserTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _targetDir;
    private readonly string _stagingDir;
    private readonly string _updatesRoot;

    public UpdaterArgumentParserTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        _targetDir = Path.Combine(_rootPath, "target");
        _stagingDir = Path.Combine(_rootPath, "staging");
        _updatesRoot = Path.Combine(_rootPath, "updates");
        Directory.CreateDirectory(_targetDir);
        Directory.CreateDirectory(_stagingDir);
        File.WriteAllText(Path.Combine(_stagingDir, "VSLoader.exe"), "main");
    }

    [Fact]
    public void Parse_returns_failure_when_process_id_is_missing()
    {
        var result = UpdaterArgumentParser.Parse([]);

        Assert.False(result.Success);
        Assert.Contains("processId 无效", result.ErrorMessage);
    }

    [Fact]
    public void Parse_returns_failure_when_target_directory_missing()
    {
        var args = CreateArgs(targetDir: Path.Combine(_rootPath, "missing"));

        var result = UpdaterArgumentParser.Parse(args);

        Assert.False(result.Success);
        Assert.Contains("targetDir 不存在", result.ErrorMessage);
    }

    [Fact]
    public void Parse_returns_failure_when_staging_directory_missing()
    {
        var args = CreateArgs(stagingDir: Path.Combine(_rootPath, "missing-staging"));

        var result = UpdaterArgumentParser.Parse(args);

        Assert.False(result.Success);
        Assert.Contains("stagingDir 不存在", result.ErrorMessage);
    }

    [Fact]
    public void Parse_returns_failure_when_staging_missing_main_exe()
    {
        File.Delete(Path.Combine(_stagingDir, "VSLoader.exe"));
        var args = CreateArgs();

        var result = UpdaterArgumentParser.Parse(args);

        Assert.False(result.Success);
        Assert.Contains("stagingDir 缺少 VSLoader.exe", result.ErrorMessage);
    }

    [Fact]
    public void Parse_returns_options_when_arguments_are_valid()
    {
        var args = CreateArgs();

        var result = UpdaterArgumentParser.Parse(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Options);
        Assert.Equal(123, result.Options.ProcessId);
        Assert.Equal(_targetDir, result.Options.TargetDirectory);
        Assert.Equal(_stagingDir, result.Options.StagingDirectory);
        Assert.Equal("VSLoader.exe", result.Options.MainExeName);
        Assert.Equal(_updatesRoot, result.Options.UpdatesRoot);
    }

    [Fact]
    public void Parse_returns_update_options_when_mode_is_update()
    {
        var manifestPath = Path.Combine(_rootPath, "manifest.json");
        File.WriteAllText(manifestPath, "{}");
        var args = CreateUpdateArgs(manifestPath: manifestPath, currentVersion: "2.1.0");

        var result = UpdaterArgumentParser.Parse(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("update", result.Options!.Mode);
        Assert.Equal(manifestPath, result.Options.ManifestPath);
        Assert.Equal(new Version(2, 1, 0), result.Options.CurrentVersion);
        Assert.Equal(_targetDir, result.Options.TargetDirectory);
        Assert.Equal(_updatesRoot, result.Options.UpdatesRoot);
    }

    [Fact]
    public void Parse_returns_failure_when_update_mode_manifest_path_is_missing()
    {
        var args = CreateUpdateArgs(manifestPath: string.Empty, currentVersion: "2.1.0");

        var result = UpdaterArgumentParser.Parse(args);

        Assert.False(result.Success);
        Assert.Contains("manifestPath 无效", result.ErrorMessage);
    }

    [Fact]
    public void Parse_returns_failure_when_update_mode_current_version_is_invalid()
    {
        var manifestPath = Path.Combine(_rootPath, "manifest.json");
        File.WriteAllText(manifestPath, "{}");
        var args = CreateUpdateArgs(manifestPath: manifestPath, currentVersion: "bad");

        var result = UpdaterArgumentParser.Parse(args);

        Assert.False(result.Success);
        Assert.Contains("currentVersion 无效", result.ErrorMessage);
    }

    private string[] CreateArgs(string? targetDir = null, string? stagingDir = null)
    {
        return
        [
            "--processId", "123",
            "--targetDir", targetDir ?? _targetDir,
            "--stagingDir", stagingDir ?? _stagingDir,
            "--mainExeName", "VSLoader.exe",
            "--updatesRoot", _updatesRoot
        ];
    }

    private string[] CreateUpdateArgs(string manifestPath, string currentVersion)
    {
        return
        [
            "--mode", "update",
            "--processId", "123",
            "--targetDir", _targetDir,
            "--mainExeName", "VSLoader.exe",
            "--manifestPath", manifestPath,
            "--currentVersion", currentVersion,
            "--updatesRoot", _updatesRoot
        ];
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
