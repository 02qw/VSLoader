using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using VSLoader.Updater.Services;

namespace VSLoader.Tests;

public sealed class UpdaterUpdateServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _targetDir;
    private readonly string _updatesRoot;

    public UpdaterUpdateServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        _targetDir = Path.Combine(_rootPath, "target");
        _updatesRoot = Path.Combine(_rootPath, "Updates");
        Directory.CreateDirectory(_targetDir);
        Directory.CreateDirectory(_updatesRoot);
    }

    [Fact]
    public async Task RunAsync_returns_no_update_when_remote_version_is_not_newer()
    {
        var packagePath = CreatePackage(includeMain: true, includeUpdater: true);
        const string releaseNotes = "当前版本说明";
        var manifestPath = WriteManifest(new SoftwareUpdateManifest
        {
            Version = "2.1.0",
            PackageFile = packagePath,
            Sha256 = ComputeSha256(packagePath),
            ReleaseNotes = releaseNotes
        });
        var service = new UpdaterUpdateService(apply: _ => UpdaterApplyResult.Ok());

        var result = await service.RunAsync(CreateOptions(manifestPath, currentVersion: new Version(2, 1, 0)));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(result.RestartMainApp);
        Assert.Contains("当前已是最新版本", result.Message, StringComparison.Ordinal);
        Assert.Equal(releaseNotes, result.ReleaseNotes);
    }

    [Fact]
    public async Task RunAsync_returns_failure_when_sha256_does_not_match()
    {
        var packagePath = CreatePackage(includeMain: true, includeUpdater: true);
        var manifest = new SoftwareUpdateManifest
        {
            Version = "2.1.1",
            PackageFile = packagePath,
            Sha256 = "0000"
        };
        var manifestPath = WriteManifest(manifest);
        var service = new UpdaterUpdateService(apply: _ => UpdaterApplyResult.Ok());

        var result = await service.RunAsync(CreateOptions(manifestPath));

        Assert.False(result.Success);
        Assert.Contains("SHA256 校验失败", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_extracts_package_and_calls_apply_on_success()
    {
        var packagePath = CreatePackage(includeMain: true, includeUpdater: true);
        var manifestPath = WriteManifest(packagePath, version: "2.1.1");
        UpdaterOptions? appliedOptions = null;
        var service = new UpdaterUpdateService(apply: options =>
        {
            appliedOptions = options;
            return UpdaterApplyResult.Ok();
        });

        var result = await service.RunAsync(CreateOptions(manifestPath));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.RestartMainApp);
        Assert.NotNull(appliedOptions);
        Assert.Equal(Path.Combine(_updatesRoot, "staging", "2.1.1"), appliedOptions.StagingDirectory);
    }

    [Fact]
    public async Task RunAsync_reports_and_returns_release_notes_from_manifest()
    {
        var packagePath = CreatePackage(includeMain: true, includeUpdater: true);
        const string releaseNotes = "1. 展示更新内容\n2. 更新完成后确认启动";
        var manifestPath = WriteManifest(new SoftwareUpdateManifest
        {
            Version = "2.1.1",
            PackageFile = packagePath,
            Sha256 = ComputeSha256(packagePath),
            ReleaseNotes = releaseNotes
        });
        var progressItems = new List<UpdaterProgress>();
        var progress = new Progress<UpdaterProgress>(progressItems.Add);
        var service = new UpdaterUpdateService(apply: _ => UpdaterApplyResult.Ok());

        var result = await service.RunAsync(CreateOptions(manifestPath), progress);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(releaseNotes, result.ReleaseNotes);
        Assert.Contains(progressItems, item => item.ReleaseNotes == releaseNotes);
    }

    private UpdaterOptions CreateOptions(string manifestPath, Version? currentVersion = null)
    {
        return new UpdaterOptions
        {
            Mode = "update",
            ProcessId = 123,
            TargetDirectory = _targetDir,
            MainExeName = "VSLoader.exe",
            ManifestPath = manifestPath,
            CurrentVersion = currentVersion ?? new Version(2, 1, 0),
            UpdatesRoot = _updatesRoot
        };
    }

    private string WriteManifest(string packagePath, string version)
    {
        return WriteManifest(new SoftwareUpdateManifest
        {
            Version = version,
            PackageFile = packagePath,
            Sha256 = ComputeSha256(packagePath)
        });
    }

    private string WriteManifest(SoftwareUpdateManifest manifest)
    {
        var manifestPath = Path.Combine(_rootPath, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        return manifestPath;
    }

    private string CreatePackage(bool includeMain, bool includeUpdater)
    {
        var sourceDir = Path.Combine(_rootPath, "package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceDir);
        if (includeMain)
        {
            File.WriteAllText(Path.Combine(sourceDir, "VSLoader.exe"), "main");
        }

        if (includeUpdater)
        {
            File.WriteAllText(Path.Combine(sourceDir, "VSLoader.Updater.exe"), "updater");
        }

        var zipPath = Path.Combine(_rootPath, Guid.NewGuid().ToString("N") + ".zip");
        ZipFile.CreateFromDirectory(sourceDir, zipPath);
        return zipPath;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
