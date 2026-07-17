using VSLoader.Models;

namespace VSLoader.Services;

public sealed class ContextMenuCapabilityConfigService
{
    private readonly ContextMenuUrlTemplateService urlTemplateService;

    public ContextMenuCapabilityConfigService()
        : this(new ContextMenuUrlTemplateService())
    {
    }

    public ContextMenuCapabilityConfigService(ContextMenuUrlTemplateService urlTemplateService)
    {
        this.urlTemplateService = urlTemplateService;
    }

    public ContextMenuCapabilityCollectionConfig CreateDefault()
    {
        return ContextMenuCapabilityDefaults.Create();
    }

    public IReadOnlyList<string> Normalize(ContextMenuCapabilityCollectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var warnings = new List<string>();
        if (config.SchemaVersion != 1)
        {
            var defaults = CreateDefault();
            warnings.Add($"右键菜单能力集合版本不受支持：{config.SchemaVersion}，已恢复默认能力。");
            config.SchemaVersion = defaults.SchemaVersion;
            config.Items = defaults.Items;
            return warnings;
        }

        config.SchemaVersion = 1;
        config.Items ??= [];

        var normalized = new List<ContextMenuCapabilityDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builtIns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in config.Items.OrderBy(item => item?.Order ?? int.MaxValue))
        {
            if (source is null)
            {
                continue;
            }

            NormalizeItem(source);
            if (string.Equals(source.Kind, ContextMenuCapabilityKinds.BuiltIn, StringComparison.Ordinal))
            {
                if (!ContextMenuBuiltInActionIds.All.Contains(source.BuiltInActionId, StringComparer.OrdinalIgnoreCase))
                {
                    source.Enabled = false;
                    warnings.Add($"内建能力不受支持：{source.BuiltInActionId}。");
                }
                else if (!builtIns.Add(source.BuiltInActionId))
                {
                    warnings.Add($"已忽略重复内建能力：{source.BuiltInActionId}。");
                    continue;
                }
                else
                {
                    source.Id = source.BuiltInActionId;
                    source.Name = ContextMenuBuiltInActionIds.GetDisplayName(source.BuiltInActionId);
                }
            }
            else if (!ContextMenuCapabilityKinds.IsSupported(source.Kind))
            {
                source.Enabled = false;
                warnings.Add($"能力“{source.Name}”使用了不支持的类型：{source.Kind}。");
            }

            if (!string.Equals(source.Kind, ContextMenuCapabilityKinds.BuiltIn, StringComparison.Ordinal)
                && ContextMenuBuiltInActionIds.All.Contains(source.Id, StringComparer.OrdinalIgnoreCase))
            {
                source.Id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(source.Id) || !ids.Add(source.Id))
            {
                source.Id = Guid.NewGuid().ToString("N");
                ids.Add(source.Id);
            }

            normalized.Add(source);
        }

        foreach (var actionId in ContextMenuBuiltInActionIds.All)
        {
            if (builtIns.Contains(actionId))
            {
                continue;
            }

            var item = ContextMenuCapabilityDefaults.CreateBuiltIn(actionId, 0);
            normalized.Add(item);
            ids.Add(item.Id);
        }

        for (var index = 0; index < normalized.Count; index++)
        {
            normalized[index].Order = index * 10;
        }

        config.Items = normalized;
        return warnings;
    }

    public IReadOnlyList<ContextMenuCapabilityDefinition> GetVisible(
        ContextMenuCapabilityCollectionConfig config,
        string surface)
    {
        if (config?.Items is null)
        {
            return [];
        }

        return config.Items
            .Where(item => item is not null && item.Enabled)
            .Where(item => string.Equals(surface, ContextMenuCapabilitySurfaces.ShortcutList, StringComparison.Ordinal)
                ? item.ShowInShortcutList
                : string.Equals(surface, ContextMenuCapabilitySurfaces.FactoryMap, StringComparison.Ordinal)
                    && item.ShowInFactoryMap)
            .OrderBy(item => item.Order)
            .Select(item => item.Clone())
            .ToList();
    }

    public SaveResult Validate(ContextMenuCapabilityDefinition item)
    {
        if (item is null)
        {
            return SaveResult.Fail("能力配置为空。");
        }

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return SaveResult.Fail("能力名称不能为空。");
        }

        if (!item.ShowInShortcutList && !item.ShowInFactoryMap)
        {
            return SaveResult.Fail($"能力“{item.Name}”至少需要选择一个展示位置。");
        }

        if (string.Equals(item.Kind, ContextMenuCapabilityKinds.BuiltIn, StringComparison.Ordinal))
        {
            return ContextMenuBuiltInActionIds.All.Contains(item.BuiltInActionId, StringComparer.OrdinalIgnoreCase)
                ? SaveResult.Ok()
                : SaveResult.Fail($"内建能力不受支持：{item.BuiltInActionId}。");
        }

        if (string.Equals(item.Kind, ContextMenuCapabilityKinds.PowerShell, StringComparison.Ordinal))
        {
            var config = item.PowerShell ?? new PowerShellCapabilityConfig();
            if (string.IsNullOrWhiteSpace(config.Script))
            {
                return SaveResult.Fail($"PowerShell 能力“{item.Name}”的脚本不能为空。");
            }

            if (!PowerShellCapabilityExecutionModes.IsSupported(config.ExecutionMode))
            {
                return SaveResult.Fail($"PowerShell 能力“{item.Name}”的执行模式无效。");
            }

            if (!PowerShellCapabilityWorkingDirectoryModes.IsSupported(config.WorkingDirectoryMode))
            {
                return SaveResult.Fail($"PowerShell 能力“{item.Name}”的工作目录模式无效。");
            }

            if (config.TimeoutSeconds is < 1 or > 300)
            {
                return SaveResult.Fail($"PowerShell 能力“{item.Name}”的超时必须在 1 到 300 秒之间。");
            }

            return SaveResult.Ok();
        }

        if (string.Equals(item.Kind, ContextMenuCapabilityKinds.Web, StringComparison.Ordinal))
        {
            var result = urlTemplateService.Build(
                item.Web?.UrlTemplate,
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
            return result.Success ? SaveResult.Ok() : SaveResult.Fail(result.ErrorMessage);
        }

        return !item.Enabled
            ? SaveResult.Ok()
            : SaveResult.Fail($"能力类型不受支持：{item.Kind}。");
    }

    public SaveResult Validate(ContextMenuCapabilityCollectionConfig config)
    {
        if (config?.Items is null)
        {
            return SaveResult.Fail("右键菜单能力集合为空。");
        }

        foreach (var item in config.Items)
        {
            var result = Validate(item);
            if (!result.Success)
            {
                return result;
            }
        }

        return SaveResult.Ok();
    }

    private static void NormalizeItem(ContextMenuCapabilityDefinition item)
    {
        item.Id = item.Id?.Trim() ?? string.Empty;
        item.Name = item.Name?.Trim() ?? string.Empty;
        item.Kind = item.Kind?.Trim() ?? string.Empty;
        item.BuiltInActionId = item.BuiltInActionId?.Trim() ?? string.Empty;
        item.PowerShell ??= new PowerShellCapabilityConfig();
        item.Web ??= new WebCapabilityConfig();
        item.PowerShell.Script ??= string.Empty;
        item.PowerShell.WorkingDirectoryMode = item.PowerShell.WorkingDirectoryMode?.Trim() ?? string.Empty;
        item.PowerShell.ExecutionMode = item.PowerShell.ExecutionMode?.Trim() ?? string.Empty;
        item.Web.UrlTemplate = item.Web.UrlTemplate?.Trim() ?? string.Empty;
        item.PowerShell.TimeoutSeconds = Math.Clamp(item.PowerShell.TimeoutSeconds, 1, 300);
    }
}
