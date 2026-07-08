using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VSLoader.Models;

namespace VSLoader.Services;

public sealed class SoftwareUpdateService
{
    private readonly PathAccessPreflightService pathAccessPreflightService;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SoftwareUpdateService()
        : this(new PathAccessPreflightService())
    {
    }

    public SoftwareUpdateService(PathAccessPreflightService pathAccessPreflightService)
    {
        this.pathAccessPreflightService = pathAccessPreflightService;
    }

    public async Task<SoftwareUpdateAvailabilityResult> CheckAvailabilityAsync(
        string manifestPath,
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return SoftwareUpdateAvailabilityResult.Fail("软件更新 manifest 路径不能为空。");
        }

        var manifestPreflight = await pathAccessPreflightService.CheckFileAsync(manifestPath);
        if (!manifestPreflight.Success)
        {
            return SoftwareUpdateAvailabilityResult.Fail(ToManifestAccessError(manifestPreflight.ErrorMessage));
        }

        try
        {
            var manifest = await LoadManifestAsync(manifestPath, cancellationToken);
            var validationError = ValidateManifestVersion(manifest, out var remoteVersion);
            if (!string.IsNullOrEmpty(validationError))
            {
                return SoftwareUpdateAvailabilityResult.Fail(validationError);
            }

            return remoteVersion > currentVersion
                ? SoftwareUpdateAvailabilityResult.OkUpdate($"检测到新版本：{remoteVersion}", manifest.ReleaseNotes)
                : SoftwareUpdateAvailabilityResult.OkNoUpdate("当前已是最新版本。", manifest.ReleaseNotes);
        }
        catch (JsonException ex)
        {
            return SoftwareUpdateAvailabilityResult.Fail($"manifest 读取失败：{ex.Message}");
        }
        catch (Exception ex)
        {
            return SoftwareUpdateAvailabilityResult.Fail($"软件更新检查失败：{ex.Message}");
        }
    }

    public async Task<SoftwareUpdateResult> PrepareUpdateAsync(
        SoftwareUpdateRequest request,
        IProgress<SoftwareUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ManifestPath))
        {
            return SoftwareUpdateResult.Fail("软件更新 manifest 路径不能为空。");
        }

        var manifestPreflight = await pathAccessPreflightService.CheckFileAsync(request.ManifestPath);
        if (!manifestPreflight.Success)
        {
            return SoftwareUpdateResult.Fail(ToManifestAccessError(manifestPreflight.ErrorMessage));
        }

        try
        {
            Report(progress, 5, "正在读取更新信息...");
            var manifest = await LoadManifestAsync(request.ManifestPath, cancellationToken);
            var validationError = ValidateManifest(manifest, out var remoteVersion);
            if (!string.IsNullOrEmpty(validationError))
            {
                return SoftwareUpdateResult.Fail(validationError);
            }

            Report(progress, 15, "正在检查版本...");
            if (remoteVersion <= request.CurrentVersion)
            {
                return SoftwareUpdateResult.OkNoUpdate("当前已是最新版本。");
            }

            var packagePath = ResolvePackagePath(request.ManifestPath, manifest.PackageFile);
            var packagePreflight = await pathAccessPreflightService.CheckFileAsync(packagePath);
            if (!packagePreflight.Success)
            {
                return SoftwareUpdateResult.Fail(ToPackageAccessError(packagePreflight.ErrorMessage));
            }

            var downloadDirectory = Path.Combine(request.UpdatesRoot, "download");
            var stagingRoot = Path.Combine(request.UpdatesRoot, "staging");
            var stagingDirectory = Path.Combine(stagingRoot, FormatVersion(remoteVersion));
            PrepareCleanDirectory(downloadDirectory);
            PrepareCleanDirectory(stagingDirectory);

            Report(progress, 30, "正在复制更新包...");
            var downloadedPackagePath = Path.Combine(downloadDirectory, Path.GetFileName(packagePath));
            File.Copy(packagePath, downloadedPackagePath, true);

            Report(progress, 50, "正在校验更新包...");
            var actualSha256 = await ComputeSha256Async(downloadedPackagePath, cancellationToken);
            if (!string.Equals(actualSha256, manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return SoftwareUpdateResult.Fail("SHA256 校验失败。");
            }

            Report(progress, 70, "正在解压更新包...");
            ZipFile.ExtractToDirectory(downloadedPackagePath, stagingDirectory, true);

            var mainExePath = Path.Combine(stagingDirectory, request.MainExeName);
            if (!File.Exists(mainExePath))
            {
                return SoftwareUpdateResult.Fail($"更新包缺少 {request.MainExeName}。");
            }

            var updaterPath = Path.Combine(stagingDirectory, request.UpdaterExeName);
            if (!File.Exists(updaterPath))
            {
                return SoftwareUpdateResult.Fail($"更新包缺少 {request.UpdaterExeName}。");
            }

            Report(progress, 90, "正在准备更新器...");
            var runnerDirectory = Path.Combine(request.UpdatesRoot, "runner");
            PrepareCleanDirectory(runnerDirectory);
            CopyDirectory(stagingDirectory, runnerDirectory);
            updaterPath = Path.Combine(runnerDirectory, request.UpdaterExeName);
            if (!File.Exists(updaterPath))
            {
                return SoftwareUpdateResult.Fail($"更新器运行目录缺少 {request.UpdaterExeName}。");
            }

            var arguments = BuildUpdaterArguments(request, stagingDirectory);
            return SoftwareUpdateResult.OkUpdate("更新包准备完成。", stagingDirectory, updaterPath, arguments);
        }
        catch (JsonException ex)
        {
            return SoftwareUpdateResult.Fail($"manifest 读取失败：{ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            return SoftwareUpdateResult.Fail($"更新包解压失败：{ex.Message}");
        }
        catch (Exception ex)
        {
            return SoftwareUpdateResult.Fail($"软件更新准备失败：{ex.Message}");
        }
    }

    private async Task<SoftwareUpdateManifest> LoadManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<SoftwareUpdateManifest>(stream, jsonOptions, cancellationToken);
        if (manifest is null)
        {
            throw new JsonException("manifest 内容为空。");
        }

        return manifest;
    }

    private static string ValidateManifest(SoftwareUpdateManifest manifest, out Version remoteVersion)
    {
        var versionError = ValidateManifestVersion(manifest, out remoteVersion);
        if (!string.IsNullOrEmpty(versionError))
        {
            return versionError;
        }

        if (string.IsNullOrWhiteSpace(manifest.PackageFile))
        {
            return "manifest packageFile 不能为空。";
        }

        if (string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            return "manifest sha256 不能为空。";
        }

        return string.Empty;
    }

    private static string ValidateManifestVersion(SoftwareUpdateManifest manifest, out Version remoteVersion)
    {
        remoteVersion = new Version(0, 0);

        if (string.IsNullOrWhiteSpace(manifest.Version)
            || !Version.TryParse(manifest.Version.Trim(), out var parsedVersion))
        {
            return "manifest 版本号无效。";
        }

        remoteVersion = parsedVersion;
        return string.Empty;
    }

    private static string ResolvePackagePath(string manifestPath, string packageFile)
    {
        if (Path.IsPathRooted(packageFile))
        {
            return packageFile;
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
        return Path.Combine(manifestDirectory, packageFile);
    }

    private static string ToManifestAccessError(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return "manifest 文件不存在或不可访问。";
        }

        return errorMessage.Contains("文件不存在或不可访问", StringComparison.Ordinal)
            ? "manifest 文件不存在。"
            : $"manifest 不可访问：{errorMessage}";
    }

    private static string ToPackageAccessError(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return "更新包文件不存在或不可访问。";
        }

        return errorMessage.Contains("文件不存在或不可访问", StringComparison.Ordinal)
            ? "更新包文件不存在。"
            : $"更新包不可访问：{errorMessage}";
    }

    private static void PrepareCleanDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        Directory.CreateDirectory(directory);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            var destinationPath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(filePath, destinationPath, true);
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildUpdaterArguments(SoftwareUpdateRequest request, string stagingDirectory)
    {
        var builder = new StringBuilder();
        AppendArgument(builder, "--processId");
        AppendArgument(builder, request.CurrentProcessId.ToString());
        AppendArgument(builder, "--targetDir");
        AppendArgument(builder, request.TargetDirectory);
        AppendArgument(builder, "--stagingDir");
        AppendArgument(builder, stagingDirectory);
        AppendArgument(builder, "--mainExeName");
        AppendArgument(builder, request.MainExeName);
        AppendArgument(builder, "--updatesRoot");
        AppendArgument(builder, request.UpdatesRoot);
        return builder.ToString().Trim();
    }

    private static void AppendArgument(StringBuilder builder, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append('"');
        builder.Append(value.Replace("\"", "\\\"", StringComparison.Ordinal));
        builder.Append('"');
    }

    private static string FormatVersion(Version version)
    {
        return version.Revision >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
            : version.Build >= 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : $"{version.Major}.{version.Minor}";
    }

    private static void Report(IProgress<SoftwareUpdateProgress>? progress, int value, string stepText)
    {
        progress?.Report(new SoftwareUpdateProgress
        {
            ProgressValue = value,
            StepText = stepText
        });
    }
}
