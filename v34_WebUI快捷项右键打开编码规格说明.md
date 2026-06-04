# v34 WebUI 快捷项右键打开编码规格说明

## 1. 需求背景

当前 VSLoader 的快捷项支持：

```text
双击快捷项 -> 用 VSCode 打开目标文件夹
右键快捷项 -> AdminUI / 编辑 / 删除
```

用户现在希望在快捷项右键菜单中新增：

```text
WebUI
```

当用户右键某个快捷项并点击 `WebUI` 时，程序需要根据该快捷项对应目标文件夹中的 `INSTANCE.properties` 文件拼接 WebUI 地址，并使用系统默认浏览器打开。

## 2. 需求目标

本次开发目标：

1. 在快捷项右键菜单中新增 `WebUI` 菜单项。
2. 点击 `WebUI` 后，根据当前选中快捷项打开对应 WebUI 地址。
3. WebUI 地址实时从快捷项目标文件夹读取 `INSTANCE.properties` 拼接。
4. 不提前批量生成或缓存所有 WebUI URL。
5. 使用系统默认浏览器打开 URL。
6. 不影响现有 `AdminUI`、`编辑`、`删除` 功能。
7. 不改变主界面顶部按钮布局。
8. 不改变现有 AdminUI `.jnlp` 下载逻辑。

## 3. WebUI URL 拼接算法

最终 URL 格式：

```text
{BaseUrl}:{SslPort}/{InstanceName}/ui
```

示例：

```text
BaseUrl: https://192.168.15.69
SslPort: 10024
InstanceName: TATP012
```

最终结果：

```text
https://192.168.15.69:10024/TATP012/ui
```

## 4. 示例场景

快捷项对应目标文件夹：

```text
5534_TATP012
```

该文件夹下存在：

```text
INSTANCE.properties
```

其中包含：

```text
GUI.WebServer.SSLPort=10024
```

并且包含实例名配置，例如：

```text
zam.instance.name=TATP012
```

则拼接：

```text
https://192.168.15.69:10024/TATP012/ui
```

## 5. 配置模型设计

新增配置模型：

```text
VSLoader\Models\WebUiConfig.cs
```

推荐内容：

```csharp
namespace VSLoader.Models;

public sealed class WebUiConfig
{
    public string BaseUrl { get; set; } = "https://192.168.15.69";

    public string InstancePropertiesName { get; set; } = "INSTANCE.properties";

    public string InstanceNameKey { get; set; } = "zam.instance.name";

    public string SslPortKey { get; set; } = "GUI.WebServer.SSLPort";

    public WebUiConfig Clone()
    {
        return new WebUiConfig
        {
            BaseUrl = BaseUrl,
            InstancePropertiesName = InstancePropertiesName,
            InstanceNameKey = InstanceNameKey,
            SslPortKey = SslPortKey
        };
    }
}
```

说明：

- `BaseUrl` 默认为 `https://192.168.15.69`。
- `InstancePropertiesName` 默认为 `INSTANCE.properties`。
- `InstanceNameKey` 默认为 `zam.instance.name`。
- `SslPortKey` 默认为 `GUI.WebServer.SSLPort`。
- 这些字段进入配置文件，方便未来变更。

## 6. AppConfig 修改

修改：

```text
VSLoader\Models\AppConfig.cs
```

新增字段：

```csharp
public WebUiConfig WebUi { get; set; } = new();
```

最终类似：

```csharp
public sealed class AppConfig
{
    public string VSCodePath { get; set; } = string.Empty;
    public List<ShortcutItem> Shortcuts { get; set; } = new();
    public AdminUiConfig AdminUi { get; set; } = new();
    public HotkeyConfig Hotkey { get; set; } = new();
    public BatchImportConfig BatchImport { get; set; } = new();
    public WebUiConfig WebUi { get; set; } = new();
}
```

## 7. ConfigService 兼容旧配置

修改：

```text
VSLoader\Models\Services\ConfigService.cs
```

在反序列化后补默认值：

```csharp
config.WebUi ??= new WebUiConfig();
```

说明：

- 兼容旧版 `config.json`。
- 旧配置没有 `WebUi` 字段时，程序正常启动。

## 8. WebUiService 设计

新增服务：

```text
VSLoader\Models\Services\WebUiService.cs
```

命名空间：

```csharp
namespace VSLoader.Services;
```

职责：

1. 校验快捷项目标路径是否存在。
2. 查找 `INSTANCE.properties`。
3. 读取 properties 配置。
4. 获取实例名。
5. 获取 `GUI.WebServer.SSLPort`。
6. 拼接 WebUI URL。
7. 使用默认浏览器打开。

推荐公开方法：

```csharp
public LaunchResult OpenWebUi(ShortcutItem shortcut, WebUiConfig config)
```

## 9. WebUiService 推荐流程

```csharp
public LaunchResult OpenWebUi(ShortcutItem shortcut, WebUiConfig config)
{
    try
    {
        var url = BuildWebUiUrl(shortcut, config);
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        return LaunchResult.Ok();
    }
    catch (Exception ex)
    {
        return LaunchResult.Fail(ex.Message);
    }
}
```

`BuildWebUiUrl` 推荐流程：

```text
1. 校验 shortcut.TargetPath 存在。
2. 拼接 propertiesPath = TargetPath + InstancePropertiesName。
3. 校验 INSTANCE.properties 存在。
4. 读取 properties 文件为 Dictionary。
5. 读取 InstanceNameKey。
6. 读取 SslPortKey。
7. 校验端口为纯数字。
8. 拼接 URL。
```

## 10. 实例名获取规则

推荐优先级：

```text
第一优先级：从 INSTANCE.properties 中读取 InstanceNameKey，例如 zam.instance.name
第二优先级：如果文件中没有实例名，则从文件夹名中兜底解析
```

兜底解析规则：

```text
如果目标文件夹名包含 "_"，取最后一个 "_" 后面的内容。
例如：5534_TATP012 -> TATP012
```

如果仍无法获得实例名，报错：

```text
缺少实例名配置：zam.instance.name
```

说明：

- 实时读取配置最准确。
- 兜底解析可以增强兼容性。

## 11. properties 文件读取规则

可复用 AdminUI 中类似逻辑。

读取规则：

- 忽略空行。
- 忽略以 `#` 开头的注释行。
- 每行按第一个 `=` 分割。
- key/value 都执行 `Trim()`。
- 使用大小写不敏感字典。

示例：

```csharp
private static Dictionary<string, string> ReadPropertiesFile(string path)
{
    var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    ...
}
```

## 12. URL 拼接规则

推荐实现：

```csharp
private static string BuildUrl(WebUiConfig config, string instanceName, string port)
{
    var baseUrl = config.BaseUrl.TrimEnd('/');
    return $"{baseUrl}:{Uri.EscapeDataString(port)}/{Uri.EscapeDataString(instanceName)}/ui";
}
```

注意：

- `BaseUrl` 末尾如果有 `/`，需要去掉。
- `port` 必须是数字。
- `instanceName` 需要 URL 转义。
- `port` 虽然是数字，也可以保持原样或转义。

## 13. MainViewModel 修改

修改：

```text
VSLoader\ViewModels\MainViewModel.cs
```

新增服务字段：

```csharp
private readonly WebUiService _webUiService;
```

默认构造函数中新增：

```csharp
new WebUiService()
```

完整构造函数新增参数：

```csharp
WebUiService webUiService
```

赋值：

```csharp
_webUiService = webUiService;
```

## 14. 新增 OpenWebUiCommand

在 `MainViewModel` 中新增命令：

```csharp
[RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
private void OpenWebUi()
{
    if (SelectedShortcut is null)
    {
        return;
    }

    var result = _webUiService.OpenWebUi(SelectedShortcut, _config.WebUi);
    if (!result.Success)
    {
        _dialogService.ShowError(result.ErrorMessage ?? "打开 WebUI 失败。");
    }
}
```

并确保 `SelectedShortcut` 属性通知：

```csharp
[NotifyCanExecuteChangedFor(nameof(OpenWebUiCommand))]
```

`IsBusy` 属性也需要通知：

```csharp
[NotifyCanExecuteChangedFor(nameof(OpenWebUiCommand))]
```

说明：

- WebUI 与 AdminUI 一样，需要有选中快捷项。
- 忙碌状态下不可执行。

## 15. 右键菜单修改

修改：

```text
VSLoader\MainWindow.xaml
```

当前菜单项：

```text
AdminUI
编辑
删除
```

新增后：

```text
AdminUI
WebUI
编辑
删除
```

推荐位置：

```xml
<MenuItem Header="AdminUI" ... />
<MenuItem Header="WebUI"
          Style="{StaticResource CompactMenuItemStyle}"
          Command="{Binding PlacementTarget.DataContext.OpenWebUiCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}" />
<MenuItem Header="编辑" ... />
<MenuItem Header="删除" ... />
```

## 16. 是否新增主界面顶部按钮

本次不新增顶部按钮。

原因：

- 用户明确需求是右键快捷项菜单增加 `WebUI`。
- 当前顶部工具区按钮已经较多。
- 避免影响主界面布局。

## 17. 是否保存 URL 文件

本次不保存 URL 映射文件。

原因：

- URL 依赖 `INSTANCE.properties`。
- 实时读取最准确。
- 端口或实例名变化后无需重新生成缓存。
- 减少维护额外文件带来的错误。

## 18. 错误处理

| 场景 | 处理 |
| --- | --- |
| 未选中快捷项 | 不执行 |
| 目标路径不存在 | 提示目标路径不存在或不可访问 |
| 找不到 `INSTANCE.properties` | 提示未找到配置文件 |
| 找不到 `GUI.WebServer.SSLPort` | 提示缺少 WebUI 端口配置 |
| 端口不是数字 | 提示端口不是有效端口 |
| 找不到实例名且无法从文件夹名兜底 | 提示缺少实例名配置 |
| 浏览器打开失败 | 提示打开 WebUI 失败 |

## 19. 不允许改变的功能

本次不允许改变：

- VSCode 双击打开逻辑。
- AdminUI `.jnlp` 下载逻辑。
- AdminUI 打开逻辑。
- AdminUI 密码剪贴板逻辑。
- 批量新增识别逻辑。
- 主界面顶部按钮布局。
- 右键菜单现有 `AdminUI`、`编辑`、`删除` 行为。
- 配置文件路径。

## 20. 验收标准

### 20.1 右键菜单显示

操作：

1. 右键某个快捷项。

期望：

```text
菜单中出现 WebUI
```

菜单顺序：

```text
AdminUI
WebUI
编辑
删除
```

### 20.2 正常打开 WebUI

准备：

快捷项目标文件夹下有：

```text
INSTANCE.properties
```

文件包含：

```text
zam.instance.name=TATP012
GUI.WebServer.SSLPort=10024
```

操作：

1. 右键该快捷项。
2. 点击 `WebUI`。

期望：

默认浏览器打开：

```text
https://192.168.15.69:10024/TATP012/ui
```

### 20.3 从文件夹名兜底实例名

准备：

目标文件夹名：

```text
5534_TATP012
```

`INSTANCE.properties` 中没有 `zam.instance.name`，但有：

```text
GUI.WebServer.SSLPort=10024
```

期望：

使用 `TATP012` 作为实例名，打开：

```text
https://192.168.15.69:10024/TATP012/ui
```

### 20.4 缺少端口配置

准备：

`INSTANCE.properties` 中没有：

```text
GUI.WebServer.SSLPort
```

期望：

弹出错误提示：

```text
缺少 WebUI 端口配置：GUI.WebServer.SSLPort
```

### 20.5 端口非法

准备：

```text
GUI.WebServer.SSLPort=abc
```

期望：

弹出错误提示：

```text
GUI.WebServer.SSLPort 不是有效端口：abc
```

### 20.6 编译验收

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

## 21. 建议实施步骤

1. 新增 `WebUiConfig.cs`。
2. 修改 `AppConfig`，新增 `WebUi` 字段。
3. 修改 `ConfigService.Load()`，兼容旧配置。
4. 新增 `WebUiService.cs`。
5. 在 `WebUiService` 中实现 properties 读取、实例名解析、端口读取和 URL 拼接。
6. 在 `MainViewModel` 中注入 `WebUiService`。
7. 新增 `OpenWebUiCommand`。
8. 修改 `SelectedShortcut` 和 `IsBusy` 的命令刷新通知。
9. 修改 `MainWindow.xaml` 右键菜单，加入 `WebUI`。
10. 编译项目。
11. 手动验证正常打开、缺少端口、端口非法、实例名兜底等场景。

## 22. 明确不做的事情

本次不做：

- 主界面顶部 `WebUI` 按钮。
- WebUI URL 批量生成文件。
- WebUI URL 缓存。
- WebUI 登录自动填充。
- WebUI 连通性预检查。
- 浏览器选择功能。
- 设置窗口中的 WebUI 配置编辑入口。
