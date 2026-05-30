using System.Diagnostics;
using System.IO;

namespace VSLoader.Services;

public sealed class VSCodeLauncherService
{
    public LaunchResult Launch(string vscodePath, string targetPath)
    {
        if (!IsValidExecutablePath(vscodePath))
        {
            return LaunchResult.Fail("VSCode 路径无效，请进入设置重新选择 .exe 文件。");
        }

        if (!PathExists(targetPath))
        {
            return LaunchResult.Fail("目标路径不存在或当前不可访问。");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = vscodePath,
                UseShellExecute = false
            };

            startInfo.ArgumentList.Add(targetPath);
            Process.Start(startInfo);
            return LaunchResult.Ok();
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail(ex.Message);
        }
    }

    public static bool IsValidExecutablePath(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && File.Exists(path)
            && string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNetworkPath(string path)
    {
        return path.TrimStart().StartsWith(@"\\", StringComparison.Ordinal);
    }

    public static bool PathExists(string path)
    {
        return Directory.Exists(path) || File.Exists(path);
    }
}
