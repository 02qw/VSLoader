using System.IO;
using System.Text;

namespace VSLoader.Updater.Services;

public sealed class UpdaterErrorLogWriter
{
    private const string ErrorLogFileName = "updater-error.log";

    private readonly string errorLogRoot;

    public UpdaterErrorLogWriter(string? errorLogRoot = null)
    {
        this.errorLogRoot = string.IsNullOrWhiteSpace(errorLogRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSLoader", "errorLog")
            : errorLogRoot;
    }

    public string WriteStartupError(string[] args, Exception exception)
    {
        Directory.CreateDirectory(errorLogRoot);
        var logPath = Path.Combine(errorLogRoot, ErrorLogFileName);
        var builder = new StringBuilder();
        builder.AppendLine($"Time: {DateTime.Now:O}");
        builder.AppendLine("Source: VSLoader.Updater startup");
        builder.AppendLine("Args:");
        foreach (var arg in args)
        {
            builder.AppendLine(arg);
        }

        builder.AppendLine();
        builder.AppendLine("Exception:");
        builder.AppendLine(exception.ToString());
        RollingLogFileWriter.Append(logPath, builder.ToString());
        return logPath;
    }
}
