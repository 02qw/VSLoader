using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace VSLoader.Updater.Services;

public sealed class UpdaterUpdateService
{
    private readonly Func<UpdaterOptions, UpdaterApplyResult> apply;
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public UpdaterUpdateService(Func<UpdaterOptions, UpdaterApplyResult>? apply = null)
    {
        this.apply = apply ?? (options => new UpdaterApplyService().Apply(options));
    }

    public async Task<UpdaterUpdateResult> RunAsync(
        UpdaterOptions options,
        IProgress<UpdaterProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Report(progress, 5, "正在读取更新信息...");
            var manifest = await LoadManifestAsync(options.ManifestPath, cancellationToken);
            Report(progress, 5, "已读取更新内容。", manifest.ReleaseNotes);
            var validationError = ValidateManifest(manifest, out var remoteVersion);
            if (!string.IsNullOrEmpty(validationError))
            {
                return UpdaterUpdateResult.Fail(validationError, releaseNotes: manifest.ReleaseNotes);
            }

            Report(progress, 10, $"当前版本：{options.CurrentVersion}，服务器版本：{remoteVersion}");
            if (remoteVersion <= options.CurrentVersion)
            {
                return UpdaterUpdateResult.Ok("当前已是最新版本。", restartMainApp: false, manifest.ReleaseNotes);
            }

            var packagePath = ResolvePackagePath(options.ManifestPath, manifest.PackageFile);
            if (!File.Exists(packagePath))
            {
                return UpdaterUpdateResult.Fail("更新包文件不存在。", releaseNotes: manifest.ReleaseNotes);
            }

            var downloadDirectory = Path.Combine(options.UpdatesRoot, "download");
            var stagingRoot = Path.Combine(options.UpdatesRoot, "staging");
            var stagingDirectory = Path.Combine(stagingRoot, FormatVersion(remoteVersion));
            PrepareCleanDirectory(downloadDirectory);
            PrepareCleanDirectory(stagingDirectory);

            Report(progress, 20, "正在复制更新包...");
            var downloadedPackagePath = Path.Combine(downloadDirectory, Path.GetFileName(packagePath));
            File.Copy(packagePath, downloadedPackagePath, true);

            Report(progress, 35, "正在校验 SHA256...");
            var actualSha256 = await ComputeSha256Async(downloadedPackagePath, cancellationToken);
            if (!string.Equals(actualSha256, manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return UpdaterUpdateResult.Fail("SHA256 校验失败。", releaseNotes: manifest.ReleaseNotes);
            }

            Report(progress, 50, "正在解压更新包...");
            ZipFile.ExtractToDirectory(downloadedPackagePath, stagingDirectory, true);

            if (!File.Exists(Path.Combine(stagingDirectory, options.MainExeName)))
            {
                return UpdaterUpdateResult.Fail($"更新包缺少 {options.MainExeName}。", releaseNotes: manifest.ReleaseNotes);
            }

            if (!File.Exists(Path.Combine(stagingDirectory, "VSLoader.Updater.exe")))
            {
                return UpdaterUpdateResult.Fail("更新包缺少 VSLoader.Updater.exe。", releaseNotes: manifest.ReleaseNotes);
            }

            var applyOptions = new UpdaterOptions
            {
                Mode = "apply",
                ProcessId = options.ProcessId,
                TargetDirectory = options.TargetDirectory,
                StagingDirectory = stagingDirectory,
                MainExeName = options.MainExeName,
                UpdatesRoot = options.UpdatesRoot
            };

            Report(progress, 70, "正在备份旧版本...");
            Report(progress, 85, "正在替换程序文件...");
            var applyResult = apply(applyOptions);
            if (!applyResult.Success)
            {
                var rollbackText = applyResult.RollbackSucceeded ? "已恢复旧版本。" : "回滚失败，请人工处理。";
                return UpdaterUpdateResult.Fail($"更新失败：{applyResult.ErrorMessage}\n{rollbackText}", applyResult.ErrorLogPath, manifest.ReleaseNotes);
            }

            Report(progress, 100, "更新完成。");
            return UpdaterUpdateResult.Ok("更新完成。", restartMainApp: true, manifest.ReleaseNotes);
        }
        catch (JsonException ex)
        {
            return UpdaterUpdateResult.Fail($"manifest 读取失败：{ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            return UpdaterUpdateResult.Fail($"更新包解压失败：{ex.Message}");
        }
        catch (Exception ex)
        {
            return UpdaterUpdateResult.Fail($"软件更新失败：{ex.Message}");
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
        remoteVersion = new Version(0, 0);

        if (string.IsNullOrWhiteSpace(manifest.Version)
            || !Version.TryParse(manifest.Version.Trim(), out var parsedVersion))
        {
            return "manifest 版本号无效。";
        }

        remoteVersion = parsedVersion;

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

    private static string ResolvePackagePath(string manifestPath, string packageFile)
    {
        if (Path.IsPathRooted(packageFile))
        {
            return packageFile;
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
        return Path.Combine(manifestDirectory, packageFile);
    }

    private static void PrepareCleanDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        Directory.CreateDirectory(directory);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string FormatVersion(Version version)
    {
        return version.Revision >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
            : version.Build >= 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : $"{version.Major}.{version.Minor}";
    }

    private static void Report(IProgress<UpdaterProgress>? progress, int value, string message, string releaseNotes = "")
    {
        progress?.Report(new UpdaterProgress
        {
            Value = value,
            Message = message,
            ReleaseNotes = releaseNotes
        });
    }
}
