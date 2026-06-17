using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class SettingsViewModelWebUiTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _validExePath;

    public SettingsViewModelWebUiTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _validExePath = Path.Combine(_rootPath, "Code.exe");
        File.WriteAllText(_validExePath, string.Empty);
    }

    [Fact]
    public void Constructor_clones_webui_config()
    {
        var webUiConfig = new WebUiConfig
        {
            BaseUrl = "https://192.168.15.67",
            InstancePropertiesName = "LINE.properties",
            InstanceNameKey = "line.instance.name",
            SslPortKey = "LINE.WebServer.SSLPort"
        };

        var viewModel = CreateViewModel(webUiConfig);
        viewModel.WebUi.BaseUrl = "https://changed.example";

        Assert.Equal("https://192.168.15.67", webUiConfig.BaseUrl);
        Assert.Equal("https://changed.example", viewModel.WebUi.BaseUrl);
    }

    [Fact]
    public void Save_trims_webui_config_and_marks_saved()
    {
        var viewModel = CreateViewModel(new WebUiConfig());
        viewModel.WebUi.BaseUrl = "  https://192.168.15.67/  ";
        viewModel.WebUi.InstancePropertiesName = "  INSTANCE.properties  ";
        viewModel.WebUi.InstanceNameKey = "  zam.instance.name  ";
        viewModel.WebUi.SslPortKey = "  GUI.WebServer.SSLPort  ";

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Saved);
        Assert.Equal("https://192.168.15.67/", viewModel.WebUi.BaseUrl);
        Assert.Equal("INSTANCE.properties", viewModel.WebUi.InstancePropertiesName);
        Assert.Equal("zam.instance.name", viewModel.WebUi.InstanceNameKey);
        Assert.Equal("GUI.WebServer.SSLPort", viewModel.WebUi.SslPortKey);
    }

    [Fact]
    public void Constructor_clones_update_check_config()
    {
        var updateCheckConfig = new UpdateCheckConfig
        {
            RulesFilePath = @"C:\rules.csv",
            MapFilePath = @"C:\map.json",
            SoftwareVersionFilePath = @"C:\version.txt"
        };

        var viewModel = CreateViewModel(new WebUiConfig(), updateCheckConfig);
        viewModel.UpdateCheck.RulesFilePath = @"D:\changed.csv";

        Assert.Equal(@"C:\rules.csv", updateCheckConfig.RulesFilePath);
        Assert.Equal(@"D:\changed.csv", viewModel.UpdateCheck.RulesFilePath);
    }

    [Fact]
    public void Save_trims_active_update_check_paths_and_ignores_legacy_software_version_path()
    {
        var viewModel = CreateViewModel(new WebUiConfig());
        viewModel.UpdateCheck.RulesFilePath = "  ";
        viewModel.UpdateCheck.MapFilePath = "  C:\\map.json  ";
        viewModel.UpdateCheck.SoftwareVersionFilePath = "  C:\\version.txt  ";

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Saved);
        Assert.Equal(string.Empty, viewModel.UpdateCheck.RulesFilePath);
        Assert.Equal(@"C:\map.json", viewModel.UpdateCheck.MapFilePath);
        Assert.Equal("  C:\\version.txt  ", viewModel.UpdateCheck.SoftwareVersionFilePath);
    }

    [Fact]
    public void Save_trims_software_update_manifest_path_and_allows_empty_path()
    {
        var viewModel = CreateViewModel(new WebUiConfig());
        viewModel.SoftwareUpdateManifestPath = "  \\\\server\\VSLoaderUpdate\\manifest.json  ";

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Saved);
        Assert.Equal(@"\\server\VSLoaderUpdate\manifest.json", viewModel.SoftwareUpdateManifestPath);

        viewModel.SoftwareUpdateManifestPath = "  ";
        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Saved);
        Assert.Equal(string.Empty, viewModel.SoftwareUpdateManifestPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    private SettingsViewModel CreateViewModel(WebUiConfig webUiConfig, UpdateCheckConfig? updateCheckConfig = null)
    {
        return new SettingsViewModel(
            _validExePath,
            string.Empty,
            new AdminUiConfig(),
            webUiConfig,
            updateCheckConfig ?? new UpdateCheckConfig(),
            new HotkeyConfig(),
            new DialogService(),
            new PasswordProtectionService(),
            _ => SaveResult.Ok());
    }
}
