namespace VSLoader.Tests;

public sealed class ContextMenuCapabilityMenuIntegrationTests
{
    [Fact]
    public void Shortcut_grid_context_menu_is_generated_from_capability_collection()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "MainWindow.xaml"));
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "MainWindow.xaml.cs"));

        Assert.Contains("Opened=\"ShortcutContextMenu_Opened\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<MenuItem Header=\"VSCode\"", xaml, StringComparison.Ordinal);
        Assert.Contains("GetContextMenuCapabilities", code, StringComparison.Ordinal);
        Assert.Contains("ExecuteContextMenuCapabilityAsync", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_map_browse_menu_uses_shared_capabilities_without_legacy_enum()
    {
        var mapCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "Views", "FactoryMapWindow.xaml.cs"));
        var mainCode = File.ReadAllText(TestProjectPaths.GetProjectFilePath("VSLoader", "MainWindow.xaml.cs"));

        Assert.Contains("getContextMenuCapabilities", mapCode, StringComparison.Ordinal);
        Assert.Contains("executeContextMenuCapability", mapCode, StringComparison.Ordinal);
        Assert.DoesNotContain("FactoryMapShortcutAction", mapCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteShortcutActionFromMap", mainCode, StringComparison.Ordinal);
    }

    [Fact]
    public void PowerShell_editor_explains_variables_and_provides_copyable_examples()
    {
        var xaml = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "ContextMenuCapabilityEditorWindow.xaml"));

        Assert.Contains("VSLoader 自动提供", xaml, StringComparison.Ordinal);
        Assert.Contains("当前快捷项的目标路径", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_TARGET_PATH", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_TARGET_PARENT", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_SHORTCUT_NAME", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_DESCRIPTION", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_SOURCE_MODULE_NAME", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_WORKSPACE_ID", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_WORKSPACE_PATH", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_APP_BASE_PATH", xaml, StringComparison.Ordinal);
        Assert.Contains("$env:VSL_SOURCE_SURFACE", xaml, StringComparison.Ordinal);
        Assert.Contains("Start-Process explorer.exe", xaml, StringComparison.Ordinal);
        Assert.Contains("ProcessFolder.ps1", xaml, StringComparison.Ordinal);
        Assert.Contains("PowerShell 中不要写 {TargetPath}", xaml, StringComparison.Ordinal);
    }
}
