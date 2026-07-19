using System.IO;

namespace VSLoader.Services;

public static class UpdateTimePathService
{
    public static string GlobalUpdateTimePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VSLoader",
        "updateTime.json");
}
