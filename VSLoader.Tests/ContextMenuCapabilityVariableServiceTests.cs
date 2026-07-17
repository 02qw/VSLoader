using VSLoader.Models;
using VSLoader.Services;

namespace VSLoader.Tests;

public sealed class ContextMenuCapabilityVariableServiceTests
{
    [Fact]
    public void BuildTemplateVariables_adds_target_identity_without_changing_existing_values()
    {
        var context = CreateContext(@"\\192.168.15.69\instances\3134_TSSP001");

        var values = ContextMenuCapabilityVariableService.BuildTemplateVariables(context);

        Assert.Equal(@"\\192.168.15.69\instances\3134_TSSP001", values["TargetPath"]);
        Assert.Equal("设备 A", values["ShortcutName"]);
        Assert.Equal("3134_TSSP001", values["TargetName"]);
        Assert.Equal("3134", values["InstanceId"]);
        Assert.Equal("TSSP001", values["DeviceCode"]);
        Assert.Equal("TSSP", values["DeviceType"]);
        Assert.Equal("001", values["DeviceNumber"]);
    }

    [Fact]
    public void BuildEnvironmentVariables_exposes_the_same_target_identity()
    {
        var values = ContextMenuCapabilityVariableService.BuildEnvironmentVariables(
            CreateContext(@"C:\instances\5924_TSSP002"));

        Assert.Equal("5924_TSSP002", values["VSL_TARGET_NAME"]);
        Assert.Equal("5924", values["VSL_INSTANCE_ID"]);
        Assert.Equal("TSSP002", values["VSL_DEVICE_CODE"]);
        Assert.Equal("TSSP", values["VSL_DEVICE_TYPE"]);
        Assert.Equal("002", values["VSL_DEVICE_NUMBER"]);
    }

    [Fact]
    public void BuildEnvironmentVariables_uses_empty_values_when_identity_is_unavailable()
    {
        var values = ContextMenuCapabilityVariableService.BuildEnvironmentVariables(
            CreateContext(@"C:\instances\invalid-folder"));

        Assert.Equal("invalid-folder", values["VSL_TARGET_NAME"]);
        Assert.Empty(values["VSL_INSTANCE_ID"]);
        Assert.Empty(values["VSL_DEVICE_CODE"]);
        Assert.Empty(values["VSL_DEVICE_TYPE"]);
        Assert.Empty(values["VSL_DEVICE_NUMBER"]);
    }

    [Fact]
    public void Target_parent_resolution_is_lexical_and_does_not_probe_network_or_disk()
    {
        var source = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Models",
            "Services",
            "ContextMenuCapabilityVariableService.cs"));

        Assert.DoesNotContain("Directory.Exists", source, StringComparison.Ordinal);
        Assert.Equal(
            @"\\203.0.113.250\offline-share\instances",
            ContextMenuCapabilityVariableService.GetTargetParent(
                @"\\203.0.113.250\offline-share\instances\8842_TTVF006"));
    }

    private static ContextMenuCapabilityExecutionContext CreateContext(string targetPath)
    {
        return new ContextMenuCapabilityExecutionContext
        {
            Shortcut = new ShortcutItem
            {
                Name = "设备 A",
                TargetPath = targetPath,
                Description = "备注",
                SourceModuleName = "eap-sic-Example"
            },
            WorkspaceId = "default",
            WorkspaceDirectory = @"C:\Workspace",
            AppBaseDirectory = @"C:\VSLoader",
            Surface = ContextMenuCapabilitySurfaces.ShortcutList
        };
    }
}
