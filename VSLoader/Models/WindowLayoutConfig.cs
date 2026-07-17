namespace VSLoader.Models;

public sealed class WindowLayoutConfig
{
    public WindowBoundsConfig? MainWindow { get; set; }

    public WindowBoundsConfig? FactoryMapWindow { get; set; }

    public bool WasFactoryMapOpen { get; set; }

    public string FactoryMapWindowState { get; set; } = FactoryMapWindowStateKinds.Normal;

    public FactoryMapViewStateConfig? FactoryMapView { get; set; }

    public ShortcutGridColumnLayoutConfig? ShortcutGridColumns { get; set; }
}
