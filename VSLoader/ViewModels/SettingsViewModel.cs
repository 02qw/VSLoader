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
        HotkeyConfig hotkeyConfig,
        DialogService dialogService,
        PasswordProtectionService passwordProtectionService,
        Func<HotkeyConfig, SaveResult>? tryRegisterHotkey)
    {
        VSCodePath = vscodePath;
        AdminUi = adminUiConfig.Clone();
        Hotkey = hotkeyConfig.Clone();
        _dialogService = dialogService;
        _passwordProtectionService = passwordProtectionService;
        TryRegisterHotkey = tryRegisterHotkey;
        AdminUiPassword = _passwordProtectionService.Unprotect(AdminUi.ProtectedPassword);
        UpdateHotkeyDisplayText();
    }

    [ObservableProperty]
    private string vSCodePath = string.Empty;

    [ObservableProperty]
    private AdminUiConfig adminUi = new();

    [ObservableProperty]
    private string adminUiPassword = string.Empty;

    [ObservableProperty]
    private HotkeyConfig hotkey = new();

    [ObservableProperty]
    private string hotkeyDisplayText = string.Empty;

    [ObservableProperty]
    private bool isRecordingHotkey;

    public bool Saved { get; private set; }

    private Func<HotkeyConfig, SaveResult>? TryRegisterHotkey { get; }

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

        if (!ValidateHotkeyConfig())
        {
            return;
        }

        if (TryRegisterHotkey is not null)
        {
            var registerResult = TryRegisterHotkey(Hotkey);
            if (!registerResult.Success)
            {
                _dialogService.ShowError(registerResult.ErrorMessage ?? "快捷键注册失败。");
                return;
            }
        }

        AdminUi.ProtectedPassword = _passwordProtectionService.Protect(AdminUiPassword);
        Saved = true;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void StartRecordHotkey()
    {
        IsRecordingHotkey = true;
        HotkeyDisplayText = "请按下快捷键...";
    }

    [RelayCommand]
    private void ClearHotkey()
    {
        Hotkey = new HotkeyConfig();
        IsRecordingHotkey = false;
        UpdateHotkeyDisplayText();
    }

    public void SetRecordedHotkey(bool ctrl, bool alt, bool shift, string key, string inputType = "Keyboard")
    {
        Hotkey.Ctrl = ctrl;
        Hotkey.Alt = alt;
        Hotkey.Shift = shift;
        Hotkey.InputType = inputType;
        Hotkey.Key = key;
        Hotkey.Enabled = true;
        IsRecordingHotkey = false;
        UpdateHotkeyDisplayText();
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

    private bool ValidateHotkeyConfig()
    {
        var result = GlobalHotkeyService.Validate(Hotkey);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "快捷键无效。");
            return false;
        }

        return true;
    }

    private void UpdateHotkeyDisplayText()
    {
        HotkeyDisplayText = Hotkey.Enabled && !string.IsNullOrWhiteSpace(Hotkey.Key)
            ? GlobalHotkeyService.Format(Hotkey)
            : string.Empty;
    }
}
