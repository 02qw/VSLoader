# v63 主窗口首次默认布局稳定应用与Session边界修复编码规格说明

## 1. 需求背景

当前 VSLoader 已经实现：

```text
1. 程序首次打开时，主窗口按屏幕 WorkArea 比例计算默认位置和大小。
2. 本次运行期间，用户移动/缩放主窗口后，通过内存 Session 状态保持。
3. 托盘右键退出后，程序真正关闭，内存 Session 状态丢失。
4. 下次重新启动时，应重新按默认比例计算主窗口位置和大小。
```

当前问题：

```text
托盘区真正退出程序后，再重新打开 VSLoader，主界面第一次打开的位置和比例偶尔不一致。
```

用户期望：

```text
每次真正退出并重新启动程序后，主界面第一次打开都使用同一套稳定的默认比例和位置。
```

## 2. 问题分析

当前代码中默认布局由：

```text
VSLoader/MainWindow.xaml.cs
```

中的以下流程控制：

```csharp
Loaded += MainWindow_Loaded;
```

```csharp
private void MainWindow_Loaded(object sender, RoutedEventArgs e)
{
    ApplyDefaultWindowLayoutOnce();
    ...
}
```

`ApplyDefaultMainWindowLayout()` 中应用默认布局后又立即调用：

```csharp
SaveMainWindowBoundsToSession();
```

这会造成两个风险：

```text
1. Loaded 时机偏早，窗口可能还没有完成最终渲染、DPI、Chrome、WorkArea 计算。
2. 默认布局刚应用完就被保存为 RuntimeLayoutState，语义上把“默认布局”误当成“用户运行期调整状态”。
```

虽然 RuntimeLayoutState 不跨进程持久化，但每次启动过程中 WPF 的中间布局事件可能略有差异，从而让首次默认位置看起来不稳定。

## 3. 需求目标

本次目标：

```text
修复主窗口首次默认布局的应用时机和 Session 保存边界。
```

完成后：

```text
1. 程序真正退出后重新启动，主窗口首次默认位置和比例保持稳定。
2. 默认布局只负责设置初始位置和大小，不应立刻写入 RuntimeLayoutState。
3. 用户后续手动移动/缩放窗口后，才写入 RuntimeLayoutState。
4. 本次运行期间隐藏/恢复仍保留用户调整后的窗口状态。
5. 不影响地图窗口默认布局和地图视图 Session 逻辑。
```

## 4. 非目标范围

本次不实现：

```text
1. 跨进程持久化窗口位置。
2. 配置中心新增窗口布局配置。
3. 重置窗口布局按钮。
4. 多显示器复杂记忆策略。
5. 修改地图默认比例参数。
6. 修改托盘菜单功能。
7. 修改快捷键显示/隐藏逻辑。
```

本次只做：

```text
1. 调整主窗口首次默认布局应用时机。
2. 修复默认布局不应写入 RuntimeLayoutState 的问题。
3. 保持用户手动操作后的 Session 保存能力。
```

## 5. 核心原则

### 5.1 默认布局和用户 Session 状态必须分离

默认布局：

```text
程序启动后第一次展示时，按 WorkArea 比例计算出的窗口位置和大小。
```

用户 Session 状态：

```text
用户在本次运行期间手动移动或缩放窗口后的状态。
```

两者不能混用。

### 5.2 默认布局不应直接保存为 Session

禁止在默认布局方法中调用：

```csharp
SaveMainWindowBoundsToSession();
```

原因：

```text
默认布局不是用户操作。
默认布局写入 RuntimeLayoutState 后，会让后续恢复逻辑误以为用户已经调整过窗口。
```

### 5.3 首次默认布局应在窗口渲染稳定后应用

不推荐在：

```text
Loaded
```

中立即应用窗口默认布局。

推荐使用：

```text
ContentRendered
```

或：

```csharp
Dispatcher.BeginInvoke(..., DispatcherPriority.ApplicationIdle)
```

本次推荐：

```text
Loaded 仍负责 ViewModel、事件、快捷键初始化。
ContentRendered 负责首次默认布局。
```

## 6. 预计修改文件

预计修改：

```text
VSLoader/MainWindow.xaml.cs
```

不预计修改：

```text
VSLoader/MainWindow.xaml
VSLoader/Views/FactoryMapWindow.xaml.cs
VSLoader/Models/RuntimeLayoutState.cs
VSLoader/Models/FactoryMapViewState.cs
```

## 7. 具体设计

### 7.1 新增 ContentRendered 事件

在构造函数中新增：

```csharp
ContentRendered += MainWindow_ContentRendered;
```

保留：

```csharp
Loaded += MainWindow_Loaded;
```

### 7.2 MainWindow_Loaded 不再应用默认布局

当前：

```csharp
private void MainWindow_Loaded(object sender, RoutedEventArgs e)
{
    ApplyDefaultWindowLayoutOnce();
    ...
}
```

应修改为：

```csharp
private void MainWindow_Loaded(object sender, RoutedEventArgs e)
{
    // 只保留 DataContext、事件、快捷键初始化。
}
```

默认布局改由：

```csharp
private void MainWindow_ContentRendered(object? sender, EventArgs e)
{
    Dispatcher.BeginInvoke(new Action(ApplyDefaultWindowLayoutOnce), DispatcherPriority.ApplicationIdle);
}
```

说明：

```text
ContentRendered 后再延迟到 ApplicationIdle，可以避开 WPF 初次布局过程中的中间 SizeChanged/LocationChanged。
```

### 7.3 ApplyDefaultMainWindowLayout 不保存 Session

当前：

```csharp
ApplyWindowBounds(...);
SaveMainWindowBoundsToSession();
```

应修改为：

```csharp
ApplyWindowBounds(...);
```

不再调用：

```csharp
SaveMainWindowBoundsToSession();
```

### 7.4 避免默认布局应用过程触发 SaveMainWindowBoundsToSession

当前 `ApplyWindowBounds` 已经设置：

```csharp
_isApplyingRuntimeLayout = true;
```

`SaveMainWindowBoundsToSession` 中已有：

```csharp
if (_isApplyingRuntimeLayout || WindowState != WindowState.Normal)
{
    return;
}
```

因此默认布局过程中触发的 `LocationChanged` / `SizeChanged` 不会保存 Session。

本次需要确认该逻辑保持不变。

### 7.5 用户手动移动/缩放仍保存 Session

保留：

```csharp
LocationChanged += MainWindow_LocationOrSizeChanged;
SizeChanged += MainWindow_LocationOrSizeChanged;
```

保留：

```csharp
private void MainWindow_LocationOrSizeChanged(object? sender, EventArgs e)
{
    SaveMainWindowBoundsToSession();
    ...
}
```

说明：

```text
默认布局应用过程中不会保存。
用户后续手动移动/缩放会保存。
```

## 8. 行为细节

### 8.1 程序第一次启动

流程：

```text
1. 构造 MainWindow。
2. Loaded 初始化 ViewModel、快捷键等。
3. ContentRendered 触发。
4. Dispatcher ApplicationIdle 时调用 ApplyDefaultWindowLayoutOnce。
5. 使用 WorkArea 默认比例设置窗口。
6. 不写入 RuntimeLayoutState。
```

### 8.2 本次运行期间用户移动窗口

流程：

```text
1. 用户拖动或缩放窗口。
2. LocationChanged / SizeChanged 触发。
3. SaveMainWindowBoundsToSession 写入 RuntimeLayoutState。
4. 后续快捷键隐藏/恢复时恢复用户位置。
```

### 8.3 托盘右键退出后重新打开

流程：

```text
1. 程序进程结束。
2. RuntimeLayoutState 丢失。
3. 重新启动后没有 Session Bounds。
4. ContentRendered 后重新按默认比例布局。
```

期望：

```text
同一屏幕环境下，每次第一次打开位置和比例一致。
```

## 9. 风险与注意事项

### 9.1 ContentRendered 后窗口可能短暂出现默认 XAML 尺寸

XAML 中当前有：

```xml
Width="960"
Height="720"
```

如果默认布局延迟到 ContentRendered + ApplicationIdle，理论上窗口可能先以 XAML 尺寸短暂显示，再跳到默认比例位置。

如果闪动明显，可改为：

```text
ContentRendered 直接 ApplyDefaultWindowLayoutOnce，不再额外 BeginInvoke。
```

但本次优先保证稳定性。

### 9.2 不要影响恢复 Session

如果本次运行期间已经有：

```text
_runtimeLayoutState.HasMainWindowBounds = true
```

则 `ApplyDefaultWindowLayoutOnce` 仍可恢复 Session Bounds。

但注意：

```text
默认布局本身不应该制造 HasMainWindowBounds。
```

### 9.3 不要修改默认比例参数

本次不调整：

```csharp
DefaultLayoutLeftRatio
DefaultLayoutTopRatio
DefaultLayoutMainWidthRatio
DefaultLayoutMainHeightRatio
DefaultLayoutMapHeightRatio
```

这些参数属于 v59 需求，当前问题是时机和保存边界，不是比例值。

## 10. 手工验收

### 10.1 大退重开一致性

步骤：

```text
1. 启动 VSLoader。
2. 观察主窗口首次位置和大小。
3. 托盘右键退出。
4. 重新启动 VSLoader。
5. 重复 3 次。
```

期望：

```text
每次首次打开位置和大小一致。
```

### 10.2 本次 Session 用户调整仍生效

步骤：

```text
1. 启动 VSLoader。
2. 手动移动或缩放主窗口。
3. 使用快捷键隐藏。
4. 再使用快捷键恢复。
```

期望：

```text
恢复用户调整后的位置和大小。
```

### 10.3 真正退出后不保留用户调整

步骤：

```text
1. 启动 VSLoader。
2. 手动移动主窗口到其它位置。
3. 托盘右键退出。
4. 重新启动 VSLoader。
```

期望：

```text
不恢复上一次手动移动位置。
重新使用默认比例位置。
```

### 10.4 地图窗口不退化

步骤：

```text
1. 启动 VSLoader。
2. 点击地图。
```

期望：

```text
地图窗口仍按 v59 默认逻辑贴在主窗口右侧。
```

## 11. 自动化验证

执行：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader
dotnet test .\VSLoader.sln -p:UseSharedCompilation=false
dotnet build .\VSLoader.sln -p:UseSharedCompilation=false
```

如遇文件占用：

```powershell
Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force
```

## 12. 验收标准

最终必须满足：

```text
1. 大退重开后，主窗口首次默认位置和比例稳定一致。
2. 默认布局不再直接写入 RuntimeLayoutState。
3. 用户手动移动/缩放后，本次 Session 隐藏/恢复仍保持用户状态。
4. 真正退出后，不保留上一次用户移动/缩放状态。
5. 地图默认布局不退化。
6. dotnet test 通过。
7. dotnet build 0 错误。
```
