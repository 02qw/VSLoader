using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly DialogService _dialogService;
    private readonly PasswordProtectionService _passwordProtectionService;
    private readonly ContextMenuCapabilityConfigService _contextMenuCapabilityConfigService = new();
    private readonly HashSet<string> powerShellCapabilitiesApprovedForTrust = new(StringComparer.OrdinalIgnoreCase);

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
        Func<HotkeyConfig, MapHotkeyConfig, SaveResult>? tryRegisterHotkeys,
        ContextMenuCapabilityCollectionConfig? contextMenuCapabilityConfig = null,
        IEnumerable<string>? settingsPageOrder = null)
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
        var capabilityConfig = contextMenuCapabilityConfig?.Clone()
            ?? _contextMenuCapabilityConfigService.CreateDefault();
        _ = _contextMenuCapabilityConfigService.Normalize(capabilityConfig);
        ContextMenuCapabilities = capabilityConfig;
        ContextMenuCapabilityItems = new ObservableCollection<ContextMenuCapabilityListItemViewModel>(
            capabilityConfig.Items.Select(item => new ContextMenuCapabilityListItemViewModel(item)));
        SettingsPages = new ObservableCollection<SettingsPageItemViewModel>(
            SettingsPageOrderService.Normalize(settingsPageOrder)
                .Select(pageId => new SettingsPageItemViewModel(
                    pageId,
                    SettingsPageOrderService.GetDisplayName(pageId))));
        SettingsPages.Add(new SettingsPageItemViewModel(
            SettingsPageIds.PageOrder,
            SettingsPageOrderService.GetDisplayName(SettingsPageIds.PageOrder),
            isFixed: true));
        SelectedSettingsPage = SettingsPages[0];
        RefreshSettingsPageMoveState();
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

    public ObservableCollection<ContextMenuCapabilityListItemViewModel> ContextMenuCapabilityItems { get; }

    public ObservableCollection<SettingsPageItemViewModel> SettingsPages { get; }

    public IReadOnlyList<string> SettingsPageOrder =>
        SettingsPages.Where(page => !page.IsFixed).Select(page => page.Id).ToList();

    public ContextMenuCapabilityCollectionConfig ContextMenuCapabilities { get; private set; }

    [ObservableProperty]
    private SettingsPageItemViewModel? selectedSettingsPage;

    public bool IsGeneralPageSelected => IsPageSelected(SettingsPageIds.General);

    public bool IsAdminUiPageSelected => IsPageSelected(SettingsPageIds.AdminUi);

    public bool IsWebUiPageSelected => IsPageSelected(SettingsPageIds.WebUi);

    public bool IsUpdatesPageSelected => IsPageSelected(SettingsPageIds.Updates);

    public bool IsHotkeysPageSelected => IsPageSelected(SettingsPageIds.Hotkeys);

    public bool IsContextMenuCapabilitiesPageSelected => IsPageSelected(SettingsPageIds.ContextMenuCapabilities);

    public bool IsPageOrderPageSelected => IsPageSelected(SettingsPageIds.PageOrder);

    public IReadOnlyList<ContextMenuCapabilityDefinition> PowerShellCapabilitiesApprovedForTrust =>
        ContextMenuCapabilityItems
            .Where(item => powerShellCapabilitiesApprovedForTrust.Contains(item.Definition.Id))
            .Where(item => string.Equals(item.Definition.Kind, ContextMenuCapabilityKinds.PowerShell, StringComparison.Ordinal))
            .Select(item => item.Definition.Clone())
            .ToList();

    public Func<ContextMenuCapabilityDefinition, ContextMenuCapabilityDefinition?>? EditContextMenuCapability { get; set; }

    private Func<HotkeyConfig, MapHotkeyConfig, SaveResult>? TryRegisterHotkeys { get; }

    partial void OnSelectedSettingsPageChanged(SettingsPageItemViewModel? value)
    {
        OnPropertyChanged(nameof(IsGeneralPageSelected));
        OnPropertyChanged(nameof(IsAdminUiPageSelected));
        OnPropertyChanged(nameof(IsWebUiPageSelected));
        OnPropertyChanged(nameof(IsUpdatesPageSelected));
        OnPropertyChanged(nameof(IsHotkeysPageSelected));
        OnPropertyChanged(nameof(IsContextMenuCapabilitiesPageSelected));
        OnPropertyChanged(nameof(IsPageOrderPageSelected));
    }

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

        var capabilityConfig = BuildContextMenuCapabilityConfig();
        _ = _contextMenuCapabilityConfigService.Normalize(capabilityConfig);
        var capabilityValidation = _contextMenuCapabilityConfigService.Validate(capabilityConfig);
        if (!capabilityValidation.Success)
        {
            _dialogService.ShowError(capabilityValidation.ErrorMessage ?? "右键菜单能力配置无效。");
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
        ContextMenuCapabilities = capabilityConfig;
        Saved = true;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void MoveCapabilityUp(ContextMenuCapabilityListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var index = ContextMenuCapabilityItems.IndexOf(item);
        if (index > 0)
        {
            ContextMenuCapabilityItems.Move(index, index - 1);
            RefreshCapabilityOrders();
        }
    }

    [RelayCommand]
    private void MoveCapabilityDown(ContextMenuCapabilityListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var index = ContextMenuCapabilityItems.IndexOf(item);
        if (index >= 0 && index < ContextMenuCapabilityItems.Count - 1)
        {
            ContextMenuCapabilityItems.Move(index, index + 1);
            RefreshCapabilityOrders();
        }
    }

    [RelayCommand]
    private void MoveSettingsPageUp(SettingsPageItemViewModel? page)
    {
        if (page is null || page.IsFixed)
        {
            return;
        }

        var index = SettingsPages.IndexOf(page);
        if (index > 0)
        {
            SettingsPages.Move(index, index - 1);
            RefreshSettingsPageMoveState();
        }
    }

    [RelayCommand]
    private void MoveSettingsPageDown(SettingsPageItemViewModel? page)
    {
        if (page is null || page.IsFixed)
        {
            return;
        }

        var index = SettingsPages.IndexOf(page);
        if (index >= 0 && index < SettingsPages.Count - 2)
        {
            SettingsPages.Move(index, index + 1);
            RefreshSettingsPageMoveState();
        }
    }

    [RelayCommand]
    private void RestoreDefaultSettingsPageOrder()
    {
        for (var targetIndex = 0; targetIndex < SettingsPageOrderService.DefaultPageOrder.Count; targetIndex++)
        {
            var pageId = SettingsPageOrderService.DefaultPageOrder[targetIndex];
            var currentIndex = SettingsPages.ToList().FindIndex(page =>
                string.Equals(page.Id, pageId, StringComparison.Ordinal));
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                SettingsPages.Move(currentIndex, targetIndex);
            }
        }

        RefreshSettingsPageMoveState();
    }

    [RelayCommand]
    private void AddPowerShellCapability()
    {
        EditAndAddCapability(new ContextMenuCapabilityDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "新建命令能力",
            Kind = ContextMenuCapabilityKinds.PowerShell,
            RequiresExistingTargetPath = true,
            PowerShell = new PowerShellCapabilityConfig()
        });
    }

    [RelayCommand]
    private void AddWebCapability()
    {
        EditAndAddCapability(new ContextMenuCapabilityDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "新建 Web 能力",
            Kind = ContextMenuCapabilityKinds.Web,
            RequiresExistingTargetPath = false,
            Web = new WebCapabilityConfig { UrlTemplate = "https://" }
        });
    }

    [RelayCommand]
    private void EditCapability(ContextMenuCapabilityListItemViewModel? item)
    {
        if (item is null || EditContextMenuCapability is null)
        {
            return;
        }

        var edited = EditContextMenuCapability(item.Definition.Clone());
        if (edited is null)
        {
            return;
        }

        CopyDefinition(edited, item.Definition);
        if (string.Equals(item.Definition.Kind, ContextMenuCapabilityKinds.PowerShell, StringComparison.Ordinal))
        {
            powerShellCapabilitiesApprovedForTrust.Add(item.Definition.Id);
        }

        item.RefreshDisplay();
    }

    [RelayCommand]
    private void DuplicateCapability(ContextMenuCapabilityListItemViewModel? item)
    {
        if (item is null || item.IsBuiltIn || EditContextMenuCapability is null)
        {
            return;
        }

        var copy = item.Definition.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = $"{copy.Name} - 副本";
        EditAndAddCapability(copy);
    }

    [RelayCommand]
    private void DeleteCapability(ContextMenuCapabilityListItemViewModel? item)
    {
        if (item is null || item.IsBuiltIn)
        {
            return;
        }

        if (_dialogService.Confirm($"确定删除右键菜单能力“{item.Name}”吗？"))
        {
            ContextMenuCapabilityItems.Remove(item);
            powerShellCapabilitiesApprovedForTrust.Remove(item.Definition.Id);
            RefreshCapabilityOrders();
        }
    }

    [RelayCommand]
    private void RestoreDefaultCapabilityOrder()
    {
        var builtInOrder = ContextMenuBuiltInActionIds.All;
        var builtIns = builtInOrder
            .Select(actionId => ContextMenuCapabilityItems.FirstOrDefault(item =>
                string.Equals(item.Definition.BuiltInActionId, actionId, StringComparison.OrdinalIgnoreCase)))
            .Where(item => item is not null)
            .Cast<ContextMenuCapabilityListItemViewModel>()
            .ToList();
        var custom = ContextMenuCapabilityItems.Where(item => !item.IsBuiltIn).ToList();
        ContextMenuCapabilityItems.Clear();
        foreach (var item in builtIns.Concat(custom))
        {
            ContextMenuCapabilityItems.Add(item);
        }

        RefreshCapabilityOrders();
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

    private void EditAndAddCapability(ContextMenuCapabilityDefinition definition)
    {
        if (EditContextMenuCapability is null)
        {
            return;
        }

        var edited = EditContextMenuCapability(definition);
        if (edited is null)
        {
            return;
        }

        edited.Id = string.IsNullOrWhiteSpace(edited.Id)
            ? Guid.NewGuid().ToString("N")
            : edited.Id;
        ContextMenuCapabilityItems.Add(new ContextMenuCapabilityListItemViewModel(edited));
        if (string.Equals(edited.Kind, ContextMenuCapabilityKinds.PowerShell, StringComparison.Ordinal))
        {
            powerShellCapabilitiesApprovedForTrust.Add(edited.Id);
        }

        RefreshCapabilityOrders();
    }

    private ContextMenuCapabilityCollectionConfig BuildContextMenuCapabilityConfig()
    {
        RefreshCapabilityOrders();
        return new ContextMenuCapabilityCollectionConfig
        {
            SchemaVersion = 1,
            Items = ContextMenuCapabilityItems.Select(item => item.Definition.Clone()).ToList()
        };
    }

    private void RefreshCapabilityOrders()
    {
        for (var index = 0; index < ContextMenuCapabilityItems.Count; index++)
        {
            ContextMenuCapabilityItems[index].Definition.Order = index * 10;
        }
    }

    private bool IsPageSelected(string pageId)
    {
        return string.Equals(SelectedSettingsPage?.Id, pageId, StringComparison.Ordinal);
    }

    private void RefreshSettingsPageMoveState()
    {
        for (var index = 0; index < SettingsPages.Count; index++)
        {
            var page = SettingsPages[index];
            page.CanMoveUp = !page.IsFixed && index > 0;
            page.CanMoveDown = !page.IsFixed && index < SettingsPages.Count - 2;
        }

        OnPropertyChanged(nameof(SettingsPageOrder));
    }

    private static void CopyDefinition(
        ContextMenuCapabilityDefinition source,
        ContextMenuCapabilityDefinition target)
    {
        target.Name = source.Name;
        target.Enabled = source.Enabled;
        target.ShowInShortcutList = source.ShowInShortcutList;
        target.ShowInFactoryMap = source.ShowInFactoryMap;
        target.ConfirmBeforeExecute = source.ConfirmBeforeExecute;
        target.RequiresExistingTargetPath = source.RequiresExistingTargetPath;
        target.PowerShell = source.PowerShell.Clone();
        target.Web = source.Web.Clone();
    }
}
