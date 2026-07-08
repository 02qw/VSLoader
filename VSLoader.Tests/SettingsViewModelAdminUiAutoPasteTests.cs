using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class SettingsViewModelAdminUiAutoPasteTests : IDisposable
{
    private readonly string rootPath;
    private readonly string validExePath;

    public SettingsViewModelAdminUiAutoPasteTests()
    {
        rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        validExePath = Path.Combine(rootPath, "Code.exe");
        File.WriteAllText(validExePath, string.Empty);
    }

    [Fact]
    public void Save_trims_adminui_auto_paste_settings()
    {
        var viewModel = CreateViewModel();
        viewModel.AdminUi.AutoPastePasswordEnabled = true;
        viewModel.AdminUi.AutoPasteWindowTitleKeyword = "  znt client  ";
        viewModel.AdminUi.AutoPasteProcessNames = "  java;javaw  ";
        viewModel.AdminUi.AutoPasteTimeoutSeconds = 15;

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Saved);
        Assert.Equal("znt client", viewModel.AdminUi.AutoPasteWindowTitleKeyword);
        Assert.Equal("java;javaw", viewModel.AdminUi.AutoPasteProcessNames);
        Assert.Equal(15, viewModel.AdminUi.AutoPasteTimeoutSeconds);
    }

    [Theory]
    [InlineData("", "java;javaw")]
    [InlineData("znt client", "")]
    public void Save_rejects_enabled_auto_paste_without_window_keyword_or_process_names(
        string titleKeyword,
        string processNames)
    {
        var dialogService = new RecordingDialogService();
        var viewModel = CreateViewModel(dialogService);
        viewModel.AdminUi.AutoPastePasswordEnabled = true;
        viewModel.AdminUi.AutoPasteWindowTitleKeyword = titleKeyword;
        viewModel.AdminUi.AutoPasteProcessNames = processNames;

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Saved);
        Assert.Contains(dialogService.Errors, message => message.Contains("启用自动粘贴", StringComparison.Ordinal));
    }

    [Fact]
    public void SetRecordedMapHotkey_records_modifiers_and_key()
    {
        var viewModel = CreateViewModel();

        viewModel.SetRecordedMapHotkey(ctrl: false, alt: true, shift: false, key: "X");

        Assert.True(viewModel.MapHotkey.Enabled);
        Assert.False(viewModel.MapHotkey.Ctrl);
        Assert.True(viewModel.MapHotkey.Alt);
        Assert.False(viewModel.MapHotkey.Shift);
        Assert.Equal("X", viewModel.MapHotkey.Key);
        Assert.Equal("Alt + X", viewModel.MapHotkeyDisplayText);
    }

    [Fact]
    public void Save_rejects_main_and_map_hotkey_conflict()
    {
        var dialogService = new RecordingDialogService();
        var viewModel = CreateViewModel(dialogService);
        viewModel.Hotkey = new HotkeyConfig { Enabled = true, InputType = "Keyboard", Alt = true, Key = "X" };
        viewModel.MapHotkey = new MapHotkeyConfig { Enabled = true, Alt = true, Key = "X" };

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Saved);
        Assert.Contains(dialogService.Errors, message => message.Contains("不能相同", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }

    private SettingsViewModel CreateViewModel(DialogService? dialogService = null)
    {
        return new SettingsViewModel(
            validExePath,
            string.Empty,
            new AdminUiConfig(),
            new WebUiConfig(),
            new UpdateCheckConfig(),
            new HotkeyConfig(),
            new MapHotkeyConfig(),
            dialogService ?? new DialogService(),
            new PasswordProtectionService(),
            (_, _) => SaveResult.Ok());
    }

    private sealed class RecordingDialogService : DialogService
    {
        public List<string> Errors { get; } = new();

        public override void ShowError(string message)
        {
            Errors.Add(message);
        }
    }
}
