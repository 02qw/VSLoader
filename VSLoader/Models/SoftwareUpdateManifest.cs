namespace VSLoader.Models;

public sealed class SoftwareUpdateManifest
{
    public string Version { get; set; } = string.Empty;

    public string PackageFile { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public string ReleaseNotes { get; set; } = string.Empty;
}
