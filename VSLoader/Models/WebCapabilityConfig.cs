namespace VSLoader.Models;

public sealed class WebCapabilityConfig
{
    public string UrlTemplate { get; set; } = string.Empty;

    public WebCapabilityConfig Clone()
    {
        return new WebCapabilityConfig { UrlTemplate = UrlTemplate };
    }
}
