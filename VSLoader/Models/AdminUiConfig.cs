namespace VSLoader.Models;

public sealed class AdminUiConfig
{
    public string BaseUrl { get; set; } = "https://192.168.15.69:8181/oistarter/";

    public string Host { get; set; } = "SICEAPO1.macmicst.com";

    public string RoleName { get; set; } = "Administrator";

    public string InstancePropertiesName { get; set; } = "INSTANCE.properties";

    public string InstanceNameKey { get; set; } = "zam.instance.name";

    public string PortKey { get; set; } = "SocketServer.Port";

    public string ServiceNameKey { get; set; } = "PacService";

    public bool IgnoreCertificateErrors { get; set; } = true;

    public string ProtectedPassword { get; set; } = string.Empty;

    public AdminUiConfig Clone()
    {
        return new AdminUiConfig
        {
            BaseUrl = BaseUrl,
            Host = Host,
            RoleName = RoleName,
            InstancePropertiesName = InstancePropertiesName,
            InstanceNameKey = InstanceNameKey,
            PortKey = PortKey,
            ServiceNameKey = ServiceNameKey,
            IgnoreCertificateErrors = IgnoreCertificateErrors,
            ProtectedPassword = ProtectedPassword
        };
    }
}
