using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VSLoader.Views;

public enum MessageDialogKind
{
    Info,
    Error,
    Confirm
}

public partial class MessageDialogWindow : Window
{
    public MessageDialogWindow(string message, MessageDialogKind kind)
    {
        InitializeComponent();

        Message = message;
        Kind = kind;
        IconText = kind switch
        {
            MessageDialogKind.Error => "!",
            MessageDialogKind.Confirm => "?",
            _ => "i"
        };
        IconBackground = kind switch
        {
            MessageDialogKind.Error => new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38)),
            MessageDialogKind.Confirm => new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
            _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235))
        };

        OkButton.Visibility = kind == MessageDialogKind.Confirm ? Visibility.Collapsed : Visibility.Visible;
        YesButton.Visibility = kind == MessageDialogKind.Confirm ? Visibility.Visible : Visibility.Collapsed;
        NoButton.Visibility = kind == MessageDialogKind.Confirm ? Visibility.Visible : Visibility.Collapsed;

        DataContext = this;
        DialogTitleBar.CloseRequested += (_, _) => CancelDialog();
        PreviewKeyDown += MessageDialogWindow_PreviewKeyDown;
    }

    public string Message { get; }

    public MessageDialogKind Kind { get; }

    public string IconText { get; }

    public System.Windows.Media.Brush IconBackground { get; }

    public bool Confirmed { get; private set; }

    public static bool ShouldConfirmOnClose(MessageDialogKind kind)
    {
        return false;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        CancelDialog();
    }

    private void MessageDialogWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        CancelDialog();
    }

    private void CancelDialog()
    {
        Confirmed = ShouldConfirmOnClose(Kind);
        DialogResult = false;
        Close();
    }
}
