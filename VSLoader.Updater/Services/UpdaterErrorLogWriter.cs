using System.IO;
using System.Text;

namespace VSLoader.Updater.Services;

public sealed class UpdaterErrorLogWriter
{
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
        var logPath = Path.Combine(errorLogRoot, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");
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
        File.WriteAllText(logPath, builder.ToString());
        return logPath;
    }
}
