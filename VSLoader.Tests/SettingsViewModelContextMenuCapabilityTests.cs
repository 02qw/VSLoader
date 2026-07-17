using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class SettingsViewModelContextMenuCapabilityTests
{
    [Fact]
    public void Constructor_clones_capabilities_and_move_does_not_mutate_source()
    {
        var source = ContextMenuCapabilityDefaults.Create();
        var viewModel = CreateViewModel(source);
        var webUi = viewModel.ContextMenuCapabilityItems.Single(item =>
            item.Definition.BuiltInActionId == ContextMenuBuiltInActionIds.OpenWebUi);

        viewModel.MoveCapabilityUpCommand.Execute(webUi);

        Assert.Equal(ContextMenuBuiltInActionIds.OpenWebUi, viewModel.ContextMenuCapabilityItems[0].Definition.BuiltInActionId);
        Assert.Equal(ContextMenuBuiltInActionIds.OpenVsCode, source.Items[0].BuiltInActionId);
    }

    [Fact]
    public void AddPowerShell_uses_editor_result_and_assigns_unique_id()
    {
        var viewModel = CreateViewModel(ContextMenuCapabilityDefaults.Create());
        viewModel.EditContextMenuCapability = definition =>
        {
            definition.Name = "打开目录";
            definition.PowerShell.Script = "explorer $env:VSL_TARGET_PATH";
            return definition;
        };

        viewModel.AddPowerShellCapabilityCommand.Execute(null);

        var custom = Assert.Single(
            viewModel.ContextMenuCapabilityItems,
            item => item.Definition.Kind == ContextMenuCapabilityKinds.PowerShell);
        Assert.Equal("打开目录", custom.Definition.Name);
        Assert.False(string.IsNullOrWhiteSpace(custom.Definition.Id));
        Assert.Contains(viewModel.PowerShellCapabilitiesApprovedForTrust, item => item.Id == custom.Definition.Id);
    }

    [Fact]
    public void RestoreDefaultOrder_keeps_custom_relative_order_after_builtins()
    {
        var config = ContextMenuCapabilityDefaults.Create();
        config.Items.Insert(0, CreateWeb("custom-a", "A"));
        config.Items.Insert(2, CreateWeb("custom-b", "B"));
        var viewModel = CreateViewModel(config);

        viewModel.RestoreDefaultCapabilityOrderCommand.Execute(null);

        Assert.Equal(
            ContextMenuBuiltInActionIds.All,
            viewModel.ContextMenuCapabilityItems.Take(4).Select(item => item.Definition.BuiltInActionId));
        Assert.Equal(
            ["custom-a", "custom-b"],
            viewModel.ContextMenuCapabilityItems.Skip(4).Select(item => item.Definition.Id));
    }

    [Fact]
    public void Save_rejects_invalid_custom_capability()
    {
        var dialog = new RecordingDialogService();
        var viewModel = CreateViewModel(ContextMenuCapabilityDefaults.Create(), dialog);
        viewModel.ContextMenuCapabilityItems.Add(new ContextMenuCapabilityListItemViewModel(
            new ContextMenuCapabilityDefinition
            {
                Id = "invalid",
                Name = "空命令",
                Kind = ContextMenuCapabilityKinds.PowerShell,
                PowerShell = new PowerShellCapabilityConfig { Script = string.Empty }
            }));

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Saved);
        Assert.Contains("脚本不能为空", dialog.LastError, StringComparison.Ordinal);
    }

    private static SettingsViewModel CreateViewModel(
        ContextMenuCapabilityCollectionConfig capabilities,
        DialogService? dialogService = null)
    {
        return new SettingsViewModel(
            Environment.ProcessPath!,
            string.Empty,
            new AdminUiConfig(),
            new WebUiConfig { BaseUrl = "https://example.com", InstancePropertiesName = "a.properties", InstanceNameKey = "name", SslPortKey = "port" },
            new UpdateCheckConfig(),
            new HotkeyConfig(),
            new MapHotkeyConfig(),
            dialogService ?? new RecordingDialogService(),
            new PasswordProtectionService(),
            null,
            capabilities);
    }

    private static ContextMenuCapabilityDefinition CreateWeb(string id, string name)
    {
        return new ContextMenuCapabilityDefinition
        {
            Id = id,
            Name = name,
            Kind = ContextMenuCapabilityKinds.Web,
            RequiresExistingTargetPath = false,
            Web = new WebCapabilityConfig { UrlTemplate = "https://example.com" }
        };
    }

    private sealed class RecordingDialogService : DialogService
    {
        public string LastError { get; private set; } = string.Empty;

        public override void ShowError(string message)
        {
            LastError = message;
        }
    }
}
