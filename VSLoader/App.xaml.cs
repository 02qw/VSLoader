using System.Windows;
using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;
using VSLoader.Views;

namespace VSLoader;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private AppSettingsService? appSettingsService;
    private AppSettings? appSettings;
    private WorkspaceService? workspaceService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        appSettingsService = new AppSettingsService();
        appSettings = appSettingsService.LoadOrCreate(out var settingsWarning);
        workspaceService = new WorkspaceService(appSettingsService.AppDataDirectory);
        var migrationService = new LegacyWorkspaceMigrationService(appSettingsService.AppDataDirectory, workspaceService);
        var migrationResult = migrationService.TryMigrate(appSettings);
        if (!migrationResult.Success)
        {
            System.Windows.MessageBox.Show(
                $"旧数据迁移失败，程序将尝试打开默认工作区。\n\n{migrationResult.ErrorMessage}",
                "VSLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        workspaceService.EnsureDefaultWorkspace(appSettings);
        var saveResult = appSettingsService.Save(appSettings);
        if (!saveResult.Success)
        {
            System.Windows.MessageBox.Show(
                $"保存程序配置失败：{saveResult.ErrorMessage}",
                "VSLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        if (!string.IsNullOrWhiteSpace(settingsWarning))
        {
            System.Windows.MessageBox.Show(settingsWarning, "VSLoader", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        var workspaceContext = ShowWorkspaceSelector(null, shutdownOnCancel: true);
        if (workspaceContext is null)
        {
            Shutdown();
            return;
        }

        OpenMainWindow(workspaceContext);
    }

    internal void SwitchWorkspace(MainWindow currentWindow)
    {
        var workspaceContext = ShowWorkspaceSelector(currentWindow, shutdownOnCancel: false, currentWindow.WorkspaceId);
        RefreshCurrentWorkspaceTitle(currentWindow);
        if (workspaceContext is null || workspaceContext.Id == currentWindow.WorkspaceId)
        {
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        currentWindow.PrepareForWorkspaceSwitch();
        currentWindow.Close();
        OpenMainWindow(workspaceContext);
    }

    private void RefreshCurrentWorkspaceTitle(MainWindow currentWindow)
    {
        var currentWorkspace = appSettings?.Workspaces.FirstOrDefault(workspace =>
            string.Equals(workspace.Id, currentWindow.WorkspaceId, StringComparison.OrdinalIgnoreCase));
        if (currentWorkspace is not null)
        {
            currentWindow.RefreshWorkspaceTitle(currentWorkspace.Name);
        }
    }

    private WorkspaceContext? ShowWorkspaceSelector(Window? owner, bool shutdownOnCancel, string? activeWorkspaceId = null)
    {
        if (appSettings is null || appSettingsService is null || workspaceService is null)
        {
            return null;
        }

        var selectorViewModel = new WorkspaceSelectorViewModel(appSettings, appSettingsService, workspaceService, activeWorkspaceId);
        var selectorWindow = new WorkspaceSelectorWindow(selectorViewModel);
        if (owner is not null)
        {
            selectorWindow.Owner = owner;
        }

        var result = selectorWindow.ShowDialog();
        if (result != true)
        {
            if (shutdownOnCancel)
            {
                Shutdown();
            }

            return null;
        }

        return selectorViewModel.SelectedWorkspaceContext;
    }

    private void OpenMainWindow(WorkspaceContext workspaceContext)
    {
        if (appSettings is null || appSettingsService is null)
        {
            return;
        }

        var mainWindow = new MainWindow(appSettings, appSettingsService, workspaceContext);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }
}
