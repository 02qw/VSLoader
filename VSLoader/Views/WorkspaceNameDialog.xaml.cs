using System.Windows;
using VSLoader.ViewModels;

namespace VSLoader.Views;

public partial class WorkspaceNameDialog : Window
{
    public WorkspaceNameDialog(WorkspaceNameDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            WorkspaceNameBox.Focus();
            WorkspaceNameBox.SelectAll();
        };
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }
}
