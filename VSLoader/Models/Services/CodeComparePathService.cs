using System.IO;

namespace VSLoader.Services;

public static class CodeComparePathService
{
    public static bool TryNormalizeScope(string scope, out string normalizedScope, out string errorMessage)
    {
        normalizedScope = string.Empty;
        errorMessage = string.Empty;
        var value = string.IsNullOrWhiteSpace(scope) ? @"config\deo" : scope.Trim();
        if (Path.IsPathRooted(value))
        {
            errorMessage = "默认扫描范围必须是模块目录内的相对路径。";
            return false;
        }

        var segments = value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            errorMessage = "默认扫描范围无效，不能使用 . 或 ..。";
            return false;
        }

        normalizedScope = string.Join(Path.DirectorySeparatorChar, segments);
        return true;
    }

    public static bool TryResolveLocalModulePath(
        string rootPath,
        string moduleName,
        out string localModulePath,
        out string errorMessage)
    {
        localModulePath = string.Empty;
        errorMessage = string.Empty;
        var root = rootPath?.Trim() ?? string.Empty;
        var module = moduleName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(root))
        {
            errorMessage = "未配置本地代码模块根目录，请先在设置中配置。";
            return false;
        }

        if (!Directory.Exists(root))
        {
            errorMessage = $"本地代码模块根目录不存在或不可访问：{root}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(module))
        {
            errorMessage = "当前快捷项没有配置 SourceModuleName，无法定位本地代码模块。";
            return false;
        }

        if (Path.IsPathRooted(module)
            || module.Contains(Path.DirectorySeparatorChar)
            || module.Contains(Path.AltDirectorySeparatorChar)
            || module is "." or "..")
        {
            errorMessage = $"SourceModuleName 不是安全的模块目录名：{module}";
            return false;
        }

        try
        {
            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(fullRoot, module));
            var rootPrefix = fullRoot + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"本地模块路径超出配置的根目录：{module}";
                return false;
            }

            if (!Directory.Exists(candidate))
            {
                errorMessage = $"没有找到本地代码模块：{candidate}";
                return false;
            }

            localModulePath = candidate;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"解析本地代码模块路径失败：{ex.Message}";
            return false;
        }
    }
}
