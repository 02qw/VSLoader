using System.Windows;
using VSLoader.ViewModels;

namespace VSLoader.Views;

public partial class ContextMenuCapabilityEditorWindow : Window
{
    public ContextMenuCapabilityEditorWindow(
        ContextMenuCapabilityEditorViewModel viewModel,
        Window owner)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = owner;
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }
}
