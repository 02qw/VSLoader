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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    private SettingsViewModel CreateViewModel(WebUiConfig webUiConfig)
    {
        return new SettingsViewModel(
            _validExePath,
            new AdminUiConfig(),
            webUiConfig,
            new HotkeyConfig(),
            new DialogService(),
            new PasswordProtectionService(),
            _ => SaveResult.Ok());
    }
}
