namespace VSLoader.Models;

public sealed class UpdateCheckConfig
{
    public string RulesFilePath { get; set; } = string.Empty;

    public string MapFilePath { get; set; } = string.Empty;

    public string SoftwareVersionFilePath { get; set; } = string.Empty;

    public UpdateCheckConfig Clone()
    {
        return new UpdateCheckConfig
        {
            RulesFilePath = RulesFilePath,
            MapFilePath = MapFilePath,
            SoftwareVersionFilePath = SoftwareVersionFilePath
        };
    }
}
