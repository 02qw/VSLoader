using System.Windows;
using System.Windows.Controls;
using VSLoader.Models;
using VSLoader.Views;

namespace VSLoader.Tests;

public sealed class FactoryMapWindowRuntimeLoadTests
{
    [Fact]
    public void Visible_factory_map_edges_inset_endpoints_under_nodes_to_avoid_zoom_pan_artifacts()
    {
        var edge = new FactoryMapDeviceEdgeViewData
        {
            From = new FactoryMapDeviceViewNode { X = 100, Y = 200 },
            To = new FactoryMapDeviceViewNode { X = 360, Y = 220 }
        };

        var points = FactoryMapWindow.CreateVisibleEdgePoints(edge);

        Assert.Equal(4, points.Count);
        Assert.Equal(100 + FactoryMapWindow.DeviceNodeWidth - FactoryMapWindow.EdgeEndpointInset, points[0].X);
        Assert.Equal(360 + FactoryMapWindow.EdgeEndpointInset, points[points.Count - 1].X);
    }

    [Fact]
    public void Visible_factory_map_edges_use_flat_line_caps_to_avoid_node_side_artifacts()
    {
        var code = File.ReadAllText(TestProjectPaths.GetProjectFilePath(
            "VSLoader",
            "Views",
            "FactoryMapWindow.xaml.cs"));

        var visiblePolylineStart = code.IndexOf("var polyline = new Polyline", StringComparison.Ordinal);
        var hitPolylineStart = code.IndexOf("var hitPolyline = new Polyline", StringComparison.Ordinal);
        Assert.True(visiblePolylineStart >= 0);
        Assert.True(hitPolylineStart > visiblePolylineStart);

        var visiblePolylineBlock = code[visiblePolylineStart..hitPolylineStart];
        Assert.Contains("StrokeStartLineCap = PenLineCap.Flat", visiblePolylineBlock);
        Assert.Contains("StrokeEndLineCap = PenLineCap.Flat", visiblePolylineBlock);
    }

    [Fact]
    public void Factory_map_window_can_initialize_with_application_resources()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                var window = new FactoryMapWindow(
                    _ => { },
                    (_, _) => { },
                    _ => true,
                    () => Array.Empty<ShortcutItem>(),
                    () => Path.Combine(Path.GetTempPath(), "factory-map-runtime-test.json"));

                window.Close();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    private static void EnsureApplicationResources()
    {
        var application = Application.Current ?? new Application();
        application.Resources.MergedDictionaries.Clear();
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(TestProjectPaths.GetProjectFilePath(
                "VSLoader",
                "Styles",
                "ModernTheme.xaml"), UriKind.Absolute)
        });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(TestProjectPaths.GetProjectFilePath(
                "VSLoader",
                "Styles",
                "ModernWindowChrome.xaml"), UriKind.Absolute)
        });
        application.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
