# v152 标题栏 SVG 图标与工作区最大化窗口化编码规格说明

## 1. 背景

当前 VSLoader 已经使用自定义标题栏：

```text
VSLoader\Views\Controls\ModernTitleBar.xaml
VSLoader\Views\Controls\ModernTitleBar.xaml.cs
```

用户已将新的标题栏按钮 SVG 资源放入：

```text
VSLoader\Assets
```

当前资源包括：

```text
MiniMize.svg：最小化
Max.svg：最大化
Windowed.svg：窗口化 / 还原
Close.svg：关闭
```

当前标题栏按钮仍然是 XAML 手画图形：

```text
最小化：Line
最大化：Rectangle
窗口化：两个 Rectangle
关闭：两条交叉 Line
```

这导致标题栏图标和用户提供的新资源不一致。

同时，当前最大化 / 窗口化行为也存在语义问题：

```text
软件最大化看起来像“假最大化”。
窗口化恢复时没有体现真正恢复到用户上一次正常窗口尺寸。
工厂地图窗口额外写了 ApplyMaximizedWorkingArea，和通用标题栏逻辑存在重复。
```

本次需要把“图标资源替换”和“最大化/窗口化真实行为”合并处理。

## 2. 当前代码现状

### 2.1 统一标题栏控件

统一标题栏位于：

```text
VSLoader\Views\Controls\ModernTitleBar.xaml
VSLoader\Views\Controls\ModernTitleBar.xaml.cs
```

已被以下窗口使用：

```text
VSLoader\MainWindow.xaml
VSLoader\Views\FactoryMapWindow.xaml
VSLoader\Views\SettingsWindow.xaml
VSLoader\Views\BatchImportWindow.xaml
VSLoader\Views\ShortcutEditWindow.xaml
VSLoader\Views\WorkspaceSelectorWindow.xaml
VSLoader\Views\WorkspaceNameDialog.xaml
VSLoader\Views\MessageDialogWindow.xaml
```

因此优先修改 `ModernTitleBar`，不要在各个窗口中重复实现。

### 2.2 当前最大化逻辑

当前 `ModernTitleBar.xaml.cs` 中最大化按钮逻辑为：

```csharp
window.WindowState = window.WindowState == WindowState.Maximized
    ? WindowState.Normal
    : WindowState.Maximized;
```

该逻辑只依赖 WPF 默认 `WindowState`。

### 2.3 工厂地图窗口额外最大化逻辑

`FactoryMapWindow.xaml.cs` 目前存在额外逻辑：

```text
FactoryMapWindow_StateChanged
ApplyMaximizedWorkingArea
ClearMaximizedWorkingAreaConstraint
GetCurrentScreenWorkingArea
ConvertScreenBoundsToDip
isApplyingMaximizedWorkingArea
```

该逻辑在 `WindowState == Maximized` 时手动设置：

```text
MaxWidth
MaxHeight
Left
Top
Width
Height
```

这会造成标题栏通用逻辑和地图窗口私有逻辑重复处理最大化状态。

## 3. 本阶段目标

本阶段目标：

```text
1. 标题栏最小化、最大化、窗口化、关闭按钮统一使用用户提供的 SVG 图标语义。
2. 图标支持跟随按钮 Foreground 变色。
3. 关闭按钮 hover / pressed 时继续保持红色危险语义。
4. 最大化必须是当前显示器工作区最大化，不覆盖 Windows 任务栏。
5. 窗口化必须恢复到最大化前用户真实的窗口尺寸和位置。
6. 主窗口、地图窗口、设置窗口、弹窗等使用 ModernTitleBar 的窗口行为统一。
7. ResizeMode=NoResize 的窗口继续隐藏最大化按钮。
8. 移除或停用工厂地图窗口里重复的最大化工作区修正逻辑。
9. 不破坏地图窗口独立窗口、地图快捷键、主窗口快捷键、关闭接管等既有逻辑。
```

用户体验目标：

```text
用户点击最大化：窗口铺满当前显示器工作区，但不盖住任务栏。
用户点击窗口化：窗口回到最大化前的大小和位置。
用户看到的图标和窗口状态一致。
关闭按钮仍然有清晰红色反馈。
```

## 4. 非目标

本阶段不做：

```text
1. 不重构主窗口和地图窗口的快捷键业务逻辑。
2. 不修改地图渲染、连线、节点、视图状态保存逻辑。
3. 不修改更新器窗口标题栏，除非更新器项目也显式使用同一套 ModernTitleBar。
4. 不引入第三方 SVG 渲染库。
5. 不改应用主图标 tomato.ico。
6. 不调整标题栏整体高度、文字、窗口阴影和外壳颜色。
```

## 5. SVG 图标处理规范

### 5.1 不直接用 Image 渲染 SVG

WPF 原生不会稳定地把 SVG 当作可变色矢量控件渲染。

本阶段不建议新增第三方库。

建议做法：

```text
从 SVG 中提取 path d。
转换为 WPF Geometry / Path Data。
在 XAML 中使用 Path 绘制图标。
Fill 绑定到按钮 Foreground。
```

### 5.2 建议新增图标资源

建议在以下文件之一集中定义标题栏图标资源：

```text
VSLoader\Styles\ModernWindowChrome.xaml
```

或新增：

```text
VSLoader\Styles\ModernTitleBarIcons.xaml
```

如果新增资源字典，需要在：

```text
VSLoader\App.xaml
```

合并该资源字典。

建议资源名：

```text
ModernTitleBarMinimizeIconGeometry
ModernTitleBarMaximizeIconGeometry
ModernTitleBarWindowedIconGeometry
ModernTitleBarCloseIconGeometry
```

### 5.3 图标颜色

SVG 文件中现有 `fill="#2c2c2c"` 不能作为最终颜色。

最终图标应使用：

```xml
Fill="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}"
```

这样才能支持：

```text
普通状态：ModernTitleBarIconBrush
hover 状态：ModernTitleBarIconHoverBrush
关闭 hover：ModernTitleBarCloseIconHoverBrush
```

### 5.4 图标尺寸

标题栏按钮仍保持：

```text
按钮尺寸：44 x 44
图标视觉区域：建议 14 x 14 或 15 x 15
```

由于 SVG viewBox 为 `0 0 1024 1024`，建议使用：

```xml
<Viewbox Width="14" Height="14">
    <Path Width="1024"
          Height="1024"
          Stretch="Uniform"
          Data="{StaticResource ...}" />
</Viewbox>
```

或等效写法。

要求：

```text
四个图标视觉大小一致。
窗口化图标不能明显大于最大化图标。
关闭图标不能显得过粗或贴边。
```

## 6. 标题栏按钮行为规范

### 6.1 最小化

点击最小化：

```text
OwnerWindow.WindowState = WindowState.Minimized
```

保持现有逻辑即可。

### 6.2 最大化

点击最大化时，不使用覆盖任务栏的全屏。

正确行为：

```text
记录当前正常窗口 bounds。
计算当前窗口所在屏幕的 WorkingArea。
将窗口切换到工作区最大化状态。
窗口 Left/Top/Width/Height 等于当前屏幕 WorkingArea 转换后的 DIP 坐标。
图标切换为 Windowed.svg 对应的窗口化图标。
```

注意：

```text
最大化不是进入真正独占全屏。
不能覆盖任务栏。
多显示器时必须使用窗口当前所在屏幕。
```

### 6.3 窗口化 / 还原

点击窗口化时：

```text
恢复到最大化前记录的正常窗口 bounds。
图标切换为 Max.svg 对应的最大化图标。
```

如果没有可用的正常窗口 bounds：

```text
使用 Window.RestoreBounds。
如果 RestoreBounds 不可用，再使用窗口当前 Width/Height 限制后的安全默认值。
```

要求：

```text
不能恢复成很小的异常窗口。
不能丢失用户最大化前的窗口尺寸。
不能把窗口恢复到屏幕外。
```

### 6.4 关闭

关闭按钮继续保持现有语义：

```text
如果 CloseRequested 有订阅，优先触发 CloseRequested。
否则 OwnerWindow.Close()。
```

不能破坏地图窗口关闭接管逻辑：

```text
FactoryMapWindow 中 MapTitleBar.CloseRequested 由主窗口接管。
```

关闭按钮 hover / pressed 保留红色语义。

## 7. 工作区最大化状态设计

### 7.1 推荐实现方式

在 `ModernTitleBar.xaml.cs` 中实现一套轻量状态：

```text
private Rect? normalWindowBounds;
private bool isWorkspaceMaximized;
private bool isApplyingWorkspaceBounds;
```

含义：

```text
normalWindowBounds：进入工作区最大化前的正常窗口位置和大小。
isWorkspaceMaximized：当前是否处于标题栏控制的工作区最大化状态。
isApplyingWorkspaceBounds：避免设置窗口尺寸时触发递归或错误保存。
```

### 7.2 Bounds 记录时机

进入工作区最大化前记录 bounds：

```text
WindowState == Normal
Width / Height 是有效数字
Left / Top 是有效数字
窗口没有最小化
```

不要在以下情况覆盖正常 bounds：

```text
窗口已经处于工作区最大化。
窗口最小化。
正在应用工作区最大化尺寸。
窗口尺寸异常。
```

### 7.3 当前屏幕识别

根据窗口句柄获取当前屏幕：

```text
new WindowInteropHelper(window).Handle
System.Windows.Forms.Screen.FromHandle(handle)
```

如果 handle 无效：

```text
使用 PrimaryScreen 或 AllScreens[0] 作为兜底。
```

工作区坐标要从设备像素转换为 WPF DIP：

```text
PresentationSource.FromVisual(window).CompositionTarget.TransformFromDevice
```

### 7.4 屏幕外保护

还原窗口时应确保目标 bounds 至少部分位于当前可用屏幕内。

建议规则：

```text
如果 normalWindowBounds 与任意屏幕 WorkingArea 没有交集，则把窗口放回当前屏幕工作区中央。
如果 Width/Height 小于窗口 MinWidth/MinHeight，则按 MinWidth/MinHeight 修正。
如果 Width/Height 大于当前工作区，则限制到当前工作区的 90% 或工作区大小。
```

不要让窗口恢复到用户看不见的位置。

## 8. 与 WindowState 的关系

本阶段有两种可选实现。

### 8.1 推荐方案：自定义工作区最大化，不依赖 WindowState.Maximized

推荐：

```text
保持 WindowState = Normal。
通过 Left/Top/Width/Height 模拟工作区最大化。
用 isWorkspaceMaximized 表达标题栏状态。
```

原因：

```text
WindowStyle=None + WindowChrome + WindowState.Maximized 在 WPF 中容易出现任务栏覆盖、边框尺寸异常、RestoreBounds 不稳定等问题。
当前需求明确是“工作区全屏”，不是系统意义上的真正 Maximized。
```

该方案下：

```text
最大化图标切换不能只看 WindowState。
必须看 isWorkspaceMaximized。
```

### 8.2 兼容方案：保留 WindowState.Maximized，但统一工作区约束

如果实现中必须保留 `WindowState.Maximized`：

```text
ModernTitleBar 统一处理 WorkingArea。
FactoryMapWindow 不再单独处理 WorkingArea。
```

但该方案更容易继续出现“假最大化”和还原尺寸污染。

本规格推荐 8.1。

## 9. 工厂地图窗口处理要求

`FactoryMapWindow.xaml.cs` 中重复逻辑需要处理。

建议移除或停用：

```text
isApplyingMaximizedWorkingArea
FactoryMapWindow_StateChanged
ApplyMaximizedWorkingArea
ClearMaximizedWorkingAreaConstraint
GetCurrentScreenWorkingArea
ConvertScreenBoundsToDip
```

同时移除构造函数中的：

```csharp
StateChanged += FactoryMapWindow_StateChanged;
```

如果 `GetCurrentScreenWorkingArea` / `ConvertScreenBoundsToDip` 只有该逻辑使用，也一并删除。

注意：

```text
不要删除地图窗口视图状态保存逻辑。
不要影响地图关闭保存 bounds 的主窗口逻辑。
不要影响地图 Alt+X 快捷键的最小化 / 激活行为。
```

## 10. 需要修改的文件

必须修改：

```text
VSLoader\Views\Controls\ModernTitleBar.xaml
VSLoader\Views\Controls\ModernTitleBar.xaml.cs
VSLoader\Styles\ModernWindowChrome.xaml
VSLoader\Views\FactoryMapWindow.xaml.cs
```

可能修改：

```text
VSLoader\App.xaml
VSLoader\VSLoader.csproj
VSLoader.Tests\ModernTitleBarTests.cs
VSLoader.Tests\FactoryMapWindowRuntimeLoadTests.cs
```

说明：

```text
如果图标 Path 资源直接写入 ModernWindowChrome.xaml，则不需要新增资源字典。
如果 SVG 文件只是作为原始素材，不参与运行时读取，则 VSLoader.csproj 不一定必须纳入这几个 SVG。
如果实现选择运行时读取 SVG，则必须将 SVG 作为 Resource 纳入项目。
```

本规格推荐：

```text
运行时不读取 SVG。
把 SVG path 转为 XAML Geometry。
SVG 文件保留在 Assets 作为设计源文件。
```

## 11. 测试要求

### 11.1 标题栏资源测试

建议新增或补充测试，检查：

```text
ModernTitleBarMinimizeIconGeometry 存在。
ModernTitleBarMaximizeIconGeometry 存在。
ModernTitleBarWindowedIconGeometry 存在。
ModernTitleBarCloseIconGeometry 存在。
ModernTitleBar.xaml 不再包含旧的手画 Line 关闭图标。
ModernTitleBar.xaml 不再包含旧的 Rectangle 最大化/窗口化图标。
图标 Path Fill 绑定 Foreground。
```

### 11.2 最大化行为测试

如果适合写纯逻辑测试，建议将 bounds 计算抽到小 helper：

```text
ModernWindowBoundsService
```

可测试：

```text
正常 bounds 进入工作区最大化前会被记录。
工作区最大化 bounds 使用当前屏幕 WorkingArea。
窗口化时恢复记录的 normal bounds。
normal bounds 屏幕外时会回到当前屏幕可见区域。
```

如果不抽 helper，至少补充源码结构测试：

```text
ModernTitleBar.xaml.cs 包含 isWorkspaceMaximized 或等效状态。
ModernTitleBar.xaml.cs 不再只用 WindowState.Maximized 判断图标。
FactoryMapWindow.xaml.cs 不再订阅 FactoryMapWindow_StateChanged 做 WorkingArea 修正。
```

### 11.3 回归测试

必须防止以下问题回归：

```text
ResizeMode=NoResize 的弹窗出现最大化按钮。
关闭按钮绕过 CloseRequested。
地图窗口仍保留重复 ApplyMaximizedWorkingArea。
最大化按钮图标和窗口状态不一致。
```

## 12. 人工验收

实现完成后人工检查：

```text
1. 打开主窗口。
2. 点击最大化，窗口占满当前显示器工作区，但不覆盖任务栏。
3. 点击窗口化，恢复到最大化前尺寸。
4. 调整主窗口大小，再最大化 / 窗口化，恢复尺寸正确。
5. 打开工厂地图窗口，重复最大化 / 窗口化检查。
6. 地图窗口最大化后左下角状态信息仍显示。
7. 地图窗口 Alt+X 快捷键仍能最小化 / 激活地图。
8. 设置窗口、批量导入窗口、快捷项编辑窗口标题栏图标一致。
9. 消息弹窗等 ResizeMode=NoResize 窗口不显示最大化按钮。
10. 关闭按钮 hover 时图标变红，背景浅红。
11. 最小化、最大化、窗口化、关闭四个图标大小一致，不模糊。
```

多显示器人工检查：

```text
1. 将窗口拖到副屏。
2. 点击最大化。
3. 窗口应最大化到副屏工作区。
4. 点击窗口化。
5. 窗口应恢复到副屏最大化前位置。
```

## 13. 构建验证

实现完成后执行：

```powershell
dotnet build .\VSLoader.sln -c Debug --no-restore
```

执行目录：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader
```

如当前 Debug 版 VSLoader 正在运行，需要先关闭程序，否则输出目录可能被占用。

## 14. 完成标准

满足以下条件才算完成：

```text
1. 标题栏四个按钮已使用用户提供 SVG 对应的图形语义。
2. 图标跟随按钮 Foreground 变色。
3. 关闭按钮 hover / pressed 仍然是红色危险语义。
4. 最大化为当前屏幕工作区最大化，不覆盖任务栏。
5. 窗口化恢复最大化前真实窗口尺寸和位置。
6. 工厂地图窗口不再保留重复最大化工作区修正逻辑。
7. ResizeMode=NoResize 的窗口仍隐藏最大化按钮。
8. 主窗口快捷键和地图快捷键不受影响。
9. 相关测试通过。
10. Debug 构建通过。
```

本阶段核心原则：

```text
图标要是真的资源语义，最大化和窗口化也要是真的窗口行为。
```
