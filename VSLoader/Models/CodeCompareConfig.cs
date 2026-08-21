namespace VSLoader.Models;

public sealed class CodeCompareConfig
{
    public string LocalModulesRootPath { get; set; } = string.Empty;

    public string DefaultScanScope { get; set; } = @"config\deo";

    public bool AutoScan { get; set; } = true;

    public CodeCompareConfig Clone()
    {
        return new CodeCompareConfig
        {
            LocalModulesRootPath = LocalModulesRootPath ?? string.Empty,
            DefaultScanScope = DefaultScanScope ?? @"config\deo",
            AutoScan = AutoScan
        };
    }
}
