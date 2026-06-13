using System.Text.Json;
using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ConfigServiceWebUiTests : IDisposable
{
    private readonly string _rootPath;

    public ConfigServiceWebUiTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void Load_returns_default_webui_config_when_missing_from_json()
    {
        var service = new ConfigService(_rootPath);
        File.WriteAllText(service.ConfigPath, """{"VSCodePath":"","Shortcuts":[]}""");

        var result = service.Load();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Config.WebUi);
        Assert.Equal("https://192.168.15.69", result.Config.WebUi.BaseUrl);
        Assert.Equal("INSTANCE.properties", result.Config.WebUi.InstancePropertiesName);
    }

    [Fact]
    public void Save_writes_webui_config_to_workspace_config_json()
    {
        var service = new ConfigService(_rootPath);
        var config = new AppConfig
        {
            WebUi = new WebUiConfig
            {
                BaseUrl = "https://192.168.15.67",
                InstancePropertiesName = "LINE.properties",
                InstanceNameKey = "line.instance.name",
                SslPortKey = "LINE.WebServer.SSLPort"
            }
        };

        var saveResult = service.Save(config);
        var json = File.ReadAllText(service.ConfigPath);
        using var document = JsonDocument.Parse(json);
        var webUi = document.RootElement.GetProperty("WebUi");

        Assert.True(saveResult.Success, saveResult.ErrorMessage);
        Assert.Equal("https://192.168.15.67", webUi.GetProperty("BaseUrl").GetString());
        Assert.Equal("LINE.properties", webUi.GetProperty("InstancePropertiesName").GetString());
        Assert.Equal("line.instance.name", webUi.GetProperty("InstanceNameKey").GetString());
        Assert.Equal("LINE.WebServer.SSLPort", webUi.GetProperty("SslPortKey").GetString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}
