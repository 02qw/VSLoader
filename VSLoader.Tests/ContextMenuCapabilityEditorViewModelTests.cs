using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class ContextMenuCapabilityEditorViewModelTests
{
    [Fact]
    public void PowerShell_options_use_chinese_labels_and_save_compatible_values()
    {
        var viewModel = new ContextMenuCapabilityEditorViewModel(
            new ContextMenuCapabilityDefinition
            {
                Id = "ps",
                Name = "命令",
                Kind = ContextMenuCapabilityKinds.PowerShell,
                PowerShell = new PowerShellCapabilityConfig { Script = "Write-Output 1" }
            },
            new RecordingDialogService(true));

        Assert.Collection(
            viewModel.WorkingDirectoryModes,
            option => Assert.Equal((PowerShellCapabilityWorkingDirectoryModes.Target, "目标目录（推荐）"), (option.Value, option.DisplayName)),
            option => Assert.Equal((PowerShellCapabilityWorkingDirectoryModes.TargetParent, "目标父目录"), (option.Value, option.DisplayName)),
            option => Assert.Equal((PowerShellCapabilityWorkingDirectoryModes.Workspace, "当前工作区目录"), (option.Value, option.DisplayName)),
            option => Assert.Equal((PowerShellCapabilityWorkingDirectoryModes.App, "VSLoader程序目录"), (option.Value, option.DisplayName)));
        Assert.Collection(
            viewModel.ExecutionModes,
            option => Assert.Equal((PowerShellCapabilityExecutionModes.Visible, "显示 PowerShell 窗口"), (option.Value, option.DisplayName)),
            option => Assert.Equal((PowerShellCapabilityExecutionModes.Background, "后台执行"), (option.Value, option.DisplayName)));

        viewModel.WorkingDirectoryMode = PowerShellCapabilityWorkingDirectoryModes.Workspace;
        viewModel.ExecutionMode = PowerShellCapabilityExecutionModes.Background;
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(viewModel.Result);
        Assert.Equal(PowerShellCapabilityWorkingDirectoryModes.Workspace, viewModel.Result!.PowerShell.WorkingDirectoryMode);
        Assert.Equal(PowerShellCapabilityExecutionModes.Background, viewModel.Result.PowerShell.ExecutionMode);
    }

    [Fact]
    public void PowerShell_help_uses_safe_directory_example_and_chinese_option_descriptions()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "ContextMenuCapabilityEditorWindow.xaml"));

        Assert.Contains("目标目录（推荐）", xaml, StringComparison.Ordinal);
        Assert.Contains("目标父目录", xaml, StringComparison.Ordinal);
        Assert.Contains("当前工作区目录", xaml, StringComparison.Ordinal);
        Assert.Contains("VSLoader程序目录", xaml, StringComparison.Ordinal);
        Assert.Contains("Test-Path -LiteralPath $path -PathType Container", xaml, StringComparison.Ordinal);
        Assert.Contains("Set-Location -LiteralPath $env:VSL_TARGET_PARENT", xaml, StringComparison.Ordinal);
        Assert.Contains("param(", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_TARGET_NAME", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_INSTANCE_ID", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_DEVICE_CODE", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_DEVICE_TYPE", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_DEVICE_NUMBER", xaml, StringComparison.Ordinal);
        Assert.Contains("ProcessDevice.ps1", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_help_explains_variables_rules_and_copyable_examples()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "ContextMenuCapabilityEditorWindow.xaml"));

        Assert.Contains("可用变量（VSLoader 自动提供）", xaml, StringComparison.Ordinal);
        Assert.Contains("快捷项目标路径", xaml, StringComparison.Ordinal);
        Assert.Contains("变量值会自动进行 URL 编码", xaml, StringComparison.Ordinal);
        Assert.Contains("仅允许 http:// 和 https://", xaml, StringComparison.Ordinal);
        Assert.Contains("可直接复制的 URL 模板案例", xaml, StringComparison.Ordinal);
        Assert.Contains("https://www.google.com/search?q={ShortcutName}", xaml, StringComparison.Ordinal);
        Assert.Contains("https://example.com/device?name={ShortcutName}&amp;path={TargetPath}", xaml, StringComparison.Ordinal);
        Assert.Contains("https://example.com/open?workspace={WorkspaceId}&amp;name={ShortcutName}&amp;path={TargetPath}", xaml, StringComparison.Ordinal);
        Assert.Contains("错误：$env:VSL_SHORTCUT_NAME", xaml, StringComparison.Ordinal);
        Assert.Contains("正确：{ShortcutName}", xaml, StringComparison.Ordinal);
        Assert.Contains("file:///C:/Temp", xaml, StringComparison.Ordinal);
        Assert.Contains("{}{TargetName}", xaml, StringComparison.Ordinal);
        Assert.Contains("{}{InstanceId}", xaml, StringComparison.Ordinal);
        Assert.Contains("{}{DeviceCode}", xaml, StringComparison.Ordinal);
        Assert.Contains("{}{DeviceType}", xaml, StringComparison.Ordinal);
        Assert.Contains("{}{DeviceNumber}", xaml, StringComparison.Ordinal);
        Assert.Contains("https://example.com/device?id={DeviceCode}", xaml, StringComparison.Ordinal);
        Assert.Contains("type={DeviceType}&amp;number={DeviceNumber}", xaml, StringComparison.Ordinal);
        Assert.Contains("设备序号的数字部分，例如 001；保留前导零", xaml, StringComparison.Ordinal);
        Assert.Contains("使用规则：&#x0a;变量区分大小写。&#x0a;普通可选变量没有内容时替换为空字符串。", xaml, StringComparison.Ordinal);
        Assert.Contains("常见错误：&#x0a;错误：$env:VSL_SHORTCUT_NAME", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_template_updates_encoded_preview_and_saves_result()
    {
        var viewModel = new ContextMenuCapabilityEditorViewModel(
            new ContextMenuCapabilityDefinition
            {
                Id = "web",
                Name = "查询",
                Kind = ContextMenuCapabilityKinds.Web,
                Web = new WebCapabilityConfig()
            },
            new RecordingDialogService());

        viewModel.UrlTemplate = "https://example.com/?name={ShortcutName}";
        viewModel.SaveCommand.Execute(null);

        Assert.Contains("%E7%A4%BA%E4%BE%8B", viewModel.UrlPreview, StringComparison.Ordinal);
        Assert.NotNull(viewModel.Result);
        Assert.Equal(viewModel.UrlTemplate, viewModel.Result!.Web.UrlTemplate);
    }

    [Fact]
    public void Web_template_preview_uses_realistic_target_identity()
    {
        var viewModel = new ContextMenuCapabilityEditorViewModel(
            new ContextMenuCapabilityDefinition
            {
                Id = "web",
                Name = "设备页面",
                Kind = ContextMenuCapabilityKinds.Web,
                Web = new WebCapabilityConfig()
            },
            new RecordingDialogService());

        viewModel.UrlTemplate = "https://example.com/?instance={InstanceId}&code={DeviceCode}&number={DeviceNumber}";

        Assert.Equal(
            "https://example.com/?instance=3134&code=TSSP001&number=001",
            viewModel.UrlPreview);
    }

    [Fact]
    public void PowerShell_save_requires_security_confirmation()
    {
        var dialog = new RecordingDialogService(false);
        var viewModel = new ContextMenuCapabilityEditorViewModel(
            new ContextMenuCapabilityDefinition
            {
                Id = "ps",
                Name = "命令",
                Kind = ContextMenuCapabilityKinds.PowerShell,
                PowerShell = new PowerShellCapabilityConfig { Script = "Write-Output 1" }
            },
            dialog);

        viewModel.SaveCommand.Execute(null);

        Assert.Null(viewModel.Result);
        Assert.Contains("读取、修改或删除", dialog.LastConfirmation, StringComparison.Ordinal);
    }

    private sealed class RecordingDialogService(params bool[] confirmations) : DialogService
    {
        private readonly Queue<bool> confirmationResults = new(confirmations);

        public string LastConfirmation { get; private set; } = string.Empty;

        public override bool Confirm(string message)
        {
            LastConfirmation = message;
            return confirmationResults.Count == 0 || confirmationResults.Dequeue();
        }

        public override void ShowError(string message)
        {
            throw new Xunit.Sdk.XunitException(message);
        }
    }
}
