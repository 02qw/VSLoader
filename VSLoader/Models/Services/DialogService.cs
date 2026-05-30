using System.Windows;
using System.Windows.Interop;
using VSLoader.Views;
using WinForms = System.Windows.Forms;

namespace VSLoader.Services;

public sealed class DialogService
{
    public void ShowInfo(string message)
    {
        ShowMessage(message, MessageDialogKind.Info);
    }

    public void ShowError(string message)
    {
        ShowMessage(message, MessageDialogKind.Error);
    }

    public bool Confirm(string message)
    {
        var dialog = CreateMessageDialog(message, MessageDialogKind.Confirm);
        return dialog.ShowDialog() == true && dialog.Confirmed;
    }

    public string? SelectExeFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog(GetOwnerWindow()) == true ? dialog.FileName : null;
    }

    public string? SelectFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog(GetOwnerWindow()) == true ? dialog.FileName : null;
    }

    public string? SelectCsvFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog(GetOwnerWindow()) == true ? dialog.FileName : null;
    }

    public string? SelectFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "请选择目标文件夹",
            UseDescriptionForTitle = true
        };

        var owner = GetOwnerWindow();
        var handle = owner is null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;
        var result = handle == IntPtr.Zero
            ? dialog.ShowDialog()
            : dialog.ShowDialog(new WindowHandleWrapper(handle));

        return result == WinForms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static Window? GetOwnerWindow()
    {
        return System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;
    }

    private static void ShowMessage(string message, MessageDialogKind kind)
    {
        CreateMessageDialog(message, kind).ShowDialog();
    }

    private static MessageDialogWindow CreateMessageDialog(string message, MessageDialogKind kind)
    {
        var dialog = new MessageDialogWindow(message, kind);
        var owner = GetOwnerWindow();
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog;
    }

    private sealed class WindowHandleWrapper : WinForms.IWin32Window
    {
        public WindowHandleWrapper(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }
}
