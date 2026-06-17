using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class UpdateCheckService
{
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    public UpdateCheckResult Check(UpdateCheckConfig config, string updateTimePath, Version currentVersion, string softwareUpdateManifestPath = "")
    {
        var result = new UpdateCheckResult();
        var loadResult = LoadState(updateTimePath);

        if (!loadResult.Success)
        {
            result.Failures.Add(loadResult.ErrorMessage ?? "updateTime.json 读取失败");
            return result;
        }

        var state = loadResult.State;
        var changed = false;

        changed |= CheckFile(
            config.RulesFilePath,
            state.Rules,
            "批量规则文件",
            "rules 文件不存在",
            result,
            value => result.DetectedRulesWriteTimeUtc = value);

        changed |= CheckFile(
            config.MapFilePath,
            state.Map,
            "地图配置文件",
            "map 文件不存在",
            result,
            value => result.DetectedMapWriteTimeUtc = value);

        changed |= CheckSoftwareManifest(softwareUpdateManifestPath, state.Software, currentVersion, result);

        if (changed)
        {
            var saveResult = SaveState(updateTimePath, state);
            if (!saveResult.Success)
            {
                result.Failures.Add($"updateTime.json 保存失败：{saveResult.ErrorMessage}");
            }
        }

        return result;
    }

    public SaveResult MarkRulesUsed(UpdateCheckConfig config, string updateTimePath)
    {
        if (string.IsNullOrWhiteSpace(config.RulesFilePath))
        {
            return SaveResult.Ok();
        }

        return MarkFileUsed(config.RulesFilePath, updateTimePath, state => state.Rules);
    }

    public SaveResult MarkMapUsed(string mapFilePath, string updateTimePath)
    {
        if (string.IsNullOrWhiteSpace(mapFilePath))
        {
            return SaveResult.Ok();
        }

        return MarkFileUsed(mapFilePath, updateTimePath, state => state.Map);
    }

    public SaveResult MarkSoftwareCurrent(UpdateCheckConfig config, string updateTimePath, Version currentVersion)
    {
        var loadResult = LoadState(updateTimePath);
        if (!loadResult.Success)
        {
            return SaveResult.Fail(loadResult.ErrorMessage ?? "updateTime.json 读取失败");
        }

        loadResult.State.Software.LastUsedVersion = FormatVersion(currentVersion);
        return SaveState(updateTimePath, loadResult.State);
    }

    public SaveResult AcknowledgeDetectedUpdates(string updateTimePath, UpdateCheckResult result)
    {
        if (result.DetectedRulesWriteTimeUtc is null &&
            result.DetectedMapWriteTimeUtc is null &&
            string.IsNullOrWhiteSpace(result.DetectedSoftwareVersion))
        {
            return SaveResult.Ok();
        }

        var loadResult = LoadState(updateTimePath);
        if (!loadResult.Success)
        {
            return SaveResult.Fail(loadResult.ErrorMessage ?? "updateTime.json 读取失败");
        }

        if (result.DetectedRulesWriteTimeUtc is not null)
        {
            loadResult.State.Rules.LastUsedWriteTimeUtc = result.DetectedRulesWriteTimeUtc;
        }

        if (result.DetectedMapWriteTimeUtc is not null)
        {
            loadResult.State.Map.LastUsedWriteTimeUtc = result.DetectedMapWriteTimeUtc;
        }

        if (!string.IsNullOrWhiteSpace(result.DetectedSoftwareVersion))
        {
            loadResult.State.Software.LastUsedVersion = result.DetectedSoftwareVersion.Trim();
        }

        return SaveState(updateTimePath, loadResult.State);
    }

    private static bool CheckFile(
        string filePath,
        UpdateFileState state,
        string updateLabel,
        string missingLabel,
        UpdateCheckResult result,
        Action<DateTime> setDetectedWriteTimeUtc)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                result.Failures.Add(missingLabel);
                return false;
            }

            var writeTimeUtc = File.GetLastWriteTimeUtc(filePath);
            if (state.LastUsedWriteTimeUtc is null)
            {
                state.LastUsedWriteTimeUtc = writeTimeUtc;
                return true;
            }

            if (writeTimeUtc > state.LastUsedWriteTimeUtc.Value)
            {
                result.UpdatedItems.Add(updateLabel);
                setDetectedWriteTimeUtc(writeTimeUtc);
            }
        }
        catch (Exception ex)
        {
            result.Failures.Add($"{updateLabel}读取失败：{ex.Message}");
        }

        return false;
    }

    private static bool CheckSoftwareManifest(
        string manifestPath,
        UpdateSoftwareState state,
        Version currentVersion,
        UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return false;
        }

        try
        {
            if (!File.Exists(manifestPath))
            {
                result.Failures.Add("软件更新 manifest 文件不存在");
                return false;
            }

            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<SoftwareUpdateManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            var versionText = manifest?.Version?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(versionText))
            {
                result.Failures.Add("manifest version 为空");
                return false;
            }

            if (!Version.TryParse(versionText, out var latestVersion))
            {
                result.Failures.Add("manifest version 格式无效");
                return false;
            }

            var comparison = latestVersion.CompareTo(currentVersion);
            if (comparison > 0)
            {
                if (Version.TryParse(state.LastUsedVersion, out var acknowledgedVersion) &&
                    latestVersion.CompareTo(acknowledgedVersion) <= 0)
                {
                    return false;
                }

                result.UpdatedItems.Add("软件版本");
                result.DetectedSoftwareVersion = FormatVersion(latestVersion);
                return false;
            }

            if (comparison == 0 && state.LastUsedVersion != FormatVersion(currentVersion))
            {
                state.LastUsedVersion = FormatVersion(currentVersion);
                return true;
            }
        }
        catch (Exception ex)
        {
            result.Failures.Add($"manifest 读取失败：{ex.Message}");
        }

        return false;
    }

    private SaveResult MarkFileUsed(
        string filePath,
        string updateTimePath,
        Func<UpdateTimeState, UpdateFileState> selectState)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return SaveResult.Fail("文件不存在，无法更新基线。");
            }

            var loadResult = LoadState(updateTimePath);
            if (!loadResult.Success)
            {
                return SaveResult.Fail(loadResult.ErrorMessage ?? "updateTime.json 读取失败");
            }

            selectState(loadResult.State).LastUsedWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
            return SaveState(updateTimePath, loadResult.State);
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }

    private UpdateTimeLoadResult LoadState(string updateTimePath)
    {
        if (!File.Exists(updateTimePath))
        {
            return UpdateTimeLoadResult.Ok(new UpdateTimeState());
        }

        try
        {
            var json = File.ReadAllText(updateTimePath);
            var state = JsonSerializer.Deserialize<UpdateTimeState>(json, jsonOptions);
            if (state is null)
            {
                return UpdateTimeLoadResult.Fail("updateTime.json 读取失败");
            }

            NormalizeState(state);
            return UpdateTimeLoadResult.Ok(state);
        }
        catch
        {
            return UpdateTimeLoadResult.Fail("updateTime.json 读取失败");
        }
    }

    private SaveResult SaveState(string updateTimePath, UpdateTimeState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(updateTimePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            NormalizeState(state);
            File.WriteAllText(updateTimePath, JsonSerializer.Serialize(state, jsonOptions));
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
    }

    private static void NormalizeState(UpdateTimeState state)
    {
        state.Rules ??= new UpdateFileState();
        state.Map ??= new UpdateFileState();
        state.Software ??= new UpdateSoftwareState();
        state.Software.LastUsedVersion ??= string.Empty;
    }

    private static string FormatVersion(Version version)
    {
        return version.Revision >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
            : version.Build >= 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : $"{version.Major}.{version.Minor}";
    }

    private sealed class UpdateTimeLoadResult
    {
        private UpdateTimeLoadResult(UpdateTimeState state, bool success, string? errorMessage)
        {
            State = state;
            Success = success;
            ErrorMessage = errorMessage;
        }

        public UpdateTimeState State { get; }

        public bool Success { get; }

        public string? ErrorMessage { get; }

        public static UpdateTimeLoadResult Ok(UpdateTimeState state)
        {
            return new UpdateTimeLoadResult(state, true, null);
        }

        public static UpdateTimeLoadResult Fail(string errorMessage)
        {
            return new UpdateTimeLoadResult(new UpdateTimeState(), false, errorMessage);
        }
    }
}
