using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ContextMenuCapabilityConfigServiceTests
{
    private readonly ContextMenuCapabilityConfigService service = new();

    [Fact]
    public void CreateDefault_uses_current_builtin_menu_order()
    {
        var config = service.CreateDefault();

        Assert.Equal(
            [
                ContextMenuBuiltInActionIds.OpenVsCode,
                ContextMenuBuiltInActionIds.OpenWebUi,
                ContextMenuBuiltInActionIds.OpenAdminUi,
                ContextMenuBuiltInActionIds.DownloadAdminUiLink
            ],
            config.Items.Select(item => item.BuiltInActionId));
        Assert.Equal([0, 10, 20, 30], config.Items.Select(item => item.Order));
        Assert.All(config.Items, item => Assert.Equal(ContextMenuCapabilityKinds.BuiltIn, item.Kind));
    }

    [Fact]
    public void Normalize_adds_missing_builtin_without_removing_custom_capability()
    {
        var custom = new ContextMenuCapabilityDefinition
        {
            Id = "custom-1",
            Name = "打开目录",
            Kind = ContextMenuCapabilityKinds.PowerShell,
            Order = 5,
            PowerShell = new PowerShellCapabilityConfig { Script = "explorer $env:VSL_TARGET_PATH" }
        };
        var config = new ContextMenuCapabilityCollectionConfig { Items = [custom] };

        var warnings = service.Normalize(config);

        Assert.Empty(warnings);
        Assert.Contains(config.Items, item => item.Id == "custom-1");
        Assert.Equal(4, config.Items.Count(item => item.Kind == ContextMenuCapabilityKinds.BuiltIn));
        Assert.Equal(5, config.Items.Count);
        Assert.Equal([0, 10, 20, 30, 40], config.Items.Select(item => item.Order));
    }

    [Fact]
    public void Normalize_repairs_duplicate_custom_ids_and_disables_unknown_kind()
    {
        var config = new ContextMenuCapabilityCollectionConfig
        {
            Items =
            [
                new ContextMenuCapabilityDefinition
                {
                    Id = "duplicate",
                    Name = "命令一",
                    Kind = ContextMenuCapabilityKinds.PowerShell,
                    PowerShell = new PowerShellCapabilityConfig { Script = "Write-Output 1" }
                },
                new ContextMenuCapabilityDefinition
                {
                    Id = "duplicate",
                    Name = "未知能力",
                    Kind = "future-kind",
                    Enabled = true
                }
            ]
        };

        var warnings = service.Normalize(config);

        Assert.Equal(config.Items.Count, config.Items.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var unknown = Assert.Single(config.Items, item => item.Name == "未知能力");
        Assert.False(unknown.Enabled);
        Assert.Contains(warnings, warning => warning.Contains("不支持", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_prevents_custom_capability_from_using_reserved_builtin_id()
    {
        var config = new ContextMenuCapabilityCollectionConfig
        {
            Items =
            [
                new ContextMenuCapabilityDefinition
                {
                    Id = ContextMenuBuiltInActionIds.OpenVsCode,
                    Name = "伪装能力",
                    Kind = ContextMenuCapabilityKinds.Web,
                    Web = new WebCapabilityConfig { UrlTemplate = "https://example.com" }
                }
            ]
        };

        service.Normalize(config);

        Assert.Equal(config.Items.Count, config.Items.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var custom = Assert.Single(config.Items, item => item.Name == "伪装能力");
        Assert.NotEqual(ContextMenuBuiltInActionIds.OpenVsCode, custom.Id);
    }

    [Fact]
    public void Normalize_future_schema_falls_back_to_default_builtins()
    {
        var config = new ContextMenuCapabilityCollectionConfig
        {
            SchemaVersion = 99,
            Items =
            [
                new ContextMenuCapabilityDefinition
                {
                    Id = "future-command",
                    Name = "未来命令",
                    Kind = ContextMenuCapabilityKinds.PowerShell,
                    PowerShell = new PowerShellCapabilityConfig { Script = "Write-Output future" }
                }
            ]
        };

        var warnings = service.Normalize(config);

        Assert.Equal(1, config.SchemaVersion);
        Assert.Equal(ContextMenuBuiltInActionIds.All, config.Items.Select(item => item.BuiltInActionId));
        Assert.Contains(warnings, warning => warning.Contains("版本", StringComparison.Ordinal));
    }

    [Fact]
    public void GetVisible_filters_by_enabled_state_and_surface()
    {
        var config = new ContextMenuCapabilityCollectionConfig
        {
            Items =
            [
                new ContextMenuCapabilityDefinition { Id = "main", Name = "主界面", Kind = ContextMenuCapabilityKinds.Web, Order = 20, ShowInShortcutList = true, ShowInFactoryMap = false },
                new ContextMenuCapabilityDefinition { Id = "map", Name = "地图", Kind = ContextMenuCapabilityKinds.Web, Order = 10, ShowInShortcutList = false, ShowInFactoryMap = true },
                new ContextMenuCapabilityDefinition { Id = "off", Name = "停用", Kind = ContextMenuCapabilityKinds.Web, Order = 0, Enabled = false }
            ]
        };

        Assert.Equal(["main"], service.GetVisible(config, ContextMenuCapabilitySurfaces.ShortcutList).Select(item => item.Id));
        Assert.Equal(["map"], service.GetVisible(config, ContextMenuCapabilitySurfaces.FactoryMap).Select(item => item.Id));
    }

    [Fact]
    public void Validate_rejects_empty_powershell_script_and_invalid_web_scheme()
    {
        var powershell = new ContextMenuCapabilityDefinition
        {
            Id = "ps",
            Name = "空命令",
            Kind = ContextMenuCapabilityKinds.PowerShell,
            PowerShell = new PowerShellCapabilityConfig { Script = " " }
        };
        var web = new ContextMenuCapabilityDefinition
        {
            Id = "web",
            Name = "危险网页",
            Kind = ContextMenuCapabilityKinds.Web,
            Web = new WebCapabilityConfig { UrlTemplate = "javascript:alert(1)" }
        };

        Assert.False(service.Validate(powershell).Success);
        Assert.False(service.Validate(web).Success);
    }

    [Fact]
    public void Validate_accepts_web_templates_that_use_target_identity_variables()
    {
        var web = new ContextMenuCapabilityDefinition
        {
            Id = "web-device",
            Name = "设备页面",
            Kind = ContextMenuCapabilityKinds.Web,
            Web = new WebCapabilityConfig
            {
                UrlTemplate = "https://example.com/?instance={InstanceId}&code={DeviceCode}&number={DeviceNumber}"
            }
        };

        var result = service.Validate(web);

        Assert.True(result.Success, result.ErrorMessage);
    }
}
