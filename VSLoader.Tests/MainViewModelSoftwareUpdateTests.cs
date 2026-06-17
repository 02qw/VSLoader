using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class MainViewModelSoftwareUpdateTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _validExePath;

    public MainViewModelSoftwareUpdateTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _validExePath = Path.Combine(_rootPath, "Code.exe");
        File.WriteAllText(_validExePath, string.Empty);
    }

    [Fact]
    public async Task UpdateSoftwareAsync_shows_error_when_manifest_path_is_empty()
    {
        var dialogService = new RecordingDialogService();
        var viewModel = CreateViewModel(new AppSettings { VSCodePath = _validExePath }, dialogService);

        await viewModel.UpdateSoftwareCommand.ExecuteAsync(null);

        Assert.Contains(dialogService.Errors, message => message.Contains("请先进入设置配置软件更新 manifest 路径", StringComparison.Ordinal));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task UpdateSoftwareAsync_does_not_start_updater_when_user_cancels_confirmation()
    {
        var appSettings = new AppSettings
        {
            VSCodePath = _validExePath,
            SoftwareUpdateManifestPath = Path.Combine(_rootPath, "manifest.json")
        };
        var dialogService = new RecordingDialogService();
        dialogService.ConfirmResult = false;
        var viewModel = CreateViewModel(appSettings, dialogService);
        var startedUpdater = false;
        var exitRequested = false;
        viewModel.StartUpdater = (_, _) => startedUpdater = true;
        viewModel.RequestApplicationExit = () => exitRequested = true;

        await viewModel.UpdateSoftwareCommand.ExecuteAsync(null);

        Assert.False(startedUpdater);
        Assert.False(exitRequested);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task UpdateSoftwareAsync_shows_latest_message_without_starting_updater_when_manifest_version_is_not_newer()
    {
        var manifestPath = Path.Combine(_rootPath, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new SoftwareUpdateManifest
        {
            Version = (typeof(MainViewModel).Assembly.GetName().Version ?? new Version(0, 0)).ToString(),
            PackageFile = "missing.zip",
            Sha256 = "missing",
            ReleaseNotes = "当前版本说明"
        }));
        var appSettings = new AppSettings
        {
            VSCodePath = _validExePath,
            SoftwareUpdateManifestPath = manifestPath
        };
        var dialogService = new RecordingDialogService();
        var viewModel = CreateViewModel(appSettings, dialogService);
        var startedUpdater = false;
        var exitRequested = false;
        viewModel.StartUpdater = (_, _) => startedUpdater = true;
        viewModel.RequestApplicationExit = () => exitRequested = true;

        await viewModel.UpdateSoftwareCommand.ExecuteAsync(null);

        Assert.False(startedUpdater);
        Assert.False(exitRequested);
        Assert.Empty(dialogService.ConfirmMessages);
        Assert.Contains(dialogService.Infos, message => message.Contains("当前已是最新版本", StringComparison.Ordinal));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task UpdateSoftwareAsync_starts_existing_updater_after_confirmation()
    {
        var updaterPath = Path.Combine(AppContext.BaseDirectory, "VSLoader.Updater.exe");
        File.WriteAllText(updaterPath, "updater");
        var manifestPath = WriteNewerManifest();
        var appSettings = new AppSettings
        {
            VSCodePath = _validExePath,
            SoftwareUpdateManifestPath = manifestPath
        };
        var dialogService = new RecordingDialogService();
        dialogService.ConfirmResult = true;
        var viewModel = CreateViewModel(appSettings, dialogService);
        string? startedPath = null;
        string? startedArguments = null;
        var exitRequested = false;
        viewModel.StartUpdater = (path, arguments) =>
        {
            startedPath = path;
            startedArguments = arguments;
            return true;
        };
        viewModel.RequestApplicationExit = () => exitRequested = true;

        await viewModel.UpdateSoftwareCommand.ExecuteAsync(null);

        Assert.Equal(updaterPath, startedPath);
        Assert.True(exitRequested);
        Assert.Contains("--mode", startedArguments);
        Assert.Contains("update", startedArguments);
        Assert.Contains("--manifestPath", startedArguments);
        Assert.Contains(manifestPath, startedArguments);
        Assert.Contains("--currentVersion", startedArguments);
        Assert.Contains("--targetDir", startedArguments);
        Assert.Contains("--processId", startedArguments);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task UpdateSoftwareAsync_does_not_exit_when_updater_start_fails()
    {
        var updaterPath = Path.Combine(AppContext.BaseDirectory, "VSLoader.Updater.exe");
        File.WriteAllText(updaterPath, "updater");
        var manifestPath = WriteNewerManifest();
        var appSettings = new AppSettings
        {
            VSCodePath = _validExePath,
            SoftwareUpdateManifestPath = manifestPath
        };
        var dialogService = new RecordingDialogService();
        dialogService.ConfirmResult = true;
        var viewModel = CreateViewModel(appSettings, dialogService);
        var exitRequested = false;
        viewModel.StartUpdater = (_, _) => false;
        viewModel.RequestApplicationExit = () => exitRequested = true;

        await viewModel.UpdateSoftwareCommand.ExecuteAsync(null);

        Assert.False(exitRequested);
        Assert.Contains(dialogService.Errors, message => message.Contains("更新器启动失败", StringComparison.Ordinal));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task UpdateSoftwareAsync_shows_error_when_current_updater_is_missing()
    {
        var updaterPath = Path.Combine(AppContext.BaseDirectory, "VSLoader.Updater.exe");
        if (File.Exists(updaterPath))
        {
            File.Delete(updaterPath);
        }

        var manifestPath = WriteNewerManifest();
        var appSettings = new AppSettings
        {
            VSCodePath = _validExePath,
            SoftwareUpdateManifestPath = manifestPath
        };
        var dialogService = new RecordingDialogService();
        dialogService.ConfirmResult = true;
        var viewModel = CreateViewModel(appSettings, dialogService);
        var exitRequested = false;
        viewModel.RequestApplicationExit = () => exitRequested = true;

        await viewModel.UpdateSoftwareCommand.ExecuteAsync(null);

        Assert.False(exitRequested);
        Assert.Contains(dialogService.Errors, message => message.Contains("缺少 VSLoader.Updater.exe", StringComparison.Ordinal));
    }

    private MainViewModel CreateViewModel(AppSettings appSettings, DialogService dialogService)
    {
        var configService = new ConfigService(_rootPath);
        configService.Save(new AppConfig());
        return new MainViewModel(
            appSettings,
            new AppSettingsService(_rootPath),
            configService,
            new VSCodeLauncherService(),
            dialogService,
            new BatchImportService(),
            new AdminUiService(),
            new WebUiService(),
            new ShortcutSearchService(),
            new PasswordProtectionService(),
            new ClipboardService(),
            new UpdateCheckService(),
            Path.Combine(_rootPath, "updateTime.json"),
            new SoftwareUpdateService(),
            Path.Combine(_rootPath, "Updates"));
    }

    private string WriteManifest(string packagePath)
    {
        var manifest = new SoftwareUpdateManifest
        {
            Version = "99.0.0",
            PackageFile = packagePath,
            Sha256 = ComputeSha256(packagePath)
        };
        var manifestPath = Path.Combine(_rootPath, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        return manifestPath;
    }

    private string WriteNewerManifest()
    {
        var currentVersion = typeof(MainViewModel).Assembly.GetName().Version ?? new Version(0, 0);
        var newerVersion = new Version(currentVersion.Major + 1, 0, 0);
        var manifestPath = Path.Combine(_rootPath, "manifest-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new SoftwareUpdateManifest
        {
            Version = newerVersion.ToString(),
            PackageFile = "missing.zip",
            Sha256 = "missing",
            ReleaseNotes = "新版本说明"
        }));
        return manifestPath;
    }

    private string CreateUpdatePackage()
    {
        var sourceDir = Path.Combine(_rootPath, "package-source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "VSLoader.exe"), "main");
        File.WriteAllText(Path.Combine(sourceDir, "VSLoader.Updater.exe"), "updater");
        File.WriteAllText(Path.Combine(sourceDir, "VSLoader.Updater.dll"), "updater dll");
        File.WriteAllText(Path.Combine(sourceDir, "VSLoader.Updater.runtimeconfig.json"), "{}");

        var zipPath = Path.Combine(_rootPath, "update.zip");
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

    private sealed class RecordingDialogService : DialogService
    {
        public List<string> Errors { get; } = new();

        public List<string> Infos { get; } = new();

        public List<string> ConfirmMessages { get; } = new();

        public bool ConfirmResult { get; set; } = true;

        public override void ShowInfo(string message)
        {
            Infos.Add(message);
        }

        public override void ShowError(string message)
        {
            Errors.Add(message);
        }

        public override bool Confirm(string message)
        {
            ConfirmMessages.Add(message);
            return ConfirmResult;
        }
    }
}
