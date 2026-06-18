using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class UpdaterRunnerServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _sourceDirectory;
    private readonly string _runnerDirectory;

    public UpdaterRunnerServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        _sourceDirectory = Path.Combine(_rootPath, "source");
        _runnerDirectory = Path.Combine(_rootPath, "runner");
        Directory.CreateDirectory(_sourceDirectory);
        Directory.CreateDirectory(_runnerDirectory);
    }

    [Fact]
    public void Prepare_copies_complete_directory_and_returns_runner_updater_path()
    {
        File.WriteAllText(Path.Combine(_sourceDirectory, "VSLoader.exe"), "main");
        File.WriteAllText(Path.Combine(_sourceDirectory, "VSLoader.Updater.exe"), "updater");
        Directory.CreateDirectory(Path.Combine(_sourceDirectory, "Assets"));
        File.WriteAllText(Path.Combine(_sourceDirectory, "Assets", "tomato.ico"), "icon");
        var service = new UpdaterRunnerService(_runnerDirectory);

        var result = service.Prepare(_sourceDirectory);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_runnerDirectory, "VSLoader.Updater.exe"), result.RunnerUpdaterPath);
        Assert.True(File.Exists(Path.Combine(_runnerDirectory, "VSLoader.exe")));
        Assert.True(File.Exists(Path.Combine(_runnerDirectory, "VSLoader.Updater.exe")));
        Assert.True(File.Exists(Path.Combine(_runnerDirectory, "Assets", "tomato.ico")));
    }

    [Fact]
    public void Prepare_cleans_existing_runner_before_copying()
    {
        File.WriteAllText(Path.Combine(_sourceDirectory, "VSLoader.Updater.exe"), "updater");
        File.WriteAllText(Path.Combine(_runnerDirectory, "old.txt"), "old");
        var service = new UpdaterRunnerService(_runnerDirectory);

        var result = service.Prepare(_sourceDirectory);

        Assert.True(result.Success);
        Assert.False(File.Exists(Path.Combine(_runnerDirectory, "old.txt")));
    }

    [Fact]
    public void Prepare_fails_when_source_updater_is_missing()
    {
        var service = new UpdaterRunnerService(_runnerDirectory);

        var result = service.Prepare(_sourceDirectory);

        Assert.False(result.Success);
        Assert.Contains("缺少 VSLoader.Updater.exe", result.ErrorMessage);
        Assert.Equal(string.Empty, result.RunnerUpdaterPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
