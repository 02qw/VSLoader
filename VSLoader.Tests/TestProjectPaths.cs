namespace VSLoader.Tests;

internal static class TestProjectPaths
{
    public static string GetProjectFilePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VSLoader.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("Could not locate VSLoader.sln from test output directory.");
        }

        return Path.GetFullPath(Path.Combine(directory.FullName, Path.Combine(parts)));
    }
}
