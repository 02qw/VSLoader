using System.Windows;
using System.Windows.Input;
using VSLoader.ViewModels;

namespace VSLoader.Views;

public partial class WorkspaceSelectorWindow : Window
{
    public WorkspaceSelectorWindow(WorkspaceSelectorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
        viewModel.RequestCreateWorkspace += ShowCreateWorkspaceDialog;
        viewModel.RequestRenameWorkspace += ShowRenameWorkspaceDialog;
        viewModel.RequestDeleteWorkspace += ShowDeleteWorkspaceConfirmation;
        viewModel.ShowErrorRequested += message =>
        {
            System.Windows.MessageBox.Show(this, message, "VSLoader", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
    }

    private void WorkspaceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WorkspaceSelectorViewModel viewModel
            && viewModel.OpenSelectedWorkspaceCommand.CanExecute(null))
        {
            viewModel.OpenSelectedWorkspaceCommand.Execute(null);
        }
    }

    private void ShowCreateWorkspaceDialog()
    {
        if (DataContext is not WorkspaceSelectorViewModel viewModel)
        {
            return;
        }

        var nameViewModel = new WorkspaceNameDialogViewModel();
        var dialog = new WorkspaceNameDialog(nameViewModel)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var result = viewModel.CreateWorkspace(nameViewModel.WorkspaceName);
        if (!result.Success)
        {
            System.Windows.MessageBox.Show(this, result.ErrorMessage ?? "新建工作区失败。", "VSLoader", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowRenameWorkspaceDialog()
    {
        if (DataContext is not WorkspaceSelectorViewModel viewModel || viewModel.SelectedWorkspace is null)
        {
            return;
        }

        var nameViewModel = new WorkspaceNameDialogViewModel("重命名工作区", "保存", viewModel.SelectedWorkspace.Name);
        var dialog = new WorkspaceNameDialog(nameViewModel)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var result = viewModel.RenameSelectedWorkspace(nameViewModel.WorkspaceName);
        if (!result.Success)
        {
            System.Windows.MessageBox.Show(this, result.ErrorMessage ?? "重命名工作区失败。", "VSLoader", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowDeleteWorkspaceConfirmation()
    {
        if (DataContext is not WorkspaceSelectorViewModel viewModel || viewModel.SelectedWorkspace is null)
        {
            return;
        }

        var workspaceName = viewModel.SelectedWorkspace.Name;
        var message = $"确定要彻底删除工作区“{workspaceName}”吗？\n\n该操作会删除此工作区下的全部配置、快捷项、地图、下载文件，且不可恢复。";
        var confirmation = System.Windows.MessageBox.Show(
            this,
            message,
            "删除工作区",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var result = viewModel.DeleteSelectedWorkspace();
        if (!result.Success)
        {
            System.Windows.MessageBox.Show(this, result.ErrorMessage ?? "删除工作区失败。", "VSLoader", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
