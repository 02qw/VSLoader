using System.IO;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class UpdateCheckService
{
    private readonly PathAccessPreflightService pathAccessPreflightService;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public UpdateCheckService()
        : this(new PathAccessPreflightService())
    {
    }

    public UpdateCheckService(PathAccessPreflightService pathAccessPreflightService)
    {
        this.pathAccessPreflightService = pathAccessPreflightService;
    }

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

        changed |= CheckGlobalConfigPackage(config.GlobalConfigPackagePath, state.GlobalConfig, result);
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

    public async Task<UpdateCheckResult> CheckAsync(
        UpdateCheckConfig config,
        string updateTimePath,
        Version currentVersion,
        string softwareUpdateManifestPath = "",
        CancellationToken cancellationToken = default)
    {
        var preflight = await CheckConfiguredFilesAsync(config, softwareUpdateManifestPath, cancellationToken);
        var safeConfig = config.Clone();
        var safeManifestPath = softwareUpdateManifestPath;

        if (preflight.GlobalConfigFailed)
        {
            safeConfig.GlobalConfigPackagePath = string.Empty;
        }

        if (preflight.ManifestFailed)
        {
            safeManifestPath = string.Empty;
        }

        var result = Check(safeConfig, updateTimePath, currentVersion, safeManifestPath);
        foreach (var failure in preflight.Failures)
        {
            result.Failures.Add(failure);
        }

        return result;
    }

    public SaveResult MarkRulesUsed(UpdateCheckConfig config, string updateTimePath)
    {
        return SaveResult.Ok();
    }

    public SaveResult MarkMapUsed(string mapFilePath, string updateTimePath)
    {
        return SaveResult.Ok();
    }

    public SaveResult MarkGlobalConfigUsed(string packagePath, string updateTimePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return SaveResult.Ok();
            }

            if (!File.Exists(packagePath))
            {
                return SaveResult.Fail("全局配置包不存在，无法更新基线。");
            }

            var packageInfo = ReadGlobalConfigPackageInfo(packagePath);
            var loadResult = LoadState(updateTimePath);
            if (!loadResult.Success)
            {
                return SaveResult.Fail(loadResult.ErrorMessage ?? "updateTime.json 读取失败");
            }

            loadResult.State.GlobalConfig.LastSeenWriteTimeUtc = packageInfo.WriteTimeUtc;
            loadResult.State.GlobalConfig.LastUsedExportedAt = packageInfo.ExportedAt;
            return SaveState(updateTimePath, loadResult.State);
        }
        catch (Exception ex)
        {
            return SaveResult.Fail(ex.Message);
        }
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
        if (result.DetectedGlobalConfigExportedAt is null &&
            result.DetectedGlobalConfigWriteTimeUtc is null &&
            result.DetectedRulesWriteTimeUtc is null &&
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

        if (result.DetectedGlobalConfigExportedAt is not null)
        {
            loadResult.State.GlobalConfig.LastUsedExportedAt = result.DetectedGlobalConfigExportedAt;
        }

        if (result.DetectedGlobalConfigWriteTimeUtc is not null)
        {
            loadResult.State.GlobalConfig.LastSeenWriteTimeUtc = result.DetectedGlobalConfigWriteTimeUtc;
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

    private bool CheckGlobalConfigPackage(
        string packagePath,
        UpdateGlobalConfigState state,
        UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return false;
        }

        try
        {
            if (!File.Exists(packagePath))
            {
                result.Failures.Add("全局配置包文件不存在");
                return false;
            }

            var writeTimeUtc = File.GetLastWriteTimeUtc(packagePath);
            if (state.LastSeenWriteTimeUtc == writeTimeUtc)
            {
                return false;
            }

            var packageInfo = ReadGlobalConfigPackageInfo(packagePath, writeTimeUtc);
            if (state.LastUsedExportedAt is null)
            {
                state.LastSeenWriteTimeUtc = packageInfo.WriteTimeUtc;
                state.LastUsedExportedAt = packageInfo.ExportedAt;
                return true;
            }

            if (packageInfo.ExportedAt > state.LastUsedExportedAt.Value)
            {
                result.UpdatedItems.Add("全局配置");
                result.DetectedGlobalConfigExportedAt = packageInfo.ExportedAt;
                result.DetectedGlobalConfigWriteTimeUtc = packageInfo.WriteTimeUtc;
                return false;
            }

            state.LastSeenWriteTimeUtc = packageInfo.WriteTimeUtc;
            return true;
        }
        catch (Exception ex)
        {
            result.Failures.Add($"全局配置包格式无效：{ex.Message}");
            return false;
        }
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

    private async Task<UpdateCheckPreflightResult> CheckConfiguredFilesAsync(
        UpdateCheckConfig config,
        string softwareUpdateManifestPath,
        CancellationToken cancellationToken)
    {
        var result = new UpdateCheckPreflightResult();

        result.GlobalConfigFailed = await PreflightFileAsync(
            config.GlobalConfigPackagePath,
            "全局配置包不可访问",
            result,
            cancellationToken);

        result.ManifestFailed = await PreflightFileAsync(
            softwareUpdateManifestPath,
            "软件更新 manifest 不可访问",
            result,
            cancellationToken);

        return result;
    }

    private async Task<bool> PreflightFileAsync(
        string filePath,
        string label,
        UpdateCheckPreflightResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var preflight = await pathAccessPreflightService.CheckFileAsync(filePath);
        if (!preflight.Success)
        {
            result.Failures.Add($"{label}：{preflight.ErrorMessage}");
            return true;
        }

        return false;
    }

    private GlobalConfigPackageInfo ReadGlobalConfigPackageInfo(string packagePath)
    {
        return ReadGlobalConfigPackageInfo(packagePath, File.GetLastWriteTimeUtc(packagePath));
    }

    private GlobalConfigPackageInfo ReadGlobalConfigPackageInfo(string packagePath, DateTime writeTimeUtc)
    {
        var json = File.ReadAllText(packagePath);
        var package = JsonSerializer.Deserialize<GlobalConfigPackage>(json, jsonOptions)
            ?? throw new InvalidOperationException("内容为空。");

        if (!string.Equals(package.AppName, "VSLoader", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AppName 不是 VSLoader。");
        }

        if (package.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"SchemaVersion 不支持：{package.SchemaVersion}。");
        }

        if (string.IsNullOrWhiteSpace(package.ExportedAt) ||
            !DateTimeOffset.TryParse(package.ExportedAt.Trim(), out var exportedAt))
        {
            throw new InvalidOperationException("ExportedAt 为空或格式无效。");
        }

        return new GlobalConfigPackageInfo(exportedAt, writeTimeUtc);
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
        state.GlobalConfig ??= new UpdateGlobalConfigState();
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

    private sealed class UpdateCheckPreflightResult
    {
        public List<string> Failures { get; } = [];

        public bool GlobalConfigFailed { get; set; }

        public bool ManifestFailed { get; set; }
    }

    private sealed record GlobalConfigPackageInfo(DateTimeOffset ExportedAt, DateTime WriteTimeUtc);
}
