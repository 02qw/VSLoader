using System.IO;

namespace VSLoader.Services;

public sealed class UpdaterRunnerService
{
    private readonly string runnerRootDirectory;

    public UpdaterRunnerService(string? runnerRootDirectory = null)
    {
        this.runnerRootDirectory = string.IsNullOrWhiteSpace(runnerRootDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSLoader", "UpdaterRunner")
            : runnerRootDirectory;
    }

    public string RunnerRootDirectory => runnerRootDirectory;

    public PrepareUpdaterRunnerResult Prepare(string sourceDirectory, string updaterExeName = "VSLoader.Updater.exe")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                return PrepareUpdaterRunnerResult.Fail($"源目录不存在：{sourceDirectory}");
            }

            if (Directory.Exists(RunnerRootDirectory))
            {
                Directory.Delete(RunnerRootDirectory, true);
            }

            Directory.CreateDirectory(RunnerRootDirectory);
            CopyDirectory(sourceDirectory, RunnerRootDirectory);

            var runnerUpdaterPath = Path.Combine(RunnerRootDirectory, updaterExeName);
            if (!File.Exists(runnerUpdaterPath))
            {
                return PrepareUpdaterRunnerResult.Fail($"复制后缺少 {updaterExeName}。");
            }

            return PrepareUpdaterRunnerResult.Ok(runnerUpdaterPath);
        }
        catch (Exception ex)
        {
            return PrepareUpdaterRunnerResult.Fail($"准备更新器运行副本失败：{ex.Message}");
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            var destinationPath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(filePath, destinationPath, true);
        }
    }
}

public sealed class PrepareUpdaterRunnerResult
{
    private PrepareUpdaterRunnerResult(bool success, string runnerUpdaterPath, string errorMessage)
    {
        Success = success;
        RunnerUpdaterPath = runnerUpdaterPath;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public string RunnerUpdaterPath { get; }

    public string ErrorMessage { get; }

    public static PrepareUpdaterRunnerResult Ok(string runnerUpdaterPath)
    {
        return new PrepareUpdaterRunnerResult(true, runnerUpdaterPath, string.Empty);
    }

    public static PrepareUpdaterRunnerResult Fail(string errorMessage)
    {
        return new PrepareUpdaterRunnerResult(false, string.Empty, errorMessage);
    }
}
