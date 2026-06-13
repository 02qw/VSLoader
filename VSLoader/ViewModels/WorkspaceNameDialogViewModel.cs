using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VSLoader.ViewModels;

public sealed partial class WorkspaceNameDialogViewModel : ObservableObject
{
    public WorkspaceNameDialogViewModel()
        : this("新建工作区", "创建", string.Empty)
    {
    }

    public WorkspaceNameDialogViewModel(string windowTitle, string confirmButtonText, string workspaceName)
    {
        WindowTitle = windowTitle;
        ConfirmButtonText = confirmButtonText;
        this.workspaceName = workspaceName;
    }

    public string WindowTitle { get; }

    public string ConfirmButtonText { get; }

    [ObservableProperty]
    private string workspaceName = string.Empty;

    public event Action<bool?>? RequestClose;

    [RelayCommand]
    private void Create()
    {
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
