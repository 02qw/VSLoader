# v79 AdminUI使用PacService拼接zlpService编码规格说明

## 1. 文档目的

本文件用于指导编程 Agent 修复 AdminUI JNLP 下载链接中的 `zlpService` 拼接策略。

本次需求的核心目标是：

```text
当 INSTANCE.properties 中存在 PacService 时，AdminUI 的 zlpService 应优先使用 PacService。
当 PacService 不存在或为空时，再回退使用 zam.instance.name。
```

该改动用于解决类似以下情况：

```text
PacService = MacMicCommonUI
zam.instance.name = CommonUI
```

当前程序会生成：

```text
zlpService=CommonUI.processor
```

但正确结果应为：

```text
zlpService=MacMicCommonUI.processor
```

## 2. 背景说明

当前 VSLoader 已经支持 AdminUI JNLP 自动下载。

AdminUI 配置属于工作区级配置，保存位置为：

```text
%AppData%\VSLoader\Workspaces\<当前工作区>\config.json
```

当前 AdminUI 下载逻辑会读取快捷项目标文件夹下的：

```text
INSTANCE.properties
```

并从中读取：

```text
zam.instance.name
SocketServer.Port
PacService
```

但是当前代码虽然读取了 `PacService`，却只把它用于“不一致警告”，没有用于实际 URL 拼接。

当前错误逻辑：

```text
zlpService = zam.instance.name + ".processor"
```

正确策略应调整为：

```text
如果 PacService 有值：
    zlpService = PacService + ".processor"
否则：
    zlpService = zam.instance.name + ".processor"
```

## 3. 当前问题示例

### 3.1 当前程序生成的错误 JNLP

```xml
href="CommonUI_Administrator.jnlp?host=WIN-D0UJO6N8E98.macmicst.com&amp;port=10021&amp;zlpService=CommonUI.processor"
```

应用参数中同样是：

```xml
<argument>zlpService=CommonUI.processor</argument>
```

### 3.2 正确 JNLP

```xml
href="CommonUI_Administrator.jnlp?host=WIN-D0UJO6N8E98.macmicst.com&amp;port=10021&amp;zlpService=MacMicCommonUI.processor"
```

应用参数中同样应该是：

```xml
<argument>zlpService=MacMicCommonUI.processor</argument>
```

### 3.3 关键差异

两个文件中以下字段一致：

```text
codebase
href 文件名
host
port
```

唯一导致启动差异的关键字段是：

```text
zlpService
```

## 4. 需求目标

完成后程序应满足：

```text
1. AdminUI 下载时继续从 INSTANCE.properties 读取 zam.instance.name。
2. AdminUI 下载时继续从 INSTANCE.properties 读取 SocketServer.Port。
3. AdminUI 下载时继续从 INSTANCE.properties 读取 PacService。
4. 拼接 JNLP URL 时，zlpService 优先使用 PacService。
5. PacService 不存在、为空或全是空白时，zlpService 回退使用 zam.instance.name。
6. JNLP 文件名仍然使用 zam.instance.name，不改成 PacService。
7. 本地保存的 jnlp 文件名仍然使用 zam.instance.name，不改成 PacService。
8. 普通 PacService 与 zam.instance.name 一致的模块行为不变。
9. CommonUI 这类 PacService 与 zam.instance.name 不一致的模块可以生成正确 zlpService。
```

## 5. 非目标范围

本阶段不实现：

```text
1. 不修改 AdminUI BaseUrl 配置。
2. 不修改 AdminUI Host 配置。
3. 不修改 RoleName 配置。
4. 不修改 INSTANCE.properties 文件名配置。
5. 不修改 SocketServer.Port 读取规则。
6. 不修改 JNLP 文件名策略。
7. 不修改本地 UIdownload 文件保存目录。
8. 不修改 WebUI 逻辑。
9. 不修改批量新增识别逻辑。
10. 不新增设置页面字段。
11. 不新增手动选择 zlpService 来源的开关。
12. 不修改工作区配置归属。
```

## 6. 涉及文件

主要修改：

```text
VSLoader\Models\Services\AdminUiService.cs
```

可能修改或新增测试：

```text
VSLoader.Tests\AdminUiServiceWorkspaceTests.cs
```

或新增：

```text
VSLoader.Tests\AdminUiServiceTests.cs
```

不应修改：

```text
VSLoader\Models\AdminUiConfig.cs
VSLoader\Models\AppConfig.cs
VSLoader\Models\Services\WebUiService.cs
VSLoader\ViewModels\SettingsViewModel.cs
VSLoader\Views\SettingsWindow.xaml
```

除非现有测试结构确实要求少量调整测试辅助代码。

## 7. 技术细节

### 7.1 当前核心代码位置

当前 AdminUI URL 拼接在：

```text
VSLoader\Models\Services\AdminUiService.cs
```

核心方法：

```csharp
private static string BuildJnlpUrl(AdminUiConfig config, string instanceName, string port)
```

当前逻辑类似：

```csharp
var query = string.Join("&", new[]
{
    $"host={Uri.EscapeDataString(config.Host)}",
    $"port={Uri.EscapeDataString(port)}",
    $"zlpService={Uri.EscapeDataString($"{instanceName}.processor")}"
});
```

这里的问题是：

```text
zlpService 写死使用 instanceName。
```

### 7.2 推荐改造方式

将 `BuildJnlpUrl` 的签名改为接收 `serviceName`：

```csharp
private static string BuildJnlpUrl(
    AdminUiConfig config,
    string instanceName,
    string port,
    string serviceName)
```

在方法内部计算实际用于 `zlpService` 的名称：

```csharp
var zlpServiceName = string.IsNullOrWhiteSpace(serviceName)
    ? instanceName
    : serviceName.Trim();
```

然后拼接：

```csharp
$"zlpService={Uri.EscapeDataString($"{zlpServiceName}.processor")}"
```

### 7.3 ResolveShortcutInfo 调用调整

当前 `ResolveShortcutInfo` 中已经读取了：

```csharp
var serviceName = properties.TryGetValue(config.ServiceNameKey, out var value) ? value : string.Empty;
```

需要把它传给 `BuildJnlpUrl`：

```csharp
var url = BuildJnlpUrl(config, instanceName, port, serviceName);
```

### 7.4 保持文件名不变

以下逻辑必须保持使用 `instanceName`：

```csharp
var fileName = $"{Uri.EscapeDataString(instanceName)}_{Uri.EscapeDataString(config.RoleName)}.jnlp";
var localPath = Path.Combine(DownloadDirectory, $"{instanceName}.jnlp");
```

也就是说，对于：

```text
zam.instance.name = CommonUI
PacService = MacMicCommonUI
```

最终结果应为：

```text
远程下载 URL 文件名：CommonUI_Administrator.jnlp
本地保存文件名：CommonUI.jnlp
zlpService：MacMicCommonUI.processor
```

不能改成：

```text
MacMicCommonUI_Administrator.jnlp
MacMicCommonUI.jnlp
```

## 8. 警告逻辑处理

当前下载完成后，如果：

```text
PacService != zam.instance.name
```

程序会加入类似提示：

```text
PacService 与实例名不一致：MacMicCommonUI != CommonUI
```

本次改动后，这个提示不再代表错误。

推荐处理方式：

```text
保留提示，但把语义理解为“信息提示”。
```

也就是说，本次不强制删除这条提示，避免扩大 UI 和结果弹窗改动。

如果后续用户认为这条提示会造成误解，可以另开需求改为：

```text
检测到 PacService 与实例名不一致，已优先使用 PacService 作为 zlpService。
```

本阶段不要求修改提示文案。

## 9. 兼容性规则

### 9.1 PacService 存在且有值

输入：

```properties
zam.instance.name=CommonUI
SocketServer.Port=10021
PacService=MacMicCommonUI
```

输出 URL 必须包含：

```text
zlpService=MacMicCommonUI.processor
```

### 9.2 PacService 为空

输入：

```properties
zam.instance.name=TYLC001
SocketServer.Port=10094
PacService=
```

输出 URL 必须包含：

```text
zlpService=TYLC001.processor
```

### 9.3 PacService 不存在

输入：

```properties
zam.instance.name=TYLC001
SocketServer.Port=10094
```

输出 URL 必须包含：

```text
zlpService=TYLC001.processor
```

### 9.4 PacService 与实例名一致

输入：

```properties
zam.instance.name=TYLC001
SocketServer.Port=10094
PacService=TYLC001
```

输出 URL 必须包含：

```text
zlpService=TYLC001.processor
```

## 10. 测试要求

### 10.1 新增 PacService 优先测试

建议新增测试：

```text
DownloadOne_uses_pacservice_for_zlpservice_when_present
```

测试准备：

```text
1. 创建临时快捷项目标目录。
2. 写入 INSTANCE.properties。
3. 内容包含：
   zam.instance.name=CommonUI
   SocketServer.Port=10021
   PacService=MacMicCommonUI
4. 使用可控的 BaseUrl、Host、RoleName 配置。
5. 调用能生成或捕获 URL 的逻辑。
```

预期断言：

```text
生成的 URL 包含 zlpService=MacMicCommonUI.processor。
生成的 URL 仍包含 CommonUI_Administrator.jnlp。
本地保存路径仍以 CommonUI.jnlp 结尾。
```

如果当前 `BuildJnlpUrl` 是 private，不建议为了测试直接把它改成 public。  
可以优先通过现有服务方法间接测试，或在已有测试模式中复用测试服务器捕获请求路径。

### 10.2 新增回退测试

建议新增测试：

```text
DownloadOne_falls_back_to_instance_name_when_pacservice_missing
```

输入：

```properties
zam.instance.name=TYLC001
SocketServer.Port=10094
```

预期：

```text
zlpService=TYLC001.processor
```

### 10.3 空白值测试

建议新增测试：

```text
DownloadOne_falls_back_to_instance_name_when_pacservice_is_blank
```

输入：

```properties
zam.instance.name=TYLC001
SocketServer.Port=10094
PacService=
```

或：

```properties
PacService=   
```

预期：

```text
zlpService=TYLC001.processor
```

## 11. 手工验收

### 场景一：CommonUI 特殊模块

目标目录 `INSTANCE.properties`：

```properties
zam.instance.name=CommonUI
SocketServer.Port=10021
PacService=MacMicCommonUI
```

操作：

```text
1. 在 VSLoader 中选择该快捷项。
2. 右键点击“获取AdminUI连接”或点击自动获取链接。
3. 打开下载到 UIdownload 中的 CommonUI.jnlp。
```

预期：

```text
href 中包含 zlpService=MacMicCommonUI.processor
application-desc 参数中包含 zlpService=MacMicCommonUI.processor
```

### 场景二：普通模块

目标目录 `INSTANCE.properties`：

```properties
zam.instance.name=TYLC001
SocketServer.Port=10094
PacService=TYLC001
```

预期：

```text
zlpService=TYLC001.processor
```

### 场景三：没有 PacService 的旧配置

目标目录 `INSTANCE.properties`：

```properties
zam.instance.name=TYLC001
SocketServer.Port=10094
```

预期：

```text
仍然可以下载 JNLP。
zlpService=TYLC001.processor。
程序不崩溃。
```

## 12. 风险点

### 12.1 错误地把文件名也改成 PacService

风险：

```text
服务器上的 JNLP 文件名仍然可能是 CommonUI_Administrator.jnlp。
如果改成 MacMicCommonUI_Administrator.jnlp，反而会导致下载失败。
```

规避：

```text
只改变 zlpService 参数，不改变 href 文件名和本地文件名策略。
```

### 12.2 PacService 为空时生成空 processor

风险：

```text
zlpService=.processor
```

规避：

```text
必须对 PacService 做 string.IsNullOrWhiteSpace 判断。
```

### 12.3 修改范围过大

风险：

```text
改动设置窗口、配置模型或工作区配置，可能引入不必要风险。
```

规避：

```text
本次只改 AdminUiService 的 zlpService 拼接策略和对应测试。
```

## 13. 验收标准

本需求完成必须满足：

```text
1. CommonUI 场景下生成 zlpService=MacMicCommonUI.processor。
2. href 文件名仍然是 CommonUI_Administrator.jnlp。
3. 本地文件名仍然是 CommonUI.jnlp。
4. PacService 缺失时回退使用 zam.instance.name。
5. PacService 为空白时回退使用 zam.instance.name。
6. 普通模块不受影响。
7. dotnet build 通过。
8. dotnet test 通过。
```

## 14. 推荐执行命令

实现前停止运行中的程序：

```powershell
Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force
```

构建：

```powershell
dotnet build C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader.sln -p:UseSharedCompilation=false
```

测试：

```powershell
dotnet test C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader.sln -p:UseSharedCompilation=false
```

## 15. 推荐提交信息

```text
fix: use pacservice for adminui zlpservice
```
