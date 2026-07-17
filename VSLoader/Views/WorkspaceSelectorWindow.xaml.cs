using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VSLoader.Behaviors;
using VSLoader.Services;
using VSLoader.ViewModels;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfControl = System.Windows.Controls.Control;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace VSLoader.Views;

public partial class WorkspaceSelectorWindow : Window
{
    private readonly DialogService dialogService = new();

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
            dialogService.ShowError(message);
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

    private void WorkspaceItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: WorkspaceListItemViewModel workspace }
            || DataContext is not WorkspaceSelectorViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedWorkspace = workspace;
    }

    private void WorkspaceItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListBoxItem item
            || item.DataContext is not WorkspaceListItemViewModel workspace
            || DataContext is not WorkspaceSelectorViewModel viewModel)
        {
            e.Handled = true;
            return;
        }

        viewModel.SelectedWorkspace = workspace;

        var menu = CreateWorkspaceContextMenu(viewModel);
        menu.PlacementTarget = item;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static ContextMenu CreateWorkspaceContextMenu(WorkspaceSelectorViewModel viewModel)
    {
        var menu = new ContextMenu
        {
            Padding = new Thickness(0),
            Background = WpfBrushes.White,
            BorderBrush = new WpfSolidColorBrush(WpfColor.FromRgb(209, 213, 219)),
            BorderThickness = new Thickness(1)
        };
        ContextMenuInputBehavior.SetSuppressRightClickActivation(menu, true);
        if (System.Windows.Application.Current.TryFindResource("ModernContextMenuStyle") is Style menuStyle)
        {
            menu.Style = menuStyle;
        }
        else
        {
            menu.Template = CreateCompactContextMenuTemplate();
        }

        menu.Items.Add(CreateWorkspaceMenuItem("打开", viewModel.OpenSelectedWorkspaceCommand));
        menu.Items.Add(CreateWorkspaceMenuItem("重命名", viewModel.StartRenameWorkspaceCommand));
        menu.Items.Add(CreateWorkspaceMenuItem("打开工作区文件夹", viewModel.OpenWorkspaceFolderCommand));
        menu.Items.Add(CreateWorkspaceMenuItem("删除", viewModel.StartDeleteWorkspaceCommand));

        return menu;
    }

    private static MenuItem CreateWorkspaceMenuItem(string header, ICommand command)
    {
        var item = new MenuItem
        {
            Header = header,
            MinWidth = 140,
            Padding = new Thickness(14, 8, 14, 8),
            Foreground = new WpfSolidColorBrush(WpfColor.FromRgb(17, 24, 39)),
            Background = WpfBrushes.Transparent,
            IsEnabled = command.CanExecute(null)
        };
        if (System.Windows.Application.Current.TryFindResource(
                header == "删除" ? "ModernDangerMenuItemStyle" : "ModernMenuItemStyle") is Style menuItemStyle)
        {
            item.Style = menuItemStyle;
        }
        else
        {
            item.Template = CreateCompactMenuItemTemplate();
        }

        item.Click += (_, _) =>
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
        };

        return item;
    }

    private static ControlTemplate CreateCompactContextMenuTemplate()
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, WpfBrushes.White);
        borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Border.BorderBrushProperty));
        borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Border.BorderThicknessProperty));
        borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);

        var presenterFactory = new FrameworkElementFactory(typeof(ItemsPresenter));
        borderFactory.AppendChild(presenterFactory);

        return new ControlTemplate(typeof(ContextMenu))
        {
            VisualTree = borderFactory
        };
    }

    private static ControlTemplate CreateCompactMenuItemTemplate()
    {
        var rootFactory = new FrameworkElementFactory(typeof(Border), "Root");
        rootFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(WpfControl.PaddingProperty));
        rootFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(WpfControl.BackgroundProperty));

        var presenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        presenterFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        presenterFactory.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        presenterFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        rootFactory.AppendChild(presenterFactory);

        var template = new ControlTemplate(typeof(MenuItem))
        {
            VisualTree = rootFactory
        };

        var highlightTrigger = new Trigger
        {
            Property = MenuItem.IsHighlightedProperty,
            Value = true
        };
        highlightTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new WpfSolidColorBrush(WpfColor.FromRgb(229, 241, 251)), "Root"));
        template.Triggers.Add(highlightTrigger);

        var disabledTrigger = new Trigger
        {
            Property = IsEnabledProperty,
            Value = false
        };
        disabledTrigger.Setters.Add(new Setter(ForegroundProperty, new WpfSolidColorBrush(WpfColor.FromRgb(156, 163, 175))));
        template.Triggers.Add(disabledTrigger);

        return template;
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
            dialogService.ShowError(result.ErrorMessage ?? "新建工作区失败。");
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
            dialogService.ShowError(result.ErrorMessage ?? "重命名工作区失败。");
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
        if (!dialogService.Confirm(message))
        {
            return;
        }

        var result = viewModel.DeleteSelectedWorkspace();
        if (!result.Success)
        {
            dialogService.ShowError(result.ErrorMessage ?? "删除工作区失败。");
        }
    }
}
