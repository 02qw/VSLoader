using VSLoader.Updater;

namespace VSLoader.Tests;

public sealed class UpdaterReleaseNotesDisplayStateTests
{
    [Fact]
    public void ShouldShowReleaseNotes_returns_false_when_notes_are_not_loaded_yet()
    {
        var result = VSLoader.Updater.MainWindow.ShouldShowReleaseNotes(manifestLoaded: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldShowReleaseNotes_returns_true_after_manifest_loaded_even_when_notes_are_empty()
    {
        var result = VSLoader.Updater.MainWindow.ShouldShowReleaseNotes(manifestLoaded: true);

        Assert.True(result);
    }

    [Fact]
    public void ShouldShowReleaseNotesAfterError_returns_false_even_when_notes_were_visible()
    {
        var result = VSLoader.Updater.MainWindow.ShouldShowReleaseNotesAfterError(wasVisible: true);

        Assert.False(result);
    }

    [Fact]
    public void BuildOpenErrorLogDirectoryFailureMessage_includes_original_error()
    {
        var result = VSLoader.Updater.MainWindow.BuildOpenErrorLogDirectoryFailureMessage(new InvalidOperationException("shell failed"));

        Assert.Equal("打开日志目录失败：shell failed", result);
    }

    [Fact]
    public void FormatDetailLine_prefixes_message_with_timestamp()
    {
        var result = VSLoader.Updater.MainWindow.FormatDetailLine(new DateTime(2026, 6, 17, 9, 8, 7), "正在复制更新包...");

        Assert.Equal("[09:08:07] 正在复制更新包...", result);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, 0)]
    [InlineData(5, 4)]
    public void GetLastDetailLineIndex_returns_latest_item_index(int count, int expected)
    {
        var result = VSLoader.Updater.MainWindow.GetLastDetailLineIndex(count);

        Assert.Equal(expected, result);
    }
}
