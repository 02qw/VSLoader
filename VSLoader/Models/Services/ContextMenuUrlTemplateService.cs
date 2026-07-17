using System.Text.RegularExpressions;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed partial class ContextMenuUrlTemplateService
{
    private const int MaximumUrlLength = 8192;

    [GeneratedRegex("\\{([A-Za-z][A-Za-z0-9]*)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    public ContextMenuUrlTemplateResult Build(string? template, ContextMenuCapabilityExecutionContext context)
    {
        var value = template?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return ContextMenuUrlTemplateResult.Fail("URL 模板不能为空。");
        }

        var variables = ContextMenuCapabilityVariableService.BuildTemplateVariables(context);
        var matches = PlaceholderRegex().Matches(value);
        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value;
            if (!variables.ContainsKey(name))
            {
                return ContextMenuUrlTemplateResult.Fail($"URL 模板包含不支持的变量：{{{name}}}。");
            }

            if (string.IsNullOrWhiteSpace(variables[name])
                && TryBuildMissingTargetIdentityError(name, variables["TargetName"], out var errorMessage))
            {
                return ContextMenuUrlTemplateResult.Fail(errorMessage);
            }
        }

        var withoutKnownPlaceholders = PlaceholderRegex().Replace(value, string.Empty);
        if (withoutKnownPlaceholders.Contains('{') || withoutKnownPlaceholders.Contains('}'))
        {
            return ContextMenuUrlTemplateResult.Fail("URL 模板中的变量括号格式无效。");
        }

        var url = PlaceholderRegex().Replace(value, match =>
            Uri.EscapeDataString(variables[match.Groups[1].Value]));
        if (url.Length > MaximumUrlLength)
        {
            return ContextMenuUrlTemplateResult.Fail($"生成的 URL 超过 {MaximumUrlLength} 个字符。");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return ContextMenuUrlTemplateResult.Fail("URL 模板没有生成有效的绝对地址。");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return ContextMenuUrlTemplateResult.Fail("Web 能力仅允许 http 和 https 地址。");
        }

        return ContextMenuUrlTemplateResult.Ok(url);
    }

    private static bool TryBuildMissingTargetIdentityError(
        string variableName,
        string targetName,
        out string errorMessage)
    {
        var safeTargetName = FormatTargetName(targetName);
        errorMessage = variableName switch
        {
            "TargetName" =>
                "无法从当前目标路径提取变量 {TargetName}。目标末级名称不能为空。",
            "InstanceId" =>
                $"无法从目标末级名称“{safeTargetName}”提取变量 {{InstanceId}}。"
                + "实例编号必须是最后一个下划线之前的纯数字内容，例如 3134_TSSP001。",
            "DeviceCode" or "DeviceType" or "DeviceNumber" =>
                $"无法从目标末级名称“{safeTargetName}”提取变量 {{{variableName}}}。"
                + "设备 ID 应为英文字母加数字，例如 TSSP001。",
            _ => string.Empty
        };
        return !string.IsNullOrEmpty(errorMessage);
    }

    private static string FormatTargetName(string targetName)
    {
        const int maximumDisplayLength = 80;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return "空";
        }

        return targetName.Length <= maximumDisplayLength
            ? targetName
            : targetName[..maximumDisplayLength] + "...";
    }
}
