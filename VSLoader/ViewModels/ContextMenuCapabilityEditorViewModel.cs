using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.ViewModels;

public sealed record ContextMenuCapabilityOption(string Value, string DisplayName);

public sealed partial class ContextMenuCapabilityEditorViewModel : ObservableObject
{
    private readonly ContextMenuCapabilityDefinition original;
    private readonly DialogService dialogService;
    private readonly ContextMenuCapabilityConfigService configService = new();
    private readonly ContextMenuUrlTemplateService urlTemplateService = new();

    public ContextMenuCapabilityEditorViewModel(
        ContextMenuCapabilityDefinition definition,
        DialogService dialogService)
    {
        original = definition.Clone();
        this.dialogService = dialogService;
        Name = definition.Name;
        Enabled = definition.Enabled;
        ShowInShortcutList = definition.ShowInShortcutList;
        ShowInFactoryMap = definition.ShowInFactoryMap;
        ConfirmBeforeExecute = definition.ConfirmBeforeExecute;
        RequiresExistingTargetPath = definition.RequiresExistingTargetPath;
        Script = definition.PowerShell?.Script ?? string.Empty;
        WorkingDirectoryMode = definition.PowerShell?.WorkingDirectoryMode
            ?? PowerShellCapabilityWorkingDirectoryModes.Target;
        ExecutionMode = definition.PowerShell?.ExecutionMode
            ?? PowerShellCapabilityExecutionModes.Visible;
        TimeoutSeconds = definition.PowerShell?.TimeoutSeconds ?? 30;
        UrlTemplate = definition.Web?.UrlTemplate ?? string.Empty;
        UpdateUrlPreview();
    }

    public string WindowTitle => IsBuiltIn ? "编辑内建能力" : "编辑右键菜单能力";

    public bool IsBuiltIn => string.Equals(original.Kind, ContextMenuCapabilityKinds.BuiltIn, StringComparison.Ordinal);

    public bool IsPowerShell => string.Equals(original.Kind, ContextMenuCapabilityKinds.PowerShell, StringComparison.Ordinal);

    public bool IsWeb => string.Equals(original.Kind, ContextMenuCapabilityKinds.Web, StringComparison.Ordinal);

    public string TypeDisplayName => original.Kind switch
    {
        ContextMenuCapabilityKinds.BuiltIn => "内建能力",
        ContextMenuCapabilityKinds.PowerShell => "PowerShell 命令能力",
        ContextMenuCapabilityKinds.Web => "Web 能力",
        _ => "不支持的能力"
    };

    public IReadOnlyList<ContextMenuCapabilityOption> WorkingDirectoryModes { get; } =
    [
        new(PowerShellCapabilityWorkingDirectoryModes.Target, "目标目录（推荐）"),
        new(PowerShellCapabilityWorkingDirectoryModes.TargetParent, "目标父目录"),
        new(PowerShellCapabilityWorkingDirectoryModes.Workspace, "当前工作区目录"),
        new(PowerShellCapabilityWorkingDirectoryModes.App, "VSLoader程序目录")
    ];

    public IReadOnlyList<ContextMenuCapabilityOption> ExecutionModes { get; } =
    [
        new(PowerShellCapabilityExecutionModes.Visible, "显示 PowerShell 窗口"),
        new(PowerShellCapabilityExecutionModes.Background, "后台执行")
    ];

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool enabled = true;

    [ObservableProperty]
    private bool showInShortcutList = true;

    [ObservableProperty]
    private bool showInFactoryMap = true;

    [ObservableProperty]
    private bool confirmBeforeExecute;

    [ObservableProperty]
    private bool requiresExistingTargetPath = true;

    [ObservableProperty]
    private string script = string.Empty;

    [ObservableProperty]
    private string workingDirectoryMode = PowerShellCapabilityWorkingDirectoryModes.Target;

    [ObservableProperty]
    private string executionMode = PowerShellCapabilityExecutionModes.Visible;

    [ObservableProperty]
    private int timeoutSeconds = 30;

    [ObservableProperty]
    private string urlTemplate = string.Empty;

    [ObservableProperty]
    private string urlPreview = string.Empty;

    public ContextMenuCapabilityDefinition? Result { get; private set; }

    public event Action<bool?>? RequestClose;

    partial void OnUrlTemplateChanged(string value)
    {
        UpdateUrlPreview();
    }

    [RelayCommand]
    private void Save()
    {
        var definition = original.Clone();
        definition.Name = IsBuiltIn ? original.Name : Name.Trim();
        definition.Enabled = Enabled;
        definition.ShowInShortcutList = ShowInShortcutList;
        definition.ShowInFactoryMap = ShowInFactoryMap;
        definition.ConfirmBeforeExecute = ConfirmBeforeExecute;
        definition.RequiresExistingTargetPath = RequiresExistingTargetPath;
        definition.PowerShell = new PowerShellCapabilityConfig
        {
            Script = Script,
            WorkingDirectoryMode = WorkingDirectoryMode,
            ExecutionMode = ExecutionMode,
            TimeoutSeconds = TimeoutSeconds
        };
        definition.Web = new WebCapabilityConfig { UrlTemplate = UrlTemplate };

        var validation = configService.Validate(definition);
        if (!validation.Success)
        {
            dialogService.ShowError(validation.ErrorMessage ?? "能力配置无效。");
            return;
        }

        if (IsPowerShell
            && !dialogService.Confirm(
                "PowerShell 命令可以读取、修改或删除当前用户有权限访问的文件，并可启动其他程序。"
                + "请确认你理解并信任当前脚本。\n\n"
                + Script))
        {
            return;
        }

        Result = definition;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private void UpdateUrlPreview()
    {
        if (!IsWeb || string.IsNullOrWhiteSpace(UrlTemplate))
        {
            UrlPreview = string.Empty;
            return;
        }

        var result = urlTemplateService.Build(
            UrlTemplate,
            new ContextMenuCapabilityExecutionContext
            {
                Shortcut = new ShortcutItem
                {
                    Name = "示例设备_001",
                    TargetPath = @"\\192.168.15.69\instances\3134_TSSP001",
                    Description = "示例设备 3134_TSSP001",
                    SourceModuleName = "eap-sic-Example"
                },
                WorkspaceId = "default",
                WorkspaceDirectory = @"C:\VSLoader"
            });
        UrlPreview = result.Success ? result.Url : result.ErrorMessage;
    }
}
