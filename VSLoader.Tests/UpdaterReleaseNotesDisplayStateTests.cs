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
}
