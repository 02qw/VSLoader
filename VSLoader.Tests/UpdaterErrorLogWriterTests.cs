using VSLoader.Updater.Services;

namespace VSLoader.Tests;

public sealed class UpdaterErrorLogWriterTests : IDisposable
{
    private readonly string _rootPath;

    public UpdaterErrorLogWriterTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void WriteStartupError_creates_log_with_source_args_and_exception()
    {
        var writer = new UpdaterErrorLogWriter(_rootPath);

        var logPath = writer.WriteStartupError(["--targetDir", "C:\\App"], new InvalidOperationException("boom"));

        Assert.True(File.Exists(logPath));
        var content = File.ReadAllText(logPath);
        Assert.Contains("Source: VSLoader.Updater startup", content, StringComparison.Ordinal);
        Assert.Contains("--targetDir", content, StringComparison.Ordinal);
        Assert.Contains("C:\\App", content, StringComparison.Ordinal);
        Assert.Contains("boom", content, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
