using VSLoader.Models;
using VSLoader.Services;
using VSLoader.ViewModels;

namespace VSLoader.Tests;

public sealed class SettingsViewModelPageOrderTests : IDisposable
{
    private readonly string rootPath;
    private readonly string validExePath;

    public SettingsViewModelPageOrderTests()
    {
        rootPath = Path.Combine(Path.GetTempPath(), "VSLoader.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        validExePath = Path.Combine(rootPath, "Code.exe");
        File.WriteAllText(validExePath, string.Empty);
    }

    [Fact]
    public void Constructor_uses_normalized_page_order_and_keeps_order_page_fixed_last()
    {
        var viewModel = CreateViewModel(
        [
            SettingsPageIds.Hotkeys,
            SettingsPageIds.AdminUi
        ]);

        Assert.Equal(SettingsPageIds.Hotkeys, viewModel.SettingsPages[0].Id);
        Assert.Equal(SettingsPageIds.AdminUi, viewModel.SettingsPages[1].Id);
        Assert.Equal(SettingsPageIds.PageOrder, viewModel.SettingsPages[^1].Id);
        Assert.True(viewModel.SettingsPages[^1].IsFixed);
        Assert.Same(viewModel.SettingsPages[0], viewModel.SelectedSettingsPage);
    }

    [Fact]
    public void Move_commands_update_navigation_and_saved_order_without_moving_fixed_page()
    {
        var viewModel = CreateViewModel(SettingsPageOrderService.DefaultPageOrder);
        var adminPage = viewModel.SettingsPages.Single(page => page.Id == SettingsPageIds.AdminUi);
        var fixedPage = viewModel.SettingsPages.Single(page => page.Id == SettingsPageIds.PageOrder);

        viewModel.MoveSettingsPageUpCommand.Execute(adminPage);

        Assert.Equal(SettingsPageIds.AdminUi, viewModel.SettingsPages[0].Id);
        Assert.Equal(SettingsPageIds.General, viewModel.SettingsPages[1].Id);
        Assert.Equal(SettingsPageIds.AdminUi, viewModel.SettingsPageOrder[0]);

        var beforeFixedMove = viewModel.SettingsPages.Select(page => page.Id).ToArray();
        viewModel.MoveSettingsPageUpCommand.Execute(fixedPage);

        Assert.Equal(beforeFixedMove, viewModel.SettingsPages.Select(page => page.Id));
        Assert.Equal(SettingsPageIds.PageOrder, viewModel.SettingsPages[^1].Id);
    }

    [Fact]
    public void Restore_default_page_order_preserves_selected_page_object()
    {
        var viewModel = CreateViewModel(
        [
            SettingsPageIds.Hotkeys,
            SettingsPageIds.ContextMenuCapabilities,
            SettingsPageIds.General
        ]);
        var selected = viewModel.SettingsPages.Single(page => page.Id == SettingsPageIds.ContextMenuCapabilities);
        viewModel.SelectedSettingsPage = selected;

        viewModel.RestoreDefaultSettingsPageOrderCommand.Execute(null);

        Assert.Equal(SettingsPageOrderService.DefaultPageOrder, viewModel.SettingsPageOrder);
        Assert.Same(selected, viewModel.SelectedSettingsPage);
        Assert.Equal(SettingsPageIds.PageOrder, viewModel.SettingsPages[^1].Id);
    }

    [Fact]
    public void Selected_page_exposes_exactly_one_content_visibility_state()
    {
        var viewModel = CreateViewModel(SettingsPageOrderService.DefaultPageOrder);
        viewModel.SelectedSettingsPage = viewModel.SettingsPages.Single(page => page.Id == SettingsPageIds.WebUi);

        Assert.False(viewModel.IsGeneralPageSelected);
        Assert.False(viewModel.IsAdminUiPageSelected);
        Assert.True(viewModel.IsWebUiPageSelected);
        Assert.False(viewModel.IsUpdatesPageSelected);
        Assert.False(viewModel.IsHotkeysPageSelected);
        Assert.False(viewModel.IsContextMenuCapabilitiesPageSelected);
        Assert.False(viewModel.IsPageOrderPageSelected);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }

    private SettingsViewModel CreateViewModel(IEnumerable<string> pageOrder)
    {
        return new SettingsViewModel(
            validExePath,
            string.Empty,
            new AdminUiConfig(),
            new WebUiConfig(),
            new UpdateCheckConfig(),
            new HotkeyConfig(),
            new MapHotkeyConfig(),
            new DialogService(),
            new PasswordProtectionService(),
            (_, _) => SaveResult.Ok(),
            settingsPageOrder: pageOrder);
    }
}
