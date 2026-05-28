# v4 AdminUI 自动获取连接编码规格说明

## 1. 文档目的

本文档用于指导编程 agent 或开发人员在当前 VSLoader 项目中集成 AdminUI JNLP 自动下载和打开功能。

该功能来源于 `C:\Users\shee_\OneDrive\Desktop\deepDownV2` 中的 PowerShell 工具逻辑，但 v4 要改写为适合 VSLoader 项目的 C# 实现，不直接依赖 PowerShell 脚本。

## 2. 目标效果

VSLoader 中每个快捷项最终具备两个入口：

1. 双击快捷项或点击“打开”：用 VSCode 打开该快捷项的 `TargetPath`。
2. 选中快捷项后点击“AdminUI”：打开该快捷项对应的 `.jnlp` 文件。

新增两个主界面按钮：

```text
自动获取连接
AdminUI
```

推荐按钮顺序：

```text
新增 | 批量新增识别 | 自动获取连接 | AdminUI | 编辑 | 删除 | 打开 | 设置
```

## 3. 用户确认的实现选择

| 项目 | 选择 |
| --- | --- |
| 实现方式 | 改写为 C# 代码，不直接调用 PowerShell |
| 下载目录 | `%AppData%\VSLoader\UIdownload` |
| 下载范围 | 当前主列表里的所有快捷项 |
| JNLP 参数 | 做成 VSLoader 配置项 |
| 打开方式 | 使用 Windows 默认方式打开 `.jnlp`，类似双击 |
| 未找到 JNLP | 提示用户先点击“自动获取连接” |
| 匹配关系 | 从快捷项 `TargetPath\INSTANCE.properties` 中读取 `zam.instance.name` |
| 同名 JNLP 已存在 | 下载成功后直接覆盖旧文件 |
| 下载失败时 | 不删除旧文件，保留原文件 |

## 4. deepDownV2 逻辑摘要

原工具主要逻辑：

1. 扫描实例根目录的一级子文件夹。
2. 读取每个实例目录下的 `INSTANCE.properties`。
3. 获取：

```properties
zam.instance.name
SocketServer.Port
PacService
```

4. 拼接 JNLP URL：

```text
{baseUrl}/{instanceName}_{roleName}.jnlp?host={host}&port={port}&zlpService={instanceName}.processor
```

5. 下载为：

```text
{instanceName}.jnlp
```

VSLoader v4 不再扫描独立的 `instanceRootDir`，而是遍历当前快捷项列表中的 `TargetPath`。

## 5. 配置模型修改

修改：

```text
Models\AppConfig.cs
```

新增：

```csharp
public AdminUiConfig AdminUi { get; set; } = new();
```

新增文件：

```text
Models\AdminUiConfig.cs
```

内容：

```csharp
public sealed class AdminUiConfig
{
    public string BaseUrl { get; set; } = "https://192.168.15.69:8181/oistarter/";

    public string Host { get; set; } = "SICEAPO1.macmicst.com";

    public string RoleName { get; set; } = "Administrator";

    public string InstancePropertiesName { get; set; } = "INSTANCE.properties";

    public string InstanceNameKey { get; set; } = "zam.instance.name";

    public string PortKey { get; set; } = "SocketServer.Port";

    public string ServiceNameKey { get; set; } = "PacService";

    public bool IgnoreCertificateErrors { get; set; } = true;
}
```

说明：

- `UIdownload` 路径不做成配置项，固定为 `%AppData%\VSLoader\UIdownload`。
- 如果旧配置文件没有 `AdminUi` 字段，读取配置时应自动补默认值。

## 6. 配置服务修改

修改：

```text
Services\ConfigService.cs
```

读取配置后需要补默认值：

```csharp
config.AdminUi ??= new AdminUiConfig();
```

保证旧版本 `config.json` 兼容。

## 7. 新增服务

新增：

```text
Services\AdminUiService.cs
```

职责：

- 获取 AdminUI 下载目录。
- 读取 `INSTANCE.properties`。
- 解析实例名、端口、服务名。
- 拼接 JNLP 下载 URL。
- 下载 JNLP 文件到 `%AppData%\VSLoader\UIdownload`。
- 根据快捷项查找本地 JNLP 文件。
- 打开本地 JNLP 文件。

## 8. 新增结果模型

建议新增：

```text
Services\AdminUiDownloadResult.cs
Services\AdminUiShortcutInfo.cs
```

### 8.1 AdminUiShortcutInfo

用于表示从某个快捷项解析出来的 AdminUI 信息：

```csharp
public sealed class AdminUiShortcutInfo
{
    public ShortcutItem Shortcut { get; init; } = new();

    public string InstanceName { get; init; } = string.Empty;

    public string Port { get; init; } = string.Empty;

    public string ServiceName { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string LocalJnlpPath { get; init; } = string.Empty;
}
```

### 8.2 AdminUiDownloadResult

用于汇总一次“自动获取连接”的执行结果：

```csharp
public sealed class AdminUiDownloadResult
{
    public int SuccessCount { get; init; }

    public int FailedCount { get; init; }

    public List<string> Messages { get; init; } = new();
}
```

## 9. AdminUiService 行为规格

### 9.1 下载目录

固定目录：

```text
%AppData%\VSLoader\UIdownload
```

C# 获取方式：

```csharp
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var downloadDir = Path.Combine(appData, "VSLoader", "UIdownload");
```

如果目录不存在，自动创建。

### 9.2 读取 INSTANCE.properties

对每个快捷项：

```text
shortcut.TargetPath\{InstancePropertiesName}
```

默认：

```text
shortcut.TargetPath\INSTANCE.properties
```

读取规则：

- UTF-8 读取。
- 忽略空行。
- 忽略以 `#` 开头的注释行。
- 每行按第一个 `=` 拆分 key 和 value。
- key 和 value 都要 `Trim()`。

### 9.3 必填字段

默认必填：

```text
zam.instance.name
SocketServer.Port
```

字段名来自配置：

```text
InstanceNameKey
PortKey
```

如果缺失：

```text
该快捷项下载失败，记录错误原因，继续处理下一个快捷项。
```

`PacService` 不是必填，但如果存在且和实例名不一致，需要记录警告消息，不阻断下载。

### 9.4 URL 拼接

格式：

```text
{BaseUrl}/{InstanceName}_{RoleName}.jnlp?host={Host}&port={Port}&zlpService={InstanceName}.processor
```

注意：

- `BaseUrl` 需要去掉末尾 `/` 后再统一补一个 `/`。
- query 参数需要 URL 编码。
- 保存本地文件时不带 `_Administrator`。

示例：

```text
https://192.168.15.69:8181/oistarter/TSSM009_Administrator.jnlp?host=SICEAPO1.macmicst.com&port=10063&zlpService=TSSM009.processor
```

保存为：

```text
%AppData%\VSLoader\UIdownload\TSSM009.jnlp
```

### 9.5 HTTPS 证书

如果：

```csharp
IgnoreCertificateErrors = true
```

下载时忽略 HTTPS 证书错误。

推荐 C# 实现：

- 使用 `HttpClientHandler`。
- 设置 `ServerCertificateCustomValidationCallback` 返回 `true`。

### 9.6 同名文件覆盖策略

如果本地已存在：

```text
%AppData%\VSLoader\UIdownload\TSSM009.jnlp
```

再次下载成功后：

```text
直接覆盖旧文件，不弹确认。
```

如果下载失败：

```text
保留旧文件，不删除。
```

建议实现方式：

- 先下载到临时文件。
- 下载成功后用临时文件覆盖正式文件。
- 下载失败时删除临时文件，保留旧正式文件。

临时文件示例：

```text
TSSM009.jnlp.tmp
```

## 10. 自动获取连接按钮逻辑

修改：

```text
ViewModels\MainViewModel.cs
MainWindow.xaml
```

新增命令：

```csharp
DownloadAdminUiLinksCommand
```

点击后：

1. 获取当前主列表中的所有快捷项。
2. 如果没有快捷项，提示：

```text
当前没有快捷项可处理。
```

3. 调用 `AdminUiService` 下载所有快捷项对应 JNLP。
4. 下载完成后弹窗汇总：

```text
自动获取连接完成。
成功：X
失败：Y
```

5. 如果有失败，显示失败摘要。

说明：

- 下载范围是当前主列表所有快捷项，不是只下载选中项。
- 如果当前搜索框过滤了列表，建议仍然使用完整 `Shortcuts` 集合，而不是过滤后的视图。

## 11. AdminUI 按钮逻辑

新增命令：

```csharp
OpenAdminUiCommand
```

按钮启用规则：

- 需要选中一个快捷项。
- 未选中时不可用。

点击后：

1. 读取选中快捷项 `TargetPath\INSTANCE.properties`。
2. 获取 `zam.instance.name`。
3. 拼接本地 JNLP 路径：

```text
%AppData%\VSLoader\UIdownload\{instanceName}.jnlp
```

4. 如果文件不存在，提示：

```text
未找到对应 AdminUI 文件，请先点击“自动获取连接”。
```

5. 如果文件存在，使用 Windows 默认方式打开。

推荐实现：

```csharp
Process.Start(new ProcessStartInfo
{
    FileName = localJnlpPath,
    UseShellExecute = true
});
```

## 12. 设置窗口修改

需要将 AdminUI 参数做成 VSLoader 配置项。

修改：

```text
Views\SettingsWindow.xaml
ViewModels\SettingsViewModel.cs
```

设置窗口新增 AdminUI 配置区域，字段：

- BaseUrl
- Host
- RoleName
- InstancePropertiesName
- InstanceNameKey
- PortKey
- ServiceNameKey
- IgnoreCertificateErrors

默认值使用 `AdminUiConfig` 中的默认值。

保存设置时：

- VSCode 路径仍按原规则校验。
- AdminUI 配置项不做复杂网络校验。
- 必填字符串不能为空。
- `IgnoreCertificateErrors` 使用复选框。

## 13. UI 修改

主窗口按钮建议顺序：

```text
新增
批量新增识别
自动获取连接
AdminUI
编辑
删除
打开
设置
```

其中：

- `自动获取连接` 无需选中快捷项。
- `AdminUI` 需要选中快捷项。

## 14. 错误处理

| 场景 | 处理方式 |
| --- | --- |
| 当前没有快捷项 | 提示当前没有快捷项可处理 |
| TargetPath 不存在 | 该快捷项下载失败，继续处理其他项 |
| INSTANCE.properties 不存在 | 该快捷项下载失败，继续处理其他项 |
| 缺少实例名 | 该快捷项下载失败，继续处理其他项 |
| 缺少端口 | 该快捷项下载失败，继续处理其他项 |
| 端口不是数字 | 该快捷项下载失败，继续处理其他项 |
| PacService 和实例名不一致 | 记录警告，不阻断下载 |
| JNLP 下载失败 | 记录失败，保留旧文件 |
| AdminUI 文件不存在 | 提示先点击“自动获取连接” |
| 打开 JNLP 失败 | 弹窗提示失败原因 |

## 15. 验收标准

- 主窗口出现“自动获取连接”按钮。
- 主窗口出现“AdminUI”按钮。
- “AdminUI”按钮未选中快捷项时不可用。
- 点击“自动获取连接”会遍历当前所有快捷项。
- 每个快捷项会读取 `TargetPath\INSTANCE.properties`。
- 能从 `zam.instance.name` 和 `SocketServer.Port` 拼接 JNLP URL。
- JNLP 下载到 `%AppData%\VSLoader\UIdownload`。
- 同名 JNLP 已存在时，下载成功后覆盖旧文件。
- 下载失败时保留旧文件。
- 选中快捷项点击“AdminUI”可以打开对应 `.jnlp` 文件。
- 未找到对应 `.jnlp` 时提示“未找到对应 AdminUI 文件，请先点击‘自动获取连接’。”
- AdminUI 参数可以在设置窗口中配置并保存。
- 旧版 `config.json` 没有 `AdminUi` 字段时仍能正常启动。
- `dotnet build .\VSLoader.sln` 必须通过。

