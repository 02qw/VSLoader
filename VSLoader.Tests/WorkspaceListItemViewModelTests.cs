using VSLoader.Models;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class WorkspaceListItemViewModelTests
{
    [Fact]
    public void HasStatus_is_false_when_workspace_has_no_status()
    {
        var item = new WorkspaceListItemViewModel(
            new WorkspaceInfo
            {
                Name = "普通工作区",
                Path = @"C:\Temp\work1"
            },
            isLastWorkspace: false,
            isUsable: true);

        Assert.Equal(string.Empty, item.StatusText);
        Assert.False(item.HasStatus);
    }

    [Fact]
    public void HasStatus_is_true_for_last_workspace()
    {
        var item = new WorkspaceListItemViewModel(
            new WorkspaceInfo
            {
                Name = "上次使用工作区",
                Path = @"C:\Temp\work1"
            },
            isLastWorkspace: true,
            isUsable: true);

        Assert.Equal("上次使用", item.StatusText);
        Assert.True(item.HasStatus);
    }

    [Fact]
    public void HasStatus_is_true_when_workspace_path_missing()
    {
        var item = new WorkspaceListItemViewModel(
            new WorkspaceInfo
            {
                Name = "不可用工作区",
                Path = @"C:\Temp\missing"
            },
            isLastWorkspace: false,
            isUsable: false);

        Assert.Equal("路径不存在", item.StatusText);
        Assert.True(item.HasStatus);
    }
}
