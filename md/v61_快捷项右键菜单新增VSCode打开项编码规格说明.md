# v61 快捷项右键菜单新增VSCode打开项编码规格说明

## 1. 需求背景

当前 VSLoader 主界面快捷项列表已经支持：

```text
1. 双击快捷项，用 VSCode 打开该快捷项对应的目标路径。
2. 右键快捷项，弹出快捷菜单。
3. 右键菜单中已有 AdminUI、WebUI、编辑、删除等操作。
```

现在需要在右键菜单中新增一项：

```text
VSCode
```

该菜单项放在右键菜单首项。

点击后效果：

```text
与双击快捷项完全一致，使用已配置的 VSCode.exe 打开当前快捷项的目标文件夹或文件。
```

## 2. 需求目标

本次目标：

```text
把已有的 VSCode 打开能力暴露到快捷项右键菜单中。
```

完成后：

```text
1. 用户右键某个快捷项。
2. 右键菜单第一项显示 “VSCode”。
3. 点击 “VSCode” 后，用 VSCode 打开该快捷项 TargetPath。
4. 行为与双击快捷项一致。
5. 不重复实现 VSCode 启动逻辑。
```

## 3. 非目标范围

本次不实现：

```text
1. 修改 VSCode 启动服务。
2. 修改 VSCode 路径配置逻辑。
3. 修改双击打开逻辑。
4. 修改快捷项数据结构。
5. 修改右键菜单整体视觉风格。
6. 修改 AdminUI/WebUI/编辑/删除行为。
7. 新增快捷键或工具栏按钮。
```

本次只做：

```text
在快捷项右键菜单首项新增 “VSCode”，并复用现有 OpenShortcutCommand。
```

## 4. 当前代码分析

### 4.1 主列表 DataGrid

位置：

```text
VSLoader/MainWindow.xaml
```

主列表控件：

```xml
<DataGrid x:Name="ShortcutsGrid" ...>
```

当前相关能力：

```text
1. MouseDoubleClick 绑定双击打开逻辑。
2. PreviewMouseRightButtonDown 负责右键时选中当前行。
3. ContextMenu 定义快捷项右键菜单。
```

### 4.2 双击打开逻辑

位置：

```text
VSLoader/MainWindow.xaml.cs
```

当前相关方法：

```csharp
ShortcutsGrid_MouseDoubleClick(...)
```

该方法最终应调用或触发：

```text
MainViewModel.OpenShortcutCommand
```

### 4.3 打开 VSCode 命令

位置：

```text
VSLoader/ViewModels/MainViewModel.cs
```

当前相关命令：

```csharp
[RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
private void OpenShortcut()
```

该命令内部使用：

```text
VSCodeLauncherService
```

说明：

```text
本次必须复用 OpenShortcutCommand，不重新写 Process.Start 或 VSCodeLauncherService 调用。
```

## 5. 设计方案

### 5.1 最小改动位置

优先修改：

```text
VSLoader/MainWindow.xaml
```

如果当前右键菜单是 XAML 定义：

```text
直接在 ContextMenu 的首项新增 MenuItem。
```

如果当前右键菜单是代码动态创建：

```text
在创建菜单项的代码中，把 VSCode 插入到 Items 第 0 个位置。
```

预计不需要修改：

```text
VSLoader/ViewModels/MainViewModel.cs
VSLoader/Services/VSCodeLauncherService.cs
```

### 5.2 菜单顺序

右键菜单顺序调整为：

```text
1. VSCode
2. AdminUI
3. WebUI
4. 获取AdminUI连接
5. 编辑
6. 删除
```

如果当前实际菜单项顺序略有差异，以“VSCode 位于最上方”为硬性要求。

### 5.3 命令绑定

新增菜单项应绑定：

```xml
Command="{Binding DataContext.OpenShortcutCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
```

如果当前 ContextMenu 无法直接通过视觉树找到 DataGrid，则应沿用项目中现有右键菜单命令绑定方式。

常见可选写法：

```xml
<MenuItem Header="VSCode"
          Command="{Binding PlacementTarget.DataContext.OpenShortcutCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}" />
```

最终以当前项目已有 ContextMenu 命令绑定方式为准。

### 5.4 选中项保障

右键点击某一行时，当前项目已有逻辑：

```text
ShortcutsGrid_PreviewMouseRightButtonDown
```

应该已经负责：

```text
1. 找到被右键点击的 DataGridRow。
2. 设置 ShortcutsGrid.SelectedItem。
3. 同步 MainViewModel.SelectedShortcut。
```

本次需确认：

```text
点击右键菜单 VSCode 时，OpenShortcutCommand 操作的是右键点击的那一行。
```

如果当前右键选择逻辑已经正常，则不做额外修改。

## 6. 行为细节

### 6.1 有选中快捷项

当用户右键某条快捷项并点击：

```text
VSCode
```

期望：

```text
打开该快捷项 TargetPath。
```

### 6.2 未选中快捷项

如果没有选中项：

```text
OpenShortcutCommand CanExecute 应返回 false。
```

菜单项应自动禁用。

### 6.3 VSCode 路径未配置

保持现有行为。

即：

```text
OpenShortcutCommand 内部已有错误提示逻辑。
```

本次不新增额外判断。

### 6.4 目标路径不存在

保持现有行为。

即：

```text
VSCodeLauncherService 按已有规则提示错误。
```

本次不新增额外判断。

## 7. 风险与注意事项

### 7.1 ContextMenu 数据上下文问题

WPF 中 `ContextMenu` 不在主视觉树内。

如果直接写：

```xml
Command="{Binding OpenShortcutCommand}"
```

可能绑定不到 `MainViewModel`。

应参考当前已有 AdminUI、WebUI、编辑、删除菜单项的绑定方式。

### 7.2 不要重新实现打开逻辑

禁止在菜单点击事件中新增：

```csharp
Process.Start(...)
```

或直接调用：

```csharp
_launcherService.Launch(...)
```

原因：

```text
双击和菜单 VSCode 应保持同一套错误处理、配置读取、命令可用状态。
```

### 7.3 不要破坏右键菜单样式

当前项目右键菜单已经做过样式调整。

本次只新增菜单项，不重写：

```text
ContextMenu Style
MenuItem Style
右键菜单背景
选中态
```

## 8. 手工验收

### 8.1 菜单项位置

步骤：

```text
1. 启动 VSLoader。
2. 右键任意快捷项。
```

期望：

```text
1. 菜单第一项为 VSCode。
2. 其它菜单项仍正常显示。
```

### 8.2 点击 VSCode 打开路径

步骤：

```text
1. 右键某个快捷项。
2. 点击 VSCode。
```

期望：

```text
1. 行为与双击该快捷项一致。
2. VSCode 打开该快捷项对应 TargetPath。
```

### 8.3 右键未预先选中的行

步骤：

```text
1. 当前选中 A 快捷项。
2. 右键 B 快捷项。
3. 点击 VSCode。
```

期望：

```text
打开 B 快捷项，而不是 A 快捷项。
```

### 8.4 VSCode 路径异常

步骤：

```text
1. 配置一个不存在的 VSCode.exe 路径。
2. 右键快捷项。
3. 点击 VSCode。
```

期望：

```text
复用现有错误提示。
```

## 9. 自动化验证

执行：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader
dotnet test .\VSLoader.sln -p:UseSharedCompilation=false
dotnet build .\VSLoader.sln -p:UseSharedCompilation=false
```

如果 VSLoader.exe 正在运行导致文件占用：

```powershell
Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force
```

## 10. 验收标准

最终必须满足：

```text
1. 快捷项右键菜单第一项显示 VSCode。
2. 点击 VSCode 与双击快捷项行为一致。
3. 右键 B 行后点击 VSCode 打开 B 行。
4. 原有 AdminUI、WebUI、编辑、删除不退化。
5. 不新增重复 VSCode 启动逻辑。
6. dotnet test 通过。
7. dotnet build 0 错误。
```
