using System.IO;
using System.Text;

namespace VSLoader.Updater.Services;

public sealed class UpdaterApplyService
{
    private readonly string errorLogRoot;
    private readonly Func<string, bool>? shouldFailCopy;

    public UpdaterApplyService(string? errorLogRoot = null, Func<string, bool>? shouldFailCopy = null)
    {
        this.errorLogRoot = string.IsNullOrWhiteSpace(errorLogRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSLoader", "errorLog")
            : errorLogRoot;
        this.shouldFailCopy = shouldFailCopy;
    }

    public UpdaterApplyResult Apply(UpdaterOptions options)
    {
        var step = "初始化";
        string? backupDirectory = null;

        try
        {
            step = "备份旧版本";
            backupDirectory = CreateBackup(options);

            step = "替换程序文件";
            CopyDirectory(options.StagingDirectory, options.TargetDirectory);

            step = "清理临时文件";
            CleanupOnSuccess(options.UpdatesRoot);

            return UpdaterApplyResult.Ok();
        }
        catch (Exception ex)
        {
            var rollbackSucceeded = false;
            Exception? rollbackException = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(backupDirectory) && Directory.Exists(backupDirectory))
                {
                    CopyDirectory(backupDirectory, options.TargetDirectory);
                    rollbackSucceeded = true;
                }
            }
            catch (Exception rollbackEx)
            {
                rollbackException = rollbackEx;
            }

            var logPath = WriteErrorLog(options, step, ex, rollbackException);
            return UpdaterApplyResult.Fail(ex.Message, rollbackSucceeded, logPath);
        }
    }

    private string CreateBackup(UpdaterOptions options)
    {
        var backupRoot = Path.Combine(options.UpdatesRoot, "backup");
        Directory.CreateDirectory(backupRoot);
        var backupDirectory = Path.Combine(backupRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        Directory.CreateDirectory(backupDirectory);

        foreach (var filePath in Directory.EnumerateFiles(options.TargetDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(options.TargetDirectory, filePath);
            var destinationPath = Path.Combine(backupDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(filePath, destinationPath, true);
        }

        return backupDirectory;
    }

    private void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            if (shouldFailCopy?.Invoke(filePath) == true)
            {
                throw new IOException($"模拟复制失败：{filePath}");
            }

            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            if (ShouldSkipUpdaterSelfFile(relativePath))
            {
                continue;
            }

            var destinationPath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(filePath, destinationPath, true);
        }
    }

    private static bool ShouldSkipUpdaterSelfFile(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return fileName.StartsWith("VSLoader.Updater.", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("VSLoader.Updater.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanupOnSuccess(string updatesRoot)
    {
        DeleteDirectoryIfExists(Path.Combine(updatesRoot, "download"));
        DeleteDirectoryIfExists(Path.Combine(updatesRoot, "staging"));
        KeepLatestBackup(updatesRoot);
    }

    private static void KeepLatestBackup(string updatesRoot)
    {
        var backupRoot = Path.Combine(updatesRoot, "backup");
        if (!Directory.Exists(backupRoot))
        {
            return;
        }

        var backups = Directory.GetDirectories(backupRoot)
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var oldBackup in backups.Skip(1))
        {
            DeleteDirectoryIfExists(oldBackup);
        }
    }

    private static void DeleteDirectoryIfExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private string WriteErrorLog(UpdaterOptions options, string step, Exception exception, Exception? rollbackException)
    {
        Directory.CreateDirectory(errorLogRoot);
        var logPath = Path.Combine(errorLogRoot, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");
        var builder = new StringBuilder();
        builder.AppendLine($"Time: {DateTime.Now:O}");
        builder.AppendLine($"Step: {step}");
        builder.AppendLine($"TargetDirectory: {options.TargetDirectory}");
        builder.AppendLine($"StagingDirectory: {options.StagingDirectory}");
        builder.AppendLine($"ProcessId: {options.ProcessId}");
        builder.AppendLine("Exception:");
        builder.AppendLine(exception.ToString());

        if (rollbackException is not null)
        {
            builder.AppendLine();
            builder.AppendLine("RollbackException:");
            builder.AppendLine(rollbackException.ToString());
        }

        File.WriteAllText(logPath, builder.ToString());
        return logPath;
    }
}

public sealed class UpdaterApplyResult
{
    private UpdaterApplyResult(bool success, string errorMessage, bool rollbackSucceeded, string? errorLogPath)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RollbackSucceeded = rollbackSucceeded;
        ErrorLogPath = errorLogPath;
    }

    public bool Success { get; }

    public string ErrorMessage { get; }

    public bool RollbackSucceeded { get; }

    public string? ErrorLogPath { get; }

    public static UpdaterApplyResult Ok()
    {
        return new UpdaterApplyResult(true, string.Empty, false, null);
    }

    public static UpdaterApplyResult Fail(string errorMessage, bool rollbackSucceeded, string? errorLogPath)
    {
        return new UpdaterApplyResult(false, errorMessage, rollbackSucceeded, errorLogPath);
    }
}
