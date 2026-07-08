using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class MainViewModelManualUpdateCheckTests
{
    [Fact]
    public void ShouldShowNoUpdateStatus_returns_true_only_when_no_updates_and_no_failures()
    {
        Assert.True(MainViewModel.ShouldShowNoUpdateStatus(new UpdateCheckResult()));

        var updated = new UpdateCheckResult();
        updated.UpdatedItems.Add("软件版本");
        Assert.False(MainViewModel.ShouldShowNoUpdateStatus(updated));

        var failed = new UpdateCheckResult();
        failed.Failures.Add("manifest 文件不存在");
        Assert.False(MainViewModel.ShouldShowNoUpdateStatus(failed));
    }
}
