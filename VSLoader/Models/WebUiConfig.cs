namespace VSLoader.Models;

public sealed class WebUiConfig
{
    public string BaseUrl { get; set; } = "https://192.168.15.69";

    public string InstancePropertiesName { get; set; } = "INSTANCE.properties";

    public string InstanceNameKey { get; set; } = "zam.instance.name";

    public string SslPortKey { get; set; } = "GUI.WebServer.SSLPort";

    public WebUiConfig Clone()
    {
        return new WebUiConfig
        {
            BaseUrl = BaseUrl,
            InstancePropertiesName = InstancePropertiesName,
            InstanceNameKey = InstanceNameKey,
            SslPortKey = SslPortKey
        };
    }
}
