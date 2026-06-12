# v58 运行期窗口与地图视图Session状态保持编码规格说明

## 1. 需求背景

当前 VSLoader 已经支持：

```text
1. 主窗口默认按屏幕 WorkArea 比例布局。
2. 点击“地图”后，地图窗口显示在主窗口右侧。
3. 地图窗口支持缩放、平移、节点拖拽、多选、连线、导入导出。
4. 全局快捷键可以切换程序窗口显示状态。
```

当前存在一个体验问题：

```text
地图被用户放大/平移后，按全局快捷键时，程序会先触发一次地图重新适配或刷新。
第一次按快捷键不是直接最小化，而是先把地图比例还原。
第二次按快捷键才真正最小化。
再次打开程序或地图时，地图视图比例、偏移也可能被重置。
```

用户希望：

```text
在同一次打开软件到退出软件的运行期间，窗口位置、窗口大小、地图缩放比例、地图平移偏移都能保持用户上一次操作后的状态。
```

本次明确采用：

```text
内存 Session 状态方案。
不写入文件。
程序真正退出后状态丢失。
```

## 2. 需求目标

本次目标：

```text
修复快捷键触发地图重置的问题，并增加运行期内存 Session 状态，让本次使用过程中关闭/打开/隐藏/恢复都不打乱用户已调整好的布局和地图视角。
```

完成后：

1. 地图放大/平移后，按快捷键应直接隐藏/最小化程序，不重置地图。
2. 再次按快捷键恢复程序后，地图视角保持原来的缩放和平移。
3. 关闭地图窗口再重新打开，地图窗口大小、位置、缩放比例、平移偏移保持本次 Session 上一次状态。
4. 主窗口移动/缩放后，在本次 Session 中也保留用户调整后的窗口位置和大小。
5. 程序真正退出后，不要求下次启动恢复这些状态。

## 3. 非目标范围

本次不实现：

```text
1. 跨程序重启持久化窗口位置。
2. 写入 session-layout.json。
3. 配置中心增加窗口布局选项。
4. 用户手动重置窗口布局按钮。
5. 多显示器复杂迁移。
6. 地图节点布局 JSON 结构变更。
7. 地图设备节点坐标保存逻辑变更。
```

本次只做：

```text
本次运行期间的内存状态保持。
```

## 4. 核心原则

### 4.1 快捷键只负责显示/隐藏

全局快捷键切换窗口状态时：

```text
如果主窗口当前显示，则直接最小化/隐藏。
如果主窗口当前隐藏或最小化，则恢复显示。
```

不应该额外触发：

```text
1. RefreshFactoryMap()
2. RenderMap(resetView: true)
3. RequestFitMapToView()
4. 地图重新适配
```

### 4.2 自动适配只发生在首次没有视图状态时

地图窗口首次打开时：

```text
如果本次 Session 没有保存过地图视图状态，则可以执行 FitMapToView。
```

地图窗口已经被用户操作过后：

```text
后续 RenderMap / 关闭再打开 / 隐藏再恢复，不应重置缩放和平移。
```

### 4.3 Session 状态只存在内存中

状态生命周期：

```text
程序启动 -> 初始化空 Session 状态
程序运行 -> 不断更新 Session 状态
程序真正退出 -> 状态自然丢失
```

不写文件，不处理 JSON 损坏。

## 5. 新增运行期状态模型

建议新增模型：

```text
VSLoader/Models/RuntimeLayoutState.cs
VSLoader/Models/FactoryMapViewState.cs
```

### 5.1 RuntimeLayoutState

```csharp
namespace VSLoader.Models;

public sealed class RuntimeLayoutState
{
    public bool HasMainWindowBounds { get; set; }

    public double MainLeft { get; set; }

    public double MainTop { get; set; }

    public double MainWidth { get; set; }

    public double MainHeight { get; set; }

    public bool WasFactoryMapOpen { get; set; }

    public bool HasFactoryMapBounds { get; set; }

    public double FactoryMapLeft { get; set; }

    public double FactoryMapTop { get; set; }

    public double FactoryMapWidth { get; set; }

    public double FactoryMapHeight { get; set; }

    public FactoryMapViewState? FactoryMapView { get; set; }
}
```

### 5.2 FactoryMapViewState

```csharp
namespace VSLoader.Models;

public sealed class FactoryMapViewState
{
    public double FitScale { get; set; }

    public double UserScale { get; set; }

    public double OffsetX { get; set; }

    public double OffsetY { get; set; }
}
```

说明：

```text
FitScale + UserScale + OffsetX + OffsetY 足够恢复地图视图。
```

## 6. MainWindow 侧状态管理

### 6.1 新增字段

在 `MainWindow.xaml.cs` 中新增：

```csharp
private readonly RuntimeLayoutState _runtimeLayoutState = new();
private bool _isApplyingRuntimeLayout;
```

用途：

```text
_runtimeLayoutState：
    保存本次运行期间的窗口和地图状态。

_isApplyingRuntimeLayout：
    避免程序代码应用布局时触发 LocationChanged / SizeChanged 反向覆盖状态。
```

### 6.2 保存主窗口 Bounds

在 `MainWindow_LocationOrSizeChanged` 中：

```text
1. 如果窗口是 Normal。
2. 如果不是正在应用运行期布局。
3. 保存 MainLeft / MainTop / MainWidth / MainHeight。
4. HasMainWindowBounds = true。
```

伪代码：

```csharp
private void SaveMainWindowBoundsToSession()
{
    if (_isApplyingRuntimeLayout || WindowState != WindowState.Normal)
    {
        return;
    }

    _runtimeLayoutState.HasMainWindowBounds = true;
    _runtimeLayoutState.MainLeft = Left;
    _runtimeLayoutState.MainTop = Top;
    _runtimeLayoutState.MainWidth = ActualWidth > 0 ? ActualWidth : Width;
    _runtimeLayoutState.MainHeight = ActualHeight > 0 ? ActualHeight : Height;
}
```

### 6.3 默认布局只用于首次无 Session 状态

`ApplyDefaultWindowLayoutOnce()` 应改为：

```text
如果 _runtimeLayoutState.HasMainWindowBounds 为 true，则恢复 Session 中主窗口位置。
否则应用默认 WorkArea 比例布局。
```

但本次启动时 Session 一定为空。

该逻辑主要服务于：

```text
程序运行期间隐藏后恢复时，如果需要重放主窗口 Bounds。
```

### 6.4 恢复主窗口时不重算默认比例

`RestoreAndActivate()` 中：

```text
只负责 Show / WindowState Normal / Activate。
不调用 ApplyDefaultWindowLayoutOnce。
不强制重算默认比例。
```

如果主窗口有 Session Bounds，可在恢复前或恢复后调用：

```csharp
RestoreMainWindowBoundsFromSession()
```

但注意：

```text
如果用户只是最小化窗口，WPF 自己通常会保留原位置。
不需要每次都强制设置。
```

推荐做法：

```text
只在窗口从 Hide 状态恢复时，如果 HasMainWindowBounds，则恢复主窗口 Bounds。
```

## 7. FactoryMapWindow 侧视图状态

### 7.1 新增公开方法

在 `FactoryMapWindow.xaml.cs` 中新增：

```csharp
public FactoryMapViewState CaptureViewState()
{
    return new FactoryMapViewState
    {
        FitScale = fitScale,
        UserScale = userScale,
        OffsetX = mapOffsetX,
        OffsetY = mapOffsetY
    };
}
```

新增：

```csharp
public void RestoreViewState(FactoryMapViewState? state)
{
    if (state is null)
    {
        return;
    }

    fitScale = state.FitScale > 0 ? state.FitScale : 1.0;
    userScale = state.UserScale > 0 ? state.UserScale : 1.0;
    mapOffsetX = state.OffsetX;
    mapOffsetY = state.OffsetY;
    ApplyMapTransform();
    RefreshStatusText();
}
```

### 7.2 RenderMap 支持是否重置视图

当前 `RenderMap` 大概率会：

```text
RenderCurrentMap(resetView: true)
```

本次需要改成可控：

```csharp
public void RenderMap(FactoryMapDeviceViewData map, bool resetView)
{
    currentMap = map;
    RenderCurrentMap(resetView);
}
```

保留兼容方法：

```csharp
public void RenderMap(FactoryMapDeviceViewData map)
{
    RenderMap(map, resetView: true);
}
```

MainWindow 在本次 Session 有地图视图状态时：

```text
RenderMap(loadResult.Map, resetView: false)
RestoreViewState(_runtimeLayoutState.FactoryMapView)
```

无状态时：

```text
RenderMap(loadResult.Map, resetView: true)
```

## 8. 保存地图窗口 Bounds 和 View

### 8.1 保存时机

以下时机保存地图 Session 状态：

```text
1. 地图窗口隐藏前。
2. 地图窗口关闭前。
3. 主窗口最小化导致地图隐藏前。
4. 快捷键隐藏程序前。
5. 地图窗口移动或缩放后。
6. 地图缩放或平移后。
```

### 8.2 MainWindow 保存地图状态

在 `MainWindow.xaml.cs` 中新增：

```csharp
private void SaveFactoryMapStateToSession()
{
    if (_factoryMapWindow is null)
    {
        return;
    }

    _runtimeLayoutState.WasFactoryMapOpen = _isFactoryMapOpen;
    _runtimeLayoutState.HasFactoryMapBounds = true;
    _runtimeLayoutState.FactoryMapLeft = _factoryMapWindow.Left;
    _runtimeLayoutState.FactoryMapTop = _factoryMapWindow.Top;
    _runtimeLayoutState.FactoryMapWidth = _factoryMapWindow.ActualWidth > 0 ? _factoryMapWindow.ActualWidth : _factoryMapWindow.Width;
    _runtimeLayoutState.FactoryMapHeight = _factoryMapWindow.ActualHeight > 0 ? _factoryMapWindow.ActualHeight : _factoryMapWindow.Height;
    _runtimeLayoutState.FactoryMapView = _factoryMapWindow.CaptureViewState();
}
```

### 8.3 地图视图变化后通知 MainWindow

为了让缩放/平移实时保存，可在 `FactoryMapWindow` 增加事件：

```csharp
public event EventHandler? ViewStateChanged;
```

在以下地方触发：

```text
1. MouseWheel 缩放后。
2. EndMapDrag 后。
3. RestoreViewState 后不触发，避免循环。
```

MainWindow 创建地图窗口时订阅：

```csharp
_factoryMapWindow.ViewStateChanged += (_, _) => SaveFactoryMapStateToSession();
```

说明：

```text
节点拖拽改变的是地图节点布局，不是地图视图比例，不需要触发 ViewStateChanged。
```

## 9. 地图关闭/隐藏逻辑

### 9.1 点击“地图”按钮关闭地图

当前逻辑可能是：

```csharp
_factoryMapWindow.Close();
_factoryMapWindow = null;
```

本次关闭前必须：

```text
SaveFactoryMapStateToSession();
```

然后：

```text
_isFactoryMapOpen = false;
_runtimeLayoutState.WasFactoryMapOpen = false;
```

注意：

```text
即使用户关闭地图，地图 Bounds 和 View 仍可以保留。
下次本次 Session 内再打开地图时，可恢复上一次地图窗口大小和视图。
```

### 9.2 主窗口最小化隐藏地图

当前最小化会：

```text
HideFactoryMapWindow()
```

隐藏前必须：

```text
SaveFactoryMapStateToSession();
```

并且：

```text
不要把 _isFactoryMapOpen 改成 false。
```

因为这是临时隐藏，恢复时应重新显示地图。

## 10. 快捷键修复

### 10.1 当前问题

快捷键在窗口显示时，可能进入了类似：

```csharp
WindowState = WindowState.Minimized;
```

随后 `StateChanged` 或其它逻辑触发：

```text
ShowFactoryMapIfNeeded()
RefreshFactoryMap()
RenderMap(resetView: true)
```

导致地图先被重置。

### 10.2 目标行为

快捷键逻辑：

```text
如果主窗口显示且不是最小化：
    保存主窗口和地图 Session 状态
    如果地图窗口存在，隐藏地图窗口
    最小化或 Hide 主窗口
    return

如果主窗口隐藏或最小化：
    恢复主窗口
    如果 _isFactoryMapOpen == true，恢复地图窗口
    恢复地图 Bounds 和 ViewState
```

关键：

```text
隐藏路径中不能调用 ShowFactoryMapIfNeeded。
```

### 10.3 RestoreAndActivate 修复

`RestoreAndActivate()` 中保留：

```text
Show()
WindowState = Normal
Activate()
ShowFactoryMapIfNeeded()
```

但 `ShowFactoryMapIfNeeded()` 内部必须能判断：

```text
如果有 Session Map View，则 RenderMap(resetView: false) 并 RestoreViewState。
```

## 11. ShowFactoryMapIfNeeded 改造

### 11.1 创建窗口时恢复 Bounds

创建 `_factoryMapWindow` 后：

```text
如果 _runtimeLayoutState.HasFactoryMapBounds：
    使用 Session 中保存的 Left/Top/Width/Height
否则：
    使用 PositionFactoryMapWindow() 默认并排布局
```

注意：

```text
如果 Session Bounds 超出当前 WorkArea，需要 Clamp 回工作区内。
```

### 11.2 RefreshFactoryMap 改造

当前刷新地图后可能默认重置视图。

改成：

```csharp
var hasViewState = _runtimeLayoutState.FactoryMapView is not null;
_factoryMapWindow.RenderMap(loadResult.Map, resetView: !hasViewState);
if (hasViewState)
{
    _factoryMapWindow.RestoreViewState(_runtimeLayoutState.FactoryMapView);
}
```

注意：

```text
首次打开地图时没有 ViewState，允许 resetView。
后续只要用户操作过地图视图，就不 reset。
```

## 12. 区分地图布局和地图视图

必须明确：

```text
地图布局：
    节点 X/Y、连线 edges。
    保存到 factory-map.layout.json。

地图视图：
    缩放比例、平移偏移、地图窗口位置大小。
    只保存在内存 RuntimeLayoutState。
```

本次不能把地图视图写入 `factory-map.layout.json`。

原因：

```text
factory-map.layout.json 是可导入导出的图文件。
视图状态是用户当前临时浏览状态，不应污染图文件。
```

## 13. 小屏和分辨率变化处理

虽然本次状态只在内存中，但用户可能运行期间切换分辨率或外接屏。

恢复窗口 Bounds 时必须做工作区校正：

```text
1. Left 不小于 WorkArea.Left。
2. Top 不小于 WorkArea.Top。
3. Right 不超过 WorkArea.Right。
4. Bottom 不超过 WorkArea.Bottom。
5. Width 不小于 MinWidth。
6. Height 不小于 MinHeight。
```

可以新增：

```csharp
private Rect ClampBoundsToWorkArea(Rect bounds, double minWidth, double minHeight)
```

## 14. 自动化测试建议

可以对纯状态模型不做测试。

本次重点测试：

```text
dotnet build
dotnet test
手工验收
```

如果抽出 `ClampBoundsToWorkArea` 为 internal static，可新增简单测试：

```text
1. 超出右侧的窗口会被移回工作区内。
2. 小于最小宽度的窗口会扩到 MinWidth。
```

本次不强制。

## 15. 手工验收

### 15.1 快捷键不重置地图

1. 打开 VSLoader。
2. 打开地图。
3. 放大地图并平移到某个位置。
4. 按程序快捷键。
5. 程序应直接隐藏/最小化。
6. 不应先把地图还原。
7. 再按快捷键恢复。
8. 地图缩放和平移保持隐藏前状态。

### 15.2 关闭地图再打开保持视图

1. 打开地图。
2. 放大并平移地图。
3. 点击“地图”按钮关闭地图。
4. 再点击“地图”按钮打开地图。
5. 地图窗口大小、位置、缩放、平移保持关闭前状态。

### 15.3 主窗口移动后保持

1. 拖动主窗口到新位置。
2. 打开地图。
3. 地图贴合当前主窗口或恢复本次 Session 保存位置。
4. 最小化再恢复。
5. 主窗口保持用户调整后的位置。

### 15.4 地图窗口大小保持

1. 打开地图。
2. 手动调整地图窗口大小。
3. 关闭地图。
4. 再打开地图。
5. 地图窗口恢复刚才大小。

### 15.5 程序真正退出后不要求恢复

1. 调整主窗口和地图状态。
2. 托盘退出程序。
3. 重新启动程序。
4. 程序使用默认 WorkArea 比例布局即可。

## 16. 预计修改文件

预计新增：

```text
VSLoader/Models/RuntimeLayoutState.cs
VSLoader/Models/FactoryMapViewState.cs
```

预计修改：

```text
VSLoader/MainWindow.xaml.cs
VSLoader/Views/FactoryMapWindow.xaml.cs
```

不预计修改：

```text
VSLoader/Models/Services/FactoryMapLayoutService.cs
地图布局 JSON 结构
批量导入逻辑
AdminUI/WebUI 逻辑
```

## 17. 构建验证

执行：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader
dotnet test .\VSLoader.sln -p:UseSharedCompilation=false
dotnet build .\VSLoader.sln -p:UseSharedCompilation=false
```

如果 `VSLoader.exe` 正在运行导致文件占用：

```powershell
Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force
```

## 18. 验收标准

最终必须满足：

```text
1. 快捷键隐藏程序时不重置地图缩放和平移。
2. 快捷键恢复程序时地图视图保持隐藏前状态。
3. 地图关闭再打开时，地图窗口 Bounds 保持本次 Session 上一次状态。
4. 地图关闭再打开时，地图缩放和平移保持本次 Session 上一次状态。
5. 主窗口位置和大小在本次 Session 内不被默认布局反复覆盖。
6. 程序真正退出后不要求恢复 Session 状态。
7. 地图布局 JSON 不新增视图字段。
8. 原有节点拖拽、多选、连线、导入导出不回退。
9. dotnet test 通过。
10. dotnet build 0 错误。
```
