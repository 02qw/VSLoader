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

    [Fact]
    public void WriteStartupError_uses_single_log_file_and_keeps_latest_2000_lines()
    {
        var writer = new UpdaterErrorLogWriter(_rootPath);
        var logPath = Path.Combine(_rootPath, "updater-error.log");
        File.WriteAllLines(logPath, Enumerable.Range(1, 1999).Select(index => $"old-{index:0000}"));

        var writtenPath = writer.WriteStartupError(["--targetDir", "C:\\App"], new InvalidOperationException("boom"));

        Assert.Equal(logPath, writtenPath);
        Assert.Single(Directory.GetFiles(_rootPath, "*.log"));
        var lines = File.ReadAllLines(logPath);
        Assert.Equal(2000, lines.Length);
        Assert.DoesNotContain(lines, line => line.Contains("old-0001", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("old-1999", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Source: VSLoader.Updater startup", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
