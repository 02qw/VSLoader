# v36 快捷项右键单独获取 AdminUI 连接编码规格说明

## 1. 需求背景

当前 VSLoader 已支持顶部按钮：

```text
自动获取连接
```

该功能会对当前列表中的所有快捷项执行 AdminUI `.jnlp` 下载。

问题：

```text
用户刚批量新增 1-2 个快捷项后，只想马上打开这些新增项的 AdminUI。
但此时还没有对应的 .jnlp 文件。
如果点击顶部“自动获取连接”，程序会重新处理全部快捷项，等待时间可能十几秒，体验较慢。
```

用户希望：

```text
右键某个快捷项 -> 点击“获取AdminUI连接” -> 只下载当前快捷项对应的 .jnlp 文件
```

这样可以快速为新快捷项补齐 AdminUI 连接文件，然后立即使用 `AdminUI` 打开。

## 2. 需求目标

本次开发目标：

1. 在快捷项右键菜单中新增 `获取AdminUI连接` 菜单项。
2. 点击后只处理当前选中的快捷项。
3. 下载逻辑与现有顶部 `自动获取连接` 保持一致。
4. `.jnlp` 保存目录与现有逻辑一致：

```text
%AppData%\VSLoader\UIdownload
```

5. 如果同名 `.jnlp` 已存在，继续覆盖。
6. 不自动打开 AdminUI。
7. 下载成功后给出成功提示。
8. 下载失败时给出错误提示。
9. 不改变顶部 `自动获取连接` 功能。
10. 不改变现有 `AdminUI` 打开逻辑。

## 3. 当前相关代码

AdminUI 批量下载服务：

```text
VSLoader\Models\Services\AdminUiService.cs
```

当前已有：

```csharp
public async Task<AdminUiDownloadResult> DownloadAllAsync(...)
```

该方法会：

1. 遍历所有快捷项。
2. 调用 `ResolveShortcutInfo(...)`。
3. 从快捷项目标文件夹读取 `INSTANCE.properties`。
4. 拼接 `.jnlp` 下载 URL。
5. 下载 `.jnlp` 到 `%AppData%\VSLoader\UIdownload`。
6. 已存在时覆盖。

主窗口 ViewModel：

```text
VSLoader\ViewModels\MainViewModel.cs
```

当前已有：

```csharp
DownloadAdminUiLinksAsync()
OpenAdminUiAsync()
```

快捷项右键菜单：

```text
VSLoader\MainWindow.xaml
```

当前菜单项顺序：

```text
AdminUI
WebUI
编辑
删除
```

## 4. 推荐菜单顺序

新增后推荐右键菜单顺序：

```text
AdminUI
获取AdminUI连接
WebUI
编辑
删除
```

原因：

- `AdminUI` 是打开。
- `获取AdminUI连接` 是为 AdminUI 补齐连接文件。
- 两者语义相邻。
- `WebUI` 是另一个打开入口。
- `编辑`、`删除` 保持在后面。

## 5. AdminUiService 单项下载方法

在：

```text
VSLoader\Models\Services\AdminUiService.cs
```

新增公共方法：

```csharp
public async Task<LaunchResult> DownloadOneAsync(
    ShortcutItem shortcut,
    AdminUiConfig config,
    CancellationToken cancellationToken = default)
```

职责：

- 只下载单个快捷项对应的 `.jnlp`。
- 使用和批量下载完全相同的路径解析、URL 拼接和文件覆盖规则。
- 成功返回 `LaunchResult.Ok()`。
- 失败返回 `LaunchResult.Fail(...)`。

## 6. 抽取核心下载逻辑

当前 `DownloadAllAsync()` 内部每个快捷项包含下载逻辑：

```csharp
var info = ResolveShortcutInfo(shortcut, config);
var tempPath = info.LocalJnlpPath + ".tmp";

await using (var responseStream = await httpClient.GetStreamAsync(info.Url, cancellationToken))
await using (var fileStream = File.Create(tempPath))
{
    await responseStream.CopyToAsync(fileStream, cancellationToken);
}

File.Move(tempPath, info.LocalJnlpPath, true);
```

本次建议抽取成私有方法：

```csharp
private async Task<AdminUiShortcutInfo> DownloadOneCoreAsync(
    ShortcutItem shortcut,
    AdminUiConfig config,
    HttpClient httpClient,
    CancellationToken cancellationToken)
```

推荐行为：

1. 调用 `ResolveShortcutInfo(shortcut, config)`。
2. 生成临时文件路径：

```csharp
var tempPath = info.LocalJnlpPath + ".tmp";
```

3. 下载远端 `.jnlp` 到临时文件。
4. 下载成功后：

```csharp
File.Move(tempPath, info.LocalJnlpPath, true);
```

5. 下载失败时，如果临时文件存在则删除。
6. 返回 `AdminUiShortcutInfo`，供调用方检查 `ServiceName` 等信息。

## 7. DownloadOneAsync 推荐实现

```csharp
public async Task<LaunchResult> DownloadOneAsync(
    ShortcutItem shortcut,
    AdminUiConfig config,
    CancellationToken cancellationToken = default)
{
    try
    {
        Directory.CreateDirectory(DownloadDirectory);
        using var httpClient = CreateHttpClient(config.IgnoreCertificateErrors);
        _ = await DownloadOneCoreAsync(shortcut, config, httpClient, cancellationToken);
        return LaunchResult.Ok();
    }
    catch (Exception ex)
    {
        return LaunchResult.Fail(ex.Message);
    }
}
```

说明：

- 单项下载不需要返回批量统计。
- 单项下载不自动打开 `.jnlp`。
- 单项下载成功后只说明文件已获取。

## 8. DownloadAllAsync 改造要求

`DownloadAllAsync()` 应复用 `DownloadOneCoreAsync(...)`，避免重复下载代码。

原循环中的下载块改为：

```csharp
var info = await DownloadOneCoreAsync(shortcut, config, httpClient, cancellationToken);
successCount++;
```

然后继续保留当前 PacService 检查逻辑：

```csharp
if (!string.IsNullOrWhiteSpace(info.ServiceName)
    && !string.Equals(info.ServiceName, info.InstanceName, StringComparison.OrdinalIgnoreCase))
{
    messages.Add(...);
}
```

说明：

- 顶部批量获取行为保持不变。
- 成功、失败统计保持不变。
- 进度上报保持不变。
- 覆盖逻辑保持不变。

## 9. MainViewModel 新增服务命令

修改：

```text
VSLoader\ViewModels\MainViewModel.cs
```

新增命令：

```csharp
[RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
private async Task DownloadSelectedAdminUiLinkAsync()
```

推荐流程：

```csharp
if (SelectedShortcut is null)
{
    return;
}

try
{
    IsBusy = true;
    BusyMessage = $"正在获取 {SelectedShortcut.Name} 的 AdminUI 连接，请稍候...";
    BusyProgressValue = 0;
    BusyProgressMaximum = 1;
    BusyProgressText = "正在测试 AdminUI 网络连接...";
    BusyCurrentItemText = string.Empty;

    var shortcut = SelectedShortcut;
    var adminUiConfig = _config.AdminUi.Clone();

    var testResult = await _adminUiService.TestConnectionAsync(adminUiConfig);
    if (!testResult.Success)
    {
        BusyProgressText = "网络连接失败。";
        _dialogService.ShowError(testResult.ErrorMessage ?? "网络连接失败，请检查 AdminUI BaseUrl、网络环境或 VPN。");
        return;
    }

    BusyProgressText = "正在下载 AdminUI 连接...";
    BusyCurrentItemText = $"正在处理：{shortcut.Name}";

    var result = await Task.Run(async () =>
    {
        return await _adminUiService.DownloadOneAsync(shortcut, adminUiConfig);
    });

    if (!result.Success)
    {
        _dialogService.ShowError(result.ErrorMessage ?? "获取 AdminUI 连接失败。");
        return;
    }

    BusyProgressValue = 1;
    BusyProgressText = "获取完成。";
    _dialogService.ShowInfo($"已获取 AdminUI 连接：{shortcut.Name}");
}
catch (Exception ex)
{
    _dialogService.ShowError($"获取 AdminUI 连接失败：{ex.Message}");
}
finally
{
    IsBusy = false;
    BusyMessage = string.Empty;
    BusyProgressValue = 0;
    BusyProgressMaximum = 0;
    BusyProgressText = string.Empty;
    BusyCurrentItemText = string.Empty;
}
```

## 10. 后台执行要求

单项下载也需要后台执行：

```csharp
await Task.Run(...)
```

原因：

- 单项下载同样会访问目标文件夹。
- `ResolveShortcutInfo` 内部包含：

```csharp
Directory.Exists
File.Exists
File.ReadLines
```

- 如果目标路径是网络路径，也可能卡 UI。

## 11. 命令 CanExecute 刷新

`SelectedShortcut` 属性需要新增：

```csharp
[NotifyCanExecuteChangedFor(nameof(DownloadSelectedAdminUiLinkCommand))]
```

`IsBusy` 属性需要新增：

```csharp
[NotifyCanExecuteChangedFor(nameof(DownloadSelectedAdminUiLinkCommand))]
```

命令可执行条件复用：

```csharp
HasSelectedShortcut
```

也就是：

```text
必须选中快捷项
当前不处于 IsBusy
```

## 12. 右键菜单修改

修改：

```text
VSLoader\MainWindow.xaml
```

在 `AdminUI` 菜单项后新增：

```xml
<MenuItem Header="获取AdminUI连接"
          Style="{StaticResource CompactMenuItemStyle}"
          Command="{Binding PlacementTarget.DataContext.DownloadSelectedAdminUiLinkCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}" />
```

最终顺序：

```xml
<MenuItem Header="AdminUI" ... />
<MenuItem Header="获取AdminUI连接" ... />
<MenuItem Header="WebUI" ... />
<MenuItem Header="编辑" ... />
<MenuItem Header="删除" ... />
```

## 13. 是否自动打开 AdminUI

本次不自动打开 AdminUI。

用户流程：

```text
右键快捷项 -> 获取AdminUI连接 -> 成功提示
右键快捷项 -> AdminUI
```

原因：

- 本次需求是获取连接文件。
- 避免下载成功后自动弹 Java/Swing 客户端，打断用户。
- 保持功能职责单一。

## 14. 是否修改顶部自动获取连接

本次不改变顶部按钮：

```text
自动获取连接
```

顶部按钮仍然处理当前列表中所有快捷项。

右键菜单 `获取AdminUI连接` 只处理当前选中的快捷项。

## 15. 错误处理

| 场景 | 处理 |
| --- | --- |
| 未选中快捷项 | 不执行 |
| 网络连接预检查失败 | 弹出网络连接失败提示 |
| 目标路径不存在 | 弹出目标路径不存在或不可访问 |
| 缺少 `INSTANCE.properties` | 弹出未找到配置文件 |
| 缺少端口或实例名 | 弹出对应错误 |
| 下载失败 | 弹出下载失败原因 |
| 临时文件残留 | 下载失败时删除 `.tmp` |
| 已存在同名 `.jnlp` | 覆盖 |

## 16. 不允许改变的功能

本次不允许改变：

- 顶部 `自动获取连接` 批量下载功能。
- AdminUI URL 拼接规则。
- `.jnlp` 保存目录。
- `.jnlp` 覆盖逻辑。
- `AdminUI` 打开逻辑。
- AdminUI 密码剪贴板逻辑。
- WebUI 打开逻辑。
- VSCode 打开逻辑。
- 批量新增识别逻辑。

## 17. 验收标准

### 17.1 右键菜单显示

操作：

1. 右键某个快捷项。

期望右键菜单包含：

```text
AdminUI
获取AdminUI连接
WebUI
编辑
删除
```

### 17.2 单项获取成功

准备：

1. 选中一个尚未下载 `.jnlp` 的快捷项。
2. 该快捷项目标文件夹包含有效 `INSTANCE.properties`。

操作：

1. 右键该快捷项。
2. 点击 `获取AdminUI连接`。

期望：

- 程序只下载该快捷项对应 `.jnlp`。
- 文件保存到 `%AppData%\VSLoader\UIdownload`。
- 成功后提示：

```text
已获取 AdminUI 连接：{快捷项名称}
```

### 17.3 获取后可打开 AdminUI

操作：

1. 单项获取成功后。
2. 右键该快捷项。
3. 点击 `AdminUI`。

期望：

- 能打开对应 `.jnlp`。
- 如果配置了 AdminUI 密码，仍按现有逻辑复制到剪贴板。

### 17.4 不影响批量获取

操作：

1. 点击顶部 `自动获取连接`。

期望：

- 批量获取仍按原逻辑处理全部快捷项。
- 进度条和完成弹窗仍正常。

### 17.5 同名文件覆盖

准备：

目标 `.jnlp` 已存在。

操作：

1. 点击 `获取AdminUI连接`。

期望：

- 新下载文件覆盖旧文件。
- 不生成重复文件。

### 17.6 网络失败

准备：

AdminUI BaseUrl 网络不可达。

期望：

- 弹出网络连接失败提示。
- 不写入无效 `.jnlp`。

### 17.7 编译验收

执行：

```powershell
dotnet build .\VSLoader.sln
```

期望：

```text
0 warnings
0 errors
```

如果提示 `VSLoader.exe` 被占用，说明程序正在运行，应先通过托盘菜单 `退出` 或结束进程后重新构建。

## 18. 建议实施步骤

1. 打开 `AdminUiService.cs`。
2. 抽取 `DownloadOneCoreAsync(...)` 私有方法。
3. 新增 `DownloadOneAsync(...)` 公共方法。
4. 修改 `DownloadAllAsync(...)` 复用 `DownloadOneCoreAsync(...)`。
5. 打开 `MainViewModel.cs`。
6. 新增 `DownloadSelectedAdminUiLinkAsync()` 命令。
7. 给 `SelectedShortcut` 和 `IsBusy` 增加命令刷新通知。
8. 打开 `MainWindow.xaml`。
9. 在右键菜单 `AdminUI` 后增加 `获取AdminUI连接`。
10. 编译项目。
11. 手动验证单项获取、获取后打开、批量获取不回归。

## 19. 明确不做的事情

本次不做：

- 获取后自动打开 AdminUI。
- 为多个选中项批量获取。
- 新增顶部按钮。
- 新增下载取消按钮。
- 新增下载历史。
- 新增 `.jnlp` 文件管理页面。
- 修改 AdminUI 配置项。
