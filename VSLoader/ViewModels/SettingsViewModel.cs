using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly DialogService _dialogService;
    private readonly PasswordProtectionService _passwordProtectionService;

    public SettingsViewModel(
        string vscodePath,
        AdminUiConfig adminUiConfig,
        DialogService dialogService,
        PasswordProtectionService passwordProtectionService)
    {
        VSCodePath = vscodePath;
        AdminUi = adminUiConfig.Clone();
        _dialogService = dialogService;
        _passwordProtectionService = passwordProtectionService;
        AdminUiPassword = _passwordProtectionService.Unprotect(AdminUi.ProtectedPassword);
    }

    [ObservableProperty]
    private string vSCodePath = string.Empty;

    [ObservableProperty]
    private AdminUiConfig adminUi = new();

    [ObservableProperty]
    private string adminUiPassword = string.Empty;

    public bool Saved { get; private set; }

    [RelayCommand]
    private void BrowseExe()
    {
        var path = _dialogService.SelectExeFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            VSCodePath = path;
        }
    }

    [RelayCommand]
    private void Save()
    {
        VSCodePath = VSCodePath.Trim();

        if (!VSCodeLauncherService.IsValidExecutablePath(VSCodePath))
        {
            _dialogService.ShowError("请选择一个存在的 .exe 文件。");
            return;
        }

        TrimAdminUiConfig();
        if (!ValidateAdminUiConfig())
        {
            return;
        }

        AdminUi.ProtectedPassword = _passwordProtectionService.Protect(AdminUiPassword);
        Saved = true;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    public event Action<bool?>? RequestClose;

    private void TrimAdminUiConfig()
    {
        AdminUi.BaseUrl = AdminUi.BaseUrl.Trim();
        AdminUi.Host = AdminUi.Host.Trim();
        AdminUi.RoleName = AdminUi.RoleName.Trim();
        AdminUi.InstancePropertiesName = AdminUi.InstancePropertiesName.Trim();
        AdminUi.InstanceNameKey = AdminUi.InstanceNameKey.Trim();
        AdminUi.PortKey = AdminUi.PortKey.Trim();
        AdminUi.ServiceNameKey = AdminUi.ServiceNameKey.Trim();
    }

    private bool ValidateAdminUiConfig()
    {
        if (string.IsNullOrWhiteSpace(AdminUi.BaseUrl)
            || string.IsNullOrWhiteSpace(AdminUi.Host)
            || string.IsNullOrWhiteSpace(AdminUi.RoleName)
            || string.IsNullOrWhiteSpace(AdminUi.InstancePropertiesName)
            || string.IsNullOrWhiteSpace(AdminUi.InstanceNameKey)
            || string.IsNullOrWhiteSpace(AdminUi.PortKey)
            || string.IsNullOrWhiteSpace(AdminUi.ServiceNameKey))
        {
            _dialogService.ShowError("AdminUI 配置项不能为空。");
            return false;
        }

        return true;
    }
}
