using System.Windows;
using System.Windows.Controls;

namespace VSLoader.Tests;

[Collection(WpfApplicationTestCollection.Name)]
public sealed class ModernWindowChromeUsageTests
{
    [Theory]
    [InlineData("VSLoader", "MainWindow.xaml")]
    [InlineData("VSLoader", "Views", "WorkspaceSelectorWindow.xaml")]
    [InlineData("VSLoader", "Views", "SettingsWindow.xaml")]
    [InlineData("VSLoader", "Views", "BatchImportWindow.xaml")]
    [InlineData("VSLoader", "Views", "ShortcutEditWindow.xaml")]
    [InlineData("VSLoader", "Views", "WorkspaceNameDialog.xaml")]
    [InlineData("VSLoader", "Views", "FactoryMapWindow.xaml")]
    public void Core_windows_use_modern_window_chrome_and_title_bar(params string[] parts)
    {
        var xaml = File.ReadAllText(GetProjectFilePath(parts));

        Assert.Contains("WindowChrome.WindowChrome", xaml);
        Assert.Contains("ModernTitleBar", xaml);
        Assert.Contains("ModernWindowOuterBorderBrush", xaml);
        Assert.DoesNotContain("UseAeroCaptionButtons=\"True\"", xaml);
    }

    [Fact]
    public void Message_dialog_keeps_custom_borderless_shell()
    {
        var xaml = File.ReadAllText(GetProjectFilePath("VSLoader", "Views", "MessageDialogWindow.xaml"));

        Assert.Contains("WindowStyle=\"None\"", xaml);
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("ModernTitleBar", xaml);
        Assert.Contains("Style=\"{StaticResource ModernMessageDialogSurfaceStyle}\"", xaml);
        Assert.DoesNotContain("BorderBrush=\"{StaticResource ModernWindowOuterBorderBrush}\"", xaml);
        Assert.DoesNotContain("BorderThickness=\"1\"\r\n            ClipToBounds=\"True\"", xaml);
    }

    [Fact]
    public void Message_dialog_surface_style_can_initialize_with_application_resource_order()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            Application? ownedApplication = null;
            try
            {
                var application = Application.Current;
                if (application is null)
                {
                    ownedApplication = new Application();
                    application = ownedApplication;
                }
                application.Resources.MergedDictionaries.Clear();
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(GetProjectFilePath("VSLoader", "Styles", "ModernTheme.xaml"), UriKind.Absolute)
                });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(GetProjectFilePath("VSLoader", "Styles", "ModernWindowChrome.xaml"), UriKind.Absolute)
                });

                var border = new Border
                {
                    Style = (Style)application.FindResource("ModernMessageDialogSurfaceStyle")
                };
                border.ApplyTemplate();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                ownedApplication?.Shutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void Modern_title_bar_uses_vector_icons_instead_of_font_glyphs()
    {
        var xaml = File.ReadAllText(GetProjectFilePath("VSLoader", "Views", "Controls", "ModernTitleBar.xaml"));

        Assert.DoesNotContain("Content=\"—\"", xaml);
        Assert.DoesNotContain("Content=\"□\"", xaml);
        Assert.DoesNotContain("Content=\"×\"", xaml);
        Assert.DoesNotContain("<Line", xaml);
        Assert.DoesNotContain("<Rectangle", xaml);
        Assert.Contains("ModernTitleBarMinimizeIconGeometry", xaml);
        Assert.Contains("ModernTitleBarMaximizeIconGeometry", xaml);
        Assert.Contains("ModernTitleBarWindowedIconGeometry", xaml);
        Assert.Contains("ModernTitleBarCloseIconGeometry", xaml);
        Assert.Contains("Fill=\"{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}\"", xaml);
        Assert.Contains("MaximizeIcon", xaml);
        Assert.Contains("RestoreIcon", xaml);
    }

    [Fact]
    public void Modern_title_bar_buttons_are_directly_hit_test_visible_in_window_chrome()
    {
        var xaml = File.ReadAllText(GetProjectFilePath("VSLoader", "Views", "Controls", "ModernTitleBar.xaml"));

        Assert.Contains("x:Name=\"MinimizeButton\"", xaml);
        Assert.Contains("x:Name=\"MaximizeRestoreButton\"", xaml);
        Assert.Contains("x:Name=\"CloseButton\"", xaml);

        var minimizeButtonStart = xaml.IndexOf("x:Name=\"MinimizeButton\"", StringComparison.Ordinal);
        var maximizeButtonStart = xaml.IndexOf("x:Name=\"MaximizeRestoreButton\"", StringComparison.Ordinal);
        var closeButtonStart = xaml.IndexOf("x:Name=\"CloseButton\"", StringComparison.Ordinal);
        Assert.True(minimizeButtonStart >= 0);
        Assert.True(maximizeButtonStart > minimizeButtonStart);
        Assert.True(closeButtonStart > maximizeButtonStart);

        var minimizeBlock = xaml[minimizeButtonStart..maximizeButtonStart];
        var maximizeBlock = xaml[maximizeButtonStart..closeButtonStart];
        var closeBlock = xaml[closeButtonStart..];
        Assert.Contains("shell:WindowChrome.IsHitTestVisibleInChrome=\"True\"", minimizeBlock);
        Assert.Contains("shell:WindowChrome.IsHitTestVisibleInChrome=\"True\"", maximizeBlock);
        Assert.Contains("shell:WindowChrome.IsHitTestVisibleInChrome=\"True\"", closeBlock);
    }

    [Fact]
    public void Modern_title_bar_keeps_hover_diagnostics_for_window_chrome_debugging()
    {
        var code = File.ReadAllText(GetProjectFilePath("VSLoader", "Views", "Controls", "ModernTitleBar.xaml.cs"));

        Assert.Contains("titlebar-hover.debug.log", code);
        Assert.Contains("EnableTitleBarHoverDebugLogging = false", code);
        Assert.Contains("TitleBarButton_PreviewMouseMove", code);
        Assert.Contains("TitleBarButton_MouseEnter", code);
        Assert.Contains("TitleBarButtonMouseEnterIdleSnapshot", code);
        Assert.Contains("OwnerWindow_Activated", code);
        Assert.Contains("GetForegroundWindow", code);
        Assert.Contains("OwnerWindow_WndProc", code);
        Assert.Contains("WM_NCHITTEST", code);
        Assert.Contains("WM_MOUSEMOVE", code);
        Assert.Contains("NativeWndHookAttached", code);
        Assert.Contains("BuildNativeMouseSnapshot", code);
        Assert.Contains("NativeHit=", code);
        Assert.Contains("NativeCloseContains", code);
        Assert.Contains("ClientToScreen", code);
        Assert.Contains("RefreshMouseHoverState", code);
        Assert.Contains("Mouse.Synchronize", code);
        Assert.Contains("Mouse.DirectlyOver", code);
        Assert.Contains("FormatButtonState(CloseButton)", code);
        Assert.Contains("WindowChrome.GetIsHitTestVisibleInChrome", code);
    }

    private static string GetProjectFilePath(params string[] parts)
    {
        return TestProjectPaths.GetProjectFilePath(parts);
    }
}
