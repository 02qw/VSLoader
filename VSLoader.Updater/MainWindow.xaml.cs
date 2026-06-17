using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using VSLoader.Updater.Services;

namespace VSLoader.Updater;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly UpdaterArgumentParseResult parseResult;
    private bool isUpdating = true;
    private int progressValue;
    private string statusText = "正在准备更新...";
    private string releaseNotesText = string.Empty;
    private bool hasError;
    private bool showReleaseNotes;

    public MainWindow(UpdaterArgumentParseResult parseResult)
    {
        this.parseResult = parseResult;
        InitializeComponent();
        DataContext = this;
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int ProgressValue
    {
        get => progressValue;
        private set
        {
            progressValue = value;
            OnPropertyChanged(nameof(ProgressValue));
        }
    }

    public string StatusText
    {
        get => statusText;
        private set
        {
            statusText = value;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool HasError
    {
        get => hasError;
        private set
        {
            hasError = value;
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool ShowReleaseNotes
    {
        get => showReleaseNotes;
        private set
        {
            showReleaseNotes = value;
            OnPropertyChanged(nameof(ShowReleaseNotes));
        }
    }

    public string ReleaseNotesText
    {
        get => releaseNotesText;
        private set
        {
            releaseNotesText = NormalizeReleaseNotes(value);
            OnPropertyChanged(nameof(ReleaseNotesText));
        }
    }

    public ObservableCollection<string> DetailLines { get; } = new();

    public ICommand OpenErrorLogDirectoryCommand => new RelayCommand(OpenErrorLogDirectory);

    public ICommand CloseCommand => new RelayCommand(CloseAfterError);

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Topmost = false;

        if (!parseResult.Success || parseResult.Options is null)
        {
            ShowError(parseResult.ErrorMessage);
            return;
        }

        await RunUpdateAsync(parseResult.Options);
    }

    private async Task RunUpdateAsync(UpdaterOptions options)
    {
        try
        {
            if (options.Mode.Equals("update", StringComparison.OrdinalIgnoreCase))
            {
                await RunFullUpdateAsync(options);
                return;
            }

            SetProgress(0, "正在等待主程序退出...");
            if (!await WaitForProcessExitAsync(options.ProcessId, TimeSpan.FromSeconds(30)))
            {
                ShowError("主程序未能退出，请手动退出后重试。");
                return;
            }

            SetProgress(20, "正在备份旧版本...");
            await Task.Delay(100);

            SetProgress(45, "正在替换程序文件...");
            var service = new UpdaterApplyService();
            var result = await Task.Run(() => service.Apply(options));
            if (!result.Success)
            {
                var rollbackText = result.RollbackSucceeded ? "已恢复旧版本。" : "回滚失败，请人工处理。";
                ShowError($"更新失败：{result.ErrorMessage}\n{rollbackText}\n错误日志：{result.ErrorLogPath}");
                return;
            }

            SetProgress(100, "更新完成。");
            isUpdating = false;
            ConfirmAndStartMainApp(
                options,
                "更新完成",
                "VSLoader 已更新完成，点击确认后启动新版程序。");
        }
        catch (Exception ex)
        {
            ShowError($"更新失败：{ex.Message}");
        }
    }

    private async Task RunFullUpdateAsync(UpdaterOptions options)
    {
        var progress = new Progress<UpdaterProgress>(updateProgress =>
        {
            if (!string.IsNullOrWhiteSpace(updateProgress.ReleaseNotes))
            {
                ApplyReleaseNotes(updateProgress.ReleaseNotes);
            }

            SetProgress(updateProgress.Value, updateProgress.Message);
        });

        SetProgress(0, "更新器已启动，正在准备接管...");
        if (!await WaitForProcessExitAsync(options.ProcessId, TimeSpan.FromSeconds(30)))
        {
            ShowError("主程序未能退出，请手动退出后重试。");
            return;
        }

        SetProgress(60, "主程序已退出，开始更新...");
        var service = new UpdaterUpdateService();
        var result = await service.RunAsync(options, progress);
        if (!result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.ReleaseNotes))
            {
                ApplyReleaseNotes(result.ReleaseNotes);
            }

            var logText = string.IsNullOrWhiteSpace(result.ErrorLogPath) ? string.Empty : $"\n错误日志：{result.ErrorLogPath}";
            ShowError($"{result.ErrorMessage}{logText}");
            return;
        }

        if (!result.RestartMainApp)
        {
            isUpdating = false;
            ApplyReleaseNotes(result.ReleaseNotes);
            SetProgress(100, result.Message);
            ConfirmAndStartMainApp(
                options,
                "当前已是最新版本",
                "当前 VSLoader 已经是最新版本，点击确认后返回程序。");
            return;
        }

        ApplyReleaseNotes(result.ReleaseNotes);
        SetProgress(100, "更新完成，等待确认启动新版程序。");
        isUpdating = false;
        ConfirmAndStartMainApp(
            options,
            "更新完成",
            "VSLoader 已更新完成，点击确认后启动新版程序。");
    }

    private static async Task<bool> WaitForProcessExitAsync(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            using var cts = new CancellationTokenSource(timeout);
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!isUpdating)
        {
            return;
        }

        e.Cancel = true;
        MessageBox.Show(this, "正在更新，暂时不能关闭。", "VSLoader", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowError(string message)
    {
        isUpdating = false;
        HasError = true;
        StatusText = message;
    }

    private void SetProgress(int value, string text)
    {
        ProgressValue = value;
        StatusText = text;
        DetailLines.Add($"[{DateTime.Now:HH:mm:ss}] {text}");
        DetailList.ScrollIntoView(DetailLines[^1]);
    }

    private void ApplyReleaseNotes(string? releaseNotes)
    {
        ReleaseNotesText = releaseNotes;
        ShowReleaseNotes = ShouldShowReleaseNotes(manifestLoaded: true);
    }

    internal static bool ShouldShowReleaseNotes(bool manifestLoaded)
    {
        return manifestLoaded;
    }

    private void ConfirmAndStartMainApp(UpdaterOptions options, string title, string message)
    {
        try
        {
            var dialog = new UpdateCompletedDialog(title, message, ReleaseNotesText, ShowReleaseNotes)
            {
                Owner = this
            };
            dialog.ShowDialog();
            StartMainApp(options);
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"新版程序启动失败：{ex.Message}");
        }
    }

    private static void StartMainApp(UpdaterOptions options)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(options.TargetDirectory, options.MainExeName),
            WorkingDirectory = options.TargetDirectory,
            UseShellExecute = true
        });
    }

    private static string NormalizeReleaseNotes(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "本次更新未填写更新说明。"
            : value.Trim();
    }

    private void OpenErrorLogDirectory()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSLoader", "errorLog");
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }

    private void CloseAfterError()
    {
        isUpdating = false;
        Close();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action execute;

        public RelayCommand(Action execute)
        {
            this.execute = execute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            execute();
        }
    }
}
