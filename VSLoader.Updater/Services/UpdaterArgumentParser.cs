using System.IO;

namespace VSLoader.Updater.Services;

public static class UpdaterArgumentParser
{
    public static UpdaterArgumentParseResult Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                return UpdaterArgumentParseResult.Fail($"参数格式无效：{args[i]}");
            }

            if (i + 1 >= args.Length)
            {
                return UpdaterArgumentParseResult.Fail($"参数缺少值：{args[i]}");
            }

            values[args[i]] = args[i + 1];
        }

        var mode = TryGet(values, "--mode", out var modeText) && !string.IsNullOrWhiteSpace(modeText)
            ? modeText.Trim()
            : "apply";

        if (!mode.Equals("apply", StringComparison.OrdinalIgnoreCase)
            && !mode.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            return UpdaterArgumentParseResult.Fail("mode 无效。");
        }

        if (!TryGet(values, "--processId", out var processIdText)
            || !int.TryParse(processIdText, out var processId)
            || processId <= 0)
        {
            return UpdaterArgumentParseResult.Fail("processId 无效。");
        }

        if (!TryGet(values, "--targetDir", out var targetDir)
            || string.IsNullOrWhiteSpace(targetDir)
            || !Directory.Exists(targetDir))
        {
            return UpdaterArgumentParseResult.Fail("targetDir 不存在。");
        }

        if (!TryGet(values, "--mainExeName", out var mainExeName)
            || string.IsNullOrWhiteSpace(mainExeName))
        {
            return UpdaterArgumentParseResult.Fail("mainExeName 无效。");
        }

        if (!TryGet(values, "--updatesRoot", out var updatesRoot)
            || string.IsNullOrWhiteSpace(updatesRoot))
        {
            return UpdaterArgumentParseResult.Fail("updatesRoot 无效。");
        }

        if (mode.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGet(values, "--manifestPath", out var manifestPath)
                || string.IsNullOrWhiteSpace(manifestPath)
                || !File.Exists(manifestPath))
            {
                return UpdaterArgumentParseResult.Fail("manifestPath 无效。");
            }

            if (!TryGet(values, "--currentVersion", out var currentVersionText)
                || !Version.TryParse(currentVersionText, out var currentVersion))
            {
                return UpdaterArgumentParseResult.Fail("currentVersion 无效。");
            }

            return UpdaterArgumentParseResult.Ok(new UpdaterOptions
            {
                Mode = "update",
                ProcessId = processId,
                TargetDirectory = targetDir,
                MainExeName = mainExeName,
                UpdatesRoot = updatesRoot,
                ManifestPath = manifestPath,
                CurrentVersion = currentVersion
            });
        }

        if (!TryGet(values, "--stagingDir", out var stagingDir)
            || string.IsNullOrWhiteSpace(stagingDir)
            || !Directory.Exists(stagingDir))
        {
            return UpdaterArgumentParseResult.Fail("stagingDir 不存在。");
        }

        if (!File.Exists(Path.Combine(stagingDir, mainExeName)))
        {
            return UpdaterArgumentParseResult.Fail($"stagingDir 缺少 {mainExeName}。");
        }

        return UpdaterArgumentParseResult.Ok(new UpdaterOptions
        {
            Mode = "apply",
            ProcessId = processId,
            TargetDirectory = targetDir,
            StagingDirectory = stagingDir,
            MainExeName = mainExeName,
            UpdatesRoot = updatesRoot
        });
    }

    private static bool TryGet(Dictionary<string, string> values, string key, out string value)
    {
        return values.TryGetValue(key, out value!);
    }
}
