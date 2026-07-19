namespace VSLoader.Models;

public sealed class RuntimeLayoutState
{
    public bool HasMainWindowBounds { get; set; }

    public double MainLeft { get; set; }

    public double MainTop { get; set; }

    public double MainWidth { get; set; }

    public double MainHeight { get; set; }

    public string MainWindowState { get; set; } = MainWindowStateKinds.Normal;

    public bool WasFactoryMapOpen { get; set; }

    public string FactoryMapWindowState { get; set; } = FactoryMapWindowStateKinds.Normal;

    public bool HasFactoryMapBounds { get; set; }

    public double FactoryMapLeft { get; set; }

    public double FactoryMapTop { get; set; }

    public double FactoryMapWidth { get; set; }

    public double FactoryMapHeight { get; set; }

    public FactoryMapViewState? FactoryMapView { get; set; }

    public ShortcutGridColumnLayoutConfig? ShortcutGridColumns { get; set; }
}
