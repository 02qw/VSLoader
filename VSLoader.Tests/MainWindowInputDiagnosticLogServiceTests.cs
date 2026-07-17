namespace VSLoader.Tests;

public sealed class MainWindowInputDiagnosticLogServiceTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "VSLoader.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Log_keeps_only_the_latest_entries_and_includes_event_context()
    {
        var service = new VSLoader.Services.MainWindowInputDiagnosticLogService(rootPath, maximumLogLines: 2);

        service.Log("First", "state=Normal");
        service.Log("Second", "focused=TextBox");
        service.Log("Third", "key=Enter");

        var lines = File.ReadAllLines(service.LogPath);
        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain("event=First", string.Join(Environment.NewLine, lines), StringComparison.Ordinal);
        Assert.Contains("event=Second", lines[0], StringComparison.Ordinal);
        Assert.Contains("event=Third", lines[1], StringComparison.Ordinal);
        Assert.Contains("key=Enter", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Main_window_wires_low_frequency_input_and_window_state_diagnostics()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "MainWindow.xaml.cs"));

        Assert.Contains("Activated += MainWindow_Activated;", code);
        Assert.Contains("Deactivated += MainWindow_Deactivated;", code);
        Assert.Contains("PreviewKeyDown += MainWindow_PreviewKeyDown;", code);
        Assert.Contains("private void MainWindow_PreviewKeyDown", code);
        Assert.Contains("var diagnosticKey = e.Key == Key.System ? e.SystemKey : e.Key;", code);
        Assert.Contains("Key.Enter or Key.Back", code);
        Assert.Contains("LogWindowInputDiagnostic(\"Closing\"", code);
        Assert.Contains("LogWindowInputDiagnostic", code);
        Assert.Contains("LogWindowInputDiagnostic(\"MainHotkeyCallback\"", code);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
