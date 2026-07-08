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
        string softwareUpdateManifestPath,
        AdminUiConfig adminUiConfig,
        WebUiConfig webUiConfig,
        UpdateCheckConfig updateCheckConfig,
        HotkeyConfig hotkeyConfig,
        MapHotkeyConfig mapHotkeyConfig,
        DialogService dialogService,
        PasswordProtectionService passwordProtectionService,
        Func<HotkeyConfig, MapHotkeyConfig, SaveResult>? tryRegisterHotkeys)
    {
        VSCodePath = vscodePath;
        SoftwareUpdateManifestPath = softwareUpdateManifestPath;
        AdminUi = adminUiConfig.Clone();
        WebUi = webUiConfig.Clone();
        UpdateCheck = updateCheckConfig.Clone();
        Hotkey = hotkeyConfig.Clone();
        MapHotkey = mapHotkeyConfig.Clone();
        _dialogService = dialogService;
        _passwordProtectionService = passwordProtectionService;
        TryRegisterHotkeys = tryRegisterHotkeys;
        AdminUiPassword = _passwordProtectionService.Unprotect(AdminUi.ProtectedPassword);
        UpdateHotkeyDisplayText();
        UpdateMapHotkeyDisplayText();
    }

    [ObservableProperty]
    private string vSCodePath = string.Empty;

    [ObservableProperty]
    private string softwareUpdateManifestPath = string.Empty;

    [ObservableProperty]
    private AdminUiConfig adminUi = new();

    [ObservableProperty]
    private WebUiConfig webUi = new();

    [ObservableProperty]
    private UpdateCheckConfig updateCheck = new();

    [ObservableProperty]
    private string adminUiPassword = string.Empty;

    [ObservableProperty]
    private HotkeyConfig hotkey = new();

    [ObservableProperty]
    private MapHotkeyConfig mapHotkey = new();

    [ObservableProperty]
    private string hotkeyDisplayText = string.Empty;

    [ObservableProperty]
    private string mapHotkeyDisplayText = string.Empty;

    [ObservableProperty]
    private bool isRecordingHotkey;

    [ObservableProperty]
    private bool isRecordingMapHotkey;

    public bool Saved { get; private set; }

    private Func<HotkeyConfig, MapHotkeyConfig, SaveResult>? TryRegisterHotkeys { get; }

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
        SoftwareUpdateManifestPath = SoftwareUpdateManifestPath.Trim();

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

        TrimWebUiConfig();
        if (!ValidateWebUiConfig())
        {
            return;
        }

        TrimUpdateCheckConfig();

        if (!ValidateHotkeyConfig())
        {
            return;
        }

        if (!ValidateMapHotkeyConfig())
        {
            return;
        }

        if (TryRegisterHotkeys is not null)
        {
            var registerResult = TryRegisterHotkeys(Hotkey, MapHotkey);
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
    private void BrowseSoftwareUpdateManifest()
    {
        var path = _dialogService.SelectFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            SoftwareUpdateManifestPath = path;
        }
    }

    [RelayCommand]
    private void BrowseGlobalConfigPackage()
    {
        var path = _dialogService.SelectJsonFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            UpdateCheck.GlobalConfigPackagePath = path;
        }
    }

    [RelayCommand]
    private void BrowseRulesFile()
    {
        var path = _dialogService.SelectFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            UpdateCheck.RulesFilePath = path;
        }
    }

    [RelayCommand]
    private void BrowseMapFile()
    {
        var path = _dialogService.SelectFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            UpdateCheck.MapFilePath = path;
        }
    }

    [RelayCommand]
    private void StartRecordHotkey()
    {
        IsRecordingMapHotkey = false;
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

    [RelayCommand]
    private void StartRecordMapHotkey()
    {
        IsRecordingHotkey = false;
        IsRecordingMapHotkey = true;
        MapHotkeyDisplayText = "请按下地图快捷键...";
    }

    [RelayCommand]
    private void ClearMapHotkey()
    {
        MapHotkey = new MapHotkeyConfig { Enabled = false, Key = string.Empty };
        IsRecordingMapHotkey = false;
        UpdateMapHotkeyDisplayText();
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

    public void SetRecordedMapHotkey(bool ctrl, bool alt, bool shift, string key)
    {
        MapHotkey.Ctrl = ctrl;
        MapHotkey.Alt = alt;
        MapHotkey.Shift = shift;
        MapHotkey.Key = key;
        MapHotkey.Enabled = true;
        IsRecordingMapHotkey = false;
        UpdateMapHotkeyDisplayText();
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
        AdminUi.AutoPasteWindowTitleKeyword = AdminUi.AutoPasteWindowTitleKeyword.Trim();
        AdminUi.AutoPasteProcessNames = AdminUi.AutoPasteProcessNames.Trim();
        AdminUi.AutoPasteTimeoutSeconds = Math.Clamp(AdminUi.AutoPasteTimeoutSeconds, 1, 60);
        AdminUi.AutoPasteInitialDelayMilliseconds = Math.Clamp(AdminUi.AutoPasteInitialDelayMilliseconds, 0, 30000);
        AdminUi.AutoPastePollIntervalMilliseconds = Math.Clamp(AdminUi.AutoPastePollIntervalMilliseconds, 50, 2000);
    }

    private void TrimWebUiConfig()
    {
        WebUi.BaseUrl = WebUi.BaseUrl.Trim();
        WebUi.InstancePropertiesName = WebUi.InstancePropertiesName.Trim();
        WebUi.InstanceNameKey = WebUi.InstanceNameKey.Trim();
        WebUi.SslPortKey = WebUi.SslPortKey.Trim();
    }

    private void TrimUpdateCheckConfig()
    {
        UpdateCheck.GlobalConfigPackagePath = UpdateCheck.GlobalConfigPackagePath.Trim();
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

        if (AdminUi.AutoPastePasswordEnabled
            && (string.IsNullOrWhiteSpace(AdminUi.AutoPasteWindowTitleKeyword)
                || string.IsNullOrWhiteSpace(AdminUi.AutoPasteProcessNames)))
        {
            _dialogService.ShowError("启用自动粘贴时，请配置 AdminUI 窗口标题关键字和允许进程名。");
            return false;
        }

        return true;
    }

    private bool ValidateWebUiConfig()
    {
        if (string.IsNullOrWhiteSpace(WebUi.BaseUrl))
        {
            _dialogService.ShowError("请输入有效的 WebUI BaseUrl。");
            return false;
        }

        if (!WebUi.BaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !WebUi.BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _dialogService.ShowError("WebUI BaseUrl 必须以 http:// 或 https:// 开头。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(WebUi.InstancePropertiesName))
        {
            _dialogService.ShowError("请输入 WebUI properties 文件名。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(WebUi.InstanceNameKey))
        {
            _dialogService.ShowError("请输入 WebUI 实例名 Key。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(WebUi.SslPortKey))
        {
            _dialogService.ShowError("请输入 WebUI SSL 端口 Key。");
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

    private bool ValidateMapHotkeyConfig()
    {
        var result = MapHotkeyService.Validate(MapHotkey);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "地图快捷键无效。");
            return false;
        }

        if (MapHotkeyService.HasSameGestureAsMainHotkey(MapHotkey, Hotkey))
        {
            _dialogService.ShowError("主程序快捷键和地图快捷键不能相同。");
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

    private void UpdateMapHotkeyDisplayText()
    {
        MapHotkeyDisplayText = MapHotkeyService.Format(MapHotkey);
    }
}
