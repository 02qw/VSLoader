# v20 统一弹窗 Owner 与居中显示编码规格说明

## 1. 需求背景

当前 VSLoader 中部分提示弹窗没有相对主窗口居中显示。

用户截图中可以看到：

- 提示框显示在主窗口偏右位置。
- 没有以 VSLoader 主窗口中心作为弹出位置。
- 视觉体验不稳定。

经源码检查，问题主要集中在：

```text
VSLoader\Services\DialogService.cs
```

其中 `MessageBox.Show(...)` 和若干文件/文件夹选择框没有明确指定 Owner。

没有 Owner 时，Windows 会按系统默认规则决定弹窗位置和焦点关系，因此可能不居中。

## 2. 需求目标

本次开发目标：

1. 所有信息提示框相对 VSLoader 主窗口居中。
2. 所有错误提示框相对 VSLoader 主窗口居中。
3. 所有确认提示框相对 VSLoader 主窗口居中。
4. 文件选择框尽量以主窗口作为 Owner。
5. 文件夹选择框尽量以主窗口作为父窗口。
6. 已经正常居中的自定义窗口不改动。
7. 不改变任何业务逻辑和提示文案。

## 3. 修改范围

主要修改文件：

- `VSLoader\Services\DialogService.cs`

原则上不需要修改：

- `VSLoader\ViewModels\MainViewModel.cs`
- `VSLoader\Views\ShortcutEditWindow.xaml.cs`
- `VSLoader\Views\BatchImportWindow.xaml.cs`
- `VSLoader\Views\SettingsWindow.xaml.cs`
- 其他业务服务

## 4. 当前问题来源

当前 `DialogService` 中类似：

```csharp
MessageBox.Show(message, "VSLoader", MessageBoxButton.OK, MessageBoxImage.Information);
```

没有传入 Owner。

应改为：

```csharp
MessageBox.Show(GetOwnerWindow(), message, "VSLoader", MessageBoxButton.OK, MessageBoxImage.Information);
```

类似问题存在于：

- `ShowInfo`
- `ShowError`
- `Confirm`
- `SelectExeFile`
- `SelectFile`
- `SelectCsvFile`
- `SelectFolder`

## 5. 已经正常的窗口

以下自定义窗口已经设置：

```csharp
Owner = System.Windows.Application.Current.MainWindow;
```

并且 XAML 中有：

```xml
WindowStartupLocation="CenterOwner"
```

包括：

- `ShortcutEditWindow`
- `BatchImportWindow`
- `SettingsWindow`

本次不需要修改这些窗口。

## 6. MessageBox 修改要求

### 6.1 新增 Owner 获取方法

建议在 `DialogService` 中新增：

```csharp
private static Window? GetOwnerWindow()
{
    return Application.Current?.Windows
        .OfType<Window>()
        .FirstOrDefault(window => window.IsActive)
        ?? Application.Current?.MainWindow;
}
```

理由：

- 如果当前有子窗口处于激活状态，弹窗应优先挂在当前激活窗口上。
- 如果没有激活窗口，再回退到主窗口。

### 6.2 ShowInfo

从：

```csharp
MessageBox.Show(message, "VSLoader", MessageBoxButton.OK, MessageBoxImage.Information);
```

改为：

```csharp
MessageBox.Show(GetOwnerWindow(), message, "VSLoader", MessageBoxButton.OK, MessageBoxImage.Information);
```

### 6.3 ShowError

从：

```csharp
MessageBox.Show(message, "VSLoader", MessageBoxButton.OK, MessageBoxImage.Error);
```

改为：

```csharp
MessageBox.Show(GetOwnerWindow(), message, "VSLoader", MessageBoxButton.OK, MessageBoxImage.Error);
```

### 6.4 Confirm

从：

```csharp
MessageBox.Show(message, "VSLoader", MessageBoxButton.YesNo, MessageBoxImage.Question)
```

改为：

```csharp
MessageBox.Show(GetOwnerWindow(), message, "VSLoader", MessageBoxButton.YesNo, MessageBoxImage.Question)
```

## 7. OpenFileDialog 修改要求

当前文件选择框类似：

```csharp
return dialog.ShowDialog() == true ? dialog.FileName : null;
```

建议改为：

```csharp
return dialog.ShowDialog(GetOwnerWindow()) == true ? dialog.FileName : null;
```

适用方法：

- `SelectExeFile`
- `SelectFile`
- `SelectCsvFile`

注意：

- `Microsoft.Win32.OpenFileDialog.ShowDialog(Window? owner)` 支持传入 WPF Window。
- 若 `GetOwnerWindow()` 返回 `null`，仍可传入 null 或回退到无参 `ShowDialog()`。

## 8. FolderBrowserDialog 修改要求

当前 WinForms 文件夹选择框类似：

```csharp
return dialog.ShowDialog() == WinForms.DialogResult.OK ? dialog.SelectedPath : null;
```

WinForms `FolderBrowserDialog` 需要 `IWin32Window` 类型 owner。

推荐实现一个简单适配器：

```csharp
private sealed class WindowHandleWrapper : WinForms.IWin32Window
{
    public WindowHandleWrapper(IntPtr handle)
    {
        Handle = handle;
    }

    public IntPtr Handle { get; }
}
```

然后通过：

```csharp
var owner = GetOwnerWindow();
var handle = owner is null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;

var result = handle == IntPtr.Zero
    ? dialog.ShowDialog()
    : dialog.ShowDialog(new WindowHandleWrapper(handle));
```

需要引入：

```csharp
using System.Windows.Interop;
```

## 9. 弹窗 Owner 策略

优先级：

1. 当前激活窗口。
2. 应用主窗口。
3. 无 owner 回退。

这样可以避免以下问题：

- 在设置窗口中触发错误提示时，提示框跑到主窗口中心。
- 在批量导入窗口中选择文件时，文件选择框不跟随当前窗口。
- 主窗口失焦时弹窗焦点关系混乱。

## 10. 不允许改变的内容

本次不允许改变：

- 弹窗文案。
- 弹窗按钮类型。
- 弹窗图标类型。
- 文件选择过滤规则。
- 文件夹选择逻辑。
- 业务判断逻辑。
- 自定义窗口布局。
- ViewModel 调用方式。

## 11. 验收标准

### 11.1 MessageBox 居中

触发以下提示时：

- 信息提示
- 错误提示
- 确认提示

期望：

- 弹窗相对当前激活 VSLoader 窗口居中。
- 不再跑到主窗口偏右或屏幕随机位置。

### 11.2 子窗口内提示

在以下窗口中触发错误提示：

- 新增/编辑窗口
- 批量新增识别窗口
- 设置窗口

期望：

- 提示框相对当前子窗口居中或至少保持正确 owner/focus 关系。

### 11.3 文件选择框

触发以下选择框：

- 选择 VSCode exe
- 选择 CSV 文件
- 选择普通文件

期望：

- 文件选择框以当前激活窗口为 owner。
- 不被主窗口遮挡。
- 关闭后焦点返回 VSLoader。

### 11.4 文件夹选择框

触发文件夹选择时：

- 文件夹选择框以当前激活窗口为父窗口。
- 关闭后焦点返回 VSLoader。

### 11.5 编译验收

执行：

```powershell
dotnet build .\VSLoader.sln
```

期望：

```text
0 warnings
0 errors
```

如果提示 `VSLoader.exe` 被占用，说明程序正在运行，应先关闭 VSLoader 后重新构建。

## 12. 建议实施步骤

1. 打开 `VSLoader\Services\DialogService.cs`。
2. 新增 `GetOwnerWindow()` 方法。
3. 修改 `ShowInfo`，传入 owner。
4. 修改 `ShowError`，传入 owner。
5. 修改 `Confirm`，传入 owner。
6. 修改 `SelectExeFile`、`SelectFile`、`SelectCsvFile`，传入 owner。
7. 为 `FolderBrowserDialog` 增加 WinForms owner wrapper。
8. 编译项目。
9. 人工触发几类弹窗验证居中效果。

## 13. 明确不做的事情

本次不做：

- 自定义 MessageBox 样式。
- 替换系统 MessageBox。
- 重写所有弹窗为自定义窗口。
- 修改提示内容。
- 修改业务逻辑。
