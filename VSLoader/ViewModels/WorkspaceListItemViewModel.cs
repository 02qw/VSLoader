using CommunityToolkit.Mvvm.ComponentModel;
using VSLoader.Models;

namespace VSLoader.ViewModels;

public sealed partial class WorkspaceListItemViewModel : ObservableObject
{
    public WorkspaceListItemViewModel(WorkspaceInfo info, bool isLastWorkspace, bool isUsable)
    {
        Info = info;
        IsLastWorkspace = isLastWorkspace;
        IsUsable = isUsable;
    }

    public WorkspaceInfo Info { get; }

    public string Id => Info.Id;

    public string Name => Info.Name;

    public string Path => Info.Path;

    public DateTime CreatedAt => Info.CreatedAt;

    public DateTime UpdatedAt => Info.UpdatedAt;

    public bool IsLastWorkspace { get; }

    public bool IsUsable { get; }

    public string StatusText
    {
        get
        {
            if (!IsUsable)
            {
                return "路径不存在";
            }

            return IsLastWorkspace ? "上次使用" : string.Empty;
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);
}
