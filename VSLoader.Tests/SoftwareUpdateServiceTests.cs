using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class SoftwareUpdateServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _updatesRoot;
    private readonly string _targetDir;
    private readonly SoftwareUpdateService _service = new();

    public SoftwareUpdateServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        _updatesRoot = Path.Combine(_rootPath, "Updates");
        _targetDir = Path.Combine(_rootPath, "CurrentApp");
        Directory.CreateDirectory(_rootPath);
        Directory.CreateDirectory(_targetDir);
    }

    [Fact]
    public async Task PrepareUpdateAsync_returns_failure_when_manifest_path_is_empty()
    {
        var result = await _service.PrepareUpdateAsync(new SoftwareUpdateRequest
        {
            ManifestPath = string.Empty,
            CurrentVersion = new Version(2, 0, 1),
            TargetDirectory = _targetDir,
            UpdatesRoot = _updatesRoot,
            CurrentProcessId = 123
        });

        Assert.False(result.Success);
        Assert.Contains("软件更新 manifest 路径不能为空", result.ErrorMessage);
    }

    [Fact]
    public async Task PrepareUpdateAsync_returns_failure_when_manifest_file_missing()
    {
        var result = await _service.PrepareUpdateAsync(CreateRequest(Path.Combine(_rootPath, "missing.json")));

        Assert.False(result.Success);
        Assert.Contains("manifest 文件不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task PrepareUpdateAsync_returns_failure_when_manifest_json_is_broken()
    {
        var manifestPath = Path.Combine(_rootPath, "manifest.json");
        File.WriteAllText(manifestPath, "{ broken json");

        var result = await _service.PrepareUpdateAsync(CreateRequest(manifestPath));

        Assert.False(result.Success);
        Assert.Contains("manifest 读取失败", result.ErrorMessage);
    }

    [Fact]
    public async Task PrepareUpdateAsync_returns_failure_when_version_is_invalid()
    {
        var manifestPath = WriteManifest(new SoftwareUpdateManifest
        {
            Version = "abc",
            PackageFile = "update.zip",
            Sha256 = "hash"
        });

        var result = await _service.PrepareUpdateAsync(CreateRequest(manifestPath));

        Assert.False(result.Success);
        Assert.Contains("版本号无效", result.ErrorMessage);
    }

    [Fact]
    public async Task PrepareUpdateAsync_returns_no_update_when_remote_version_is_not_newer()
    {
        var packagePath = CreateUpdatePackage(includeMainExe: true, includeUpdater: true);
        var manifestPath = WriteManifestForPackage(packagePath, "2.0.1");

        var result = await _service.PrepareUpdateAsync(CreateRequest(manifestPath, currentVersion: new Version(2, 0, 1)));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(result.UpdateAvailable);
        Assert.Equal("当前已是最新版本。", result.Message);
    }

    [Fact]
    public async Task PrepareUpdateAsync_resolves_relative_package_path_from_manifest_directory()
    {
        var packagePath = CreateUpdatePackage(includeMainExe: true, includeUpdater: true);
        var manifestPath = WriteManifestForPackage(packagePath, "2.0.2", useRelativePackagePath: true);

        var result = await _service.PrepareUpdateAsync(CreateRequest(manifestPath));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.UpdateAvailable);
        Assert.Equal(Path.Combine(_updatesRoot, "staging", "2.0.2"), result.StagingDirectory);
        Assert.True(File.Exists(Path.Combine(result.StagingDirectory!, "VSLoader.exe")));
        Assert.True(File.Exists(Path.Combine(result.StagingDirectory!, "VSLoader.Updater.exe")));
        Assert.Contains("--targetDir", result.UpdaterArguments);
    }

    [Fact]
    public async Task PrepareUpdateAsync_returns_updater_path_from_runner_directory()
    {
        var packagePath = CreateUpdatePackage(includeMainExe: true, includeUpdater: true);
        var manifestPath = WriteManifestForPackage(packagePath, "2.0.2");

        var result = await _service.PrepareUpdateAsync(CreateRequest(manifestPath));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.UpdateAvailable);
        var runnerDirectory = Path.Combine(_updatesRoot, "runner");
        Assert.Equal(Path.Combine(runnerDirectory, "VSLoader.Updater.exe"), result.UpdaterPath);
        Assert.True(File.Exists(Path.Combine(runnerDirectory, "VSLoader.Updater.exe")));
        Assert.True(File.Exists(Path.Combine(runnerDirectory, "VSLoader.Updater.dll")));
        Assert.True(File.Exists(Path.Combine(runnerDirectory, "VSLoader.Updater.runtimeconfig.json")));
    }

    [Fact]
    public async Task PrepareUpdateAsync_cleans_old_runner_before_copying_new_runner()
    {
        var oldRunnerFile = Path.Combine(_updatesRoot, "runner", "old.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(oldRunnerFile)!);
        File.WriteAllText(oldRunnerFile, "old");
        var packagePath = CreateUpdatePackage(includeMainExe: true, includeUpdater: true);
        var manifestPath = WriteManifestForPackage(packagePath, "2.0.2");

        var result = await _service.PrepareUpdateAsync(CreateRequest(manifestPath));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(File.Exists(oldRunnerFile));
    }

    [Fact]
    public async Task PrepareUpdateAsync_returns_failure_when_sha256_does_not_match()
    {
        var packagePath = CreateUpdatePackage(includeMainExe: true, includeUpdater: true);
        var manifestPath = WriteManifest(new SoftwareUpdateManifest
        {
            Version = "2.0.2",
            PackageFile = packagePath,
            Sha256 = "0000"
        });

        var result = await _service.PrepareUpdateAsync(CreateRequest(manifestPath));

        Assert.False(result.Success);
        Assert.Contains("SHA256 校验失败", result.ErrorMessage);
    }

    [Fact]
    public async Task PrepareUpdateAsync_returns_failure_when_staging_missing_main_exe()
    {
        var packagePath = CreateUpdatePackage(includeMainExe: false, includeUpdater: true);
        var manifestPath = WriteManifestForPackage(packagePath, "2.0.2");

        var result = await _service.PrepareUpdateAsync(CreateRequest(manifestPath));

        Assert.False(result.Success);
        Assert.Contains("缺少 VSLoader.exe", result.ErrorMessage);
    }

    [Fact]
    public async Task PrepareUpdateAsync_returns_failure_when_staging_missing_updater()
    {
        var packagePath = CreateUpdatePackage(includeMainExe: true, includeUpdater: false);
        var manifestPath = WriteManifestForPackage(packagePath, "2.0.2");

        var result = await _service.PrepareUpdateAsync(CreateRequest(manifestPath));

        Assert.False(result.Success);
        Assert.Contains("缺少 VSLoader.Updater.exe", result.ErrorMessage);
    }

    private SoftwareUpdateRequest CreateRequest(string manifestPath, Version? currentVersion = null)
    {
        return new SoftwareUpdateRequest
        {
            ManifestPath = manifestPath,
            CurrentVersion = currentVersion ?? new Version(2, 0, 1),
            TargetDirectory = _targetDir,
            UpdatesRoot = _updatesRoot,
            CurrentProcessId = 123
        };
    }

    private string WriteManifestForPackage(string packagePath, string version, bool useRelativePackagePath = false)
    {
        var packageFile = useRelativePackagePath
            ? Path.GetFileName(packagePath)
            : packagePath;

        return WriteManifest(new SoftwareUpdateManifest
        {
            Version = version,
            PackageFile = packageFile,
            Sha256 = ComputeSha256(packagePath),
            ReleaseNotes = "notes"
        });
    }

    private string WriteManifest(SoftwareUpdateManifest manifest)
    {
        var manifestPath = Path.Combine(_rootPath, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        return manifestPath;
    }

    private string CreateUpdatePackage(bool includeMainExe, bool includeUpdater)
    {
        var sourceDir = Path.Combine(_rootPath, "package-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceDir);
        if (includeMainExe)
        {
            File.WriteAllText(Path.Combine(sourceDir, "VSLoader.exe"), "main");
        }

        if (includeUpdater)
        {
            File.WriteAllText(Path.Combine(sourceDir, "VSLoader.Updater.exe"), "updater");
            File.WriteAllText(Path.Combine(sourceDir, "VSLoader.Updater.dll"), "updater dll");
            File.WriteAllText(Path.Combine(sourceDir, "VSLoader.Updater.runtimeconfig.json"), "{}");
        }

        File.WriteAllText(Path.Combine(sourceDir, "VSLoader.dll"), "dll");

        var zipPath = Path.Combine(_rootPath, "VSLoader_2.0.2_win-x64.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

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
