using System.Windows;
using System.Windows.Threading;
using VSLoader.Updater.Services;

namespace VSLoader.Updater;

public partial class App : Application
{
    private string[] startupArgs = [];
    private readonly UpdaterErrorLogWriter errorLogWriter = new();

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        startupArgs = e.Args;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        try
        {
            var parseResult = UpdaterArgumentParser.Parse(e.Args);
            var window = new MainWindow(parseResult);
            window.Show();
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowStartupError(e.Exception);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            errorLogWriter.WriteStartupError(startupArgs, exception);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ShowStartupError(e.Exception);
    }

    private void ShowStartupError(Exception exception)
    {
        var logPath = errorLogWriter.WriteStartupError(startupArgs, exception);
        var parseResult = UpdaterArgumentParseResult.Fail($"更新器启动失败：{exception.Message}\n错误日志：{logPath}");
        var window = new MainWindow(parseResult);
        window.Show();
    }
}
