using System.ComponentModel;
using System.Windows;

namespace VSLoader.Updater;

public partial class UpdateCompletedDialog : Window
{
    public UpdateCompletedDialog(string titleText, string messageText, string releaseNotesText, bool showReleaseNotes)
    {
        TitleText = titleText;
        MessageText = messageText;
        ReleaseNotesText = releaseNotesText;
        ShowReleaseNotes = showReleaseNotes;
        InitializeComponent();
        DataContext = this;
        Title = titleText;
    }

    public string TitleText { get; }

    public string MessageText { get; }

    public string ReleaseNotesText { get; }

    public bool ShowReleaseNotes { get; }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DialogResult is null)
        {
            DialogResult = true;
        }

        base.OnClosing(e);
    }
}
