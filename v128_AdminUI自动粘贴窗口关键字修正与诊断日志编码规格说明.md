# v128 AdminUI 自动粘贴窗口关键字修正与诊断日志编码规格说明

## 1. 背景

v127 已实现 AdminUI 密码自动粘贴逻辑：

```text
打开 AdminUI -> 解密密码 -> 写入剪贴板 -> 等待前台窗口匹配 AdminUI -> 发送 Ctrl+V + Enter
```

但实际测试中出现问题：

```text
即使 AdminUI 登录窗口已经处于焦点，也没有触发粘贴 + 确认。
```

用户截图显示，真实登录窗口标题是：

```text
TAOI008.processor
```

而设置页中当前默认窗口标题关键字是：

```text
znt client
```

当前匹配逻辑要求：

```text
前台窗口标题包含设置里的关键字
```

因此：

```text
TAOI008.processor 不包含 znt client
=> 匹配失败
=> 不执行 Ctrl+V + Enter
```

这说明 v127 的默认关键字来自 JNLP 内部信息，不符合真实 Java 登录窗口标题。

## 2. 当前相关代码

### 2.1 默认配置

文件：

```text
VSLoader/Models/AdminUiConfig.cs
```

当前默认值：

```csharp
public string AutoPasteWindowTitleKeyword { get; set; } = "znt client";
```

### 2.2 设置页

文件：

```text
VSLoader/Views/SettingsWindow.xaml
```

当前 UI 显示：

```text
窗口标题关键字
```

绑定：

```xml
Text="{Binding AdminUi.AutoPasteWindowTitleKeyword, UpdateSourceTrigger=PropertyChanged}"
```

### 2.3 匹配逻辑

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

当前逻辑：

```csharp
var titleKeyword = config.AutoPasteWindowTitleKeyword.Trim();
if (string.IsNullOrWhiteSpace(titleKeyword)
    || !window.Title.Contains(titleKeyword, StringComparison.OrdinalIgnoreCase))
{
    return false;
}
```

该逻辑本身没有错，问题在默认关键字不适合真实窗口标题。

### 2.4 前台窗口读取

文件：

```text
VSLoader/Models/Services/ForegroundWindowService.cs
```

当前读取：

```text
GetForegroundWindow()
GetWindowText(...)
GetWindowThreadProcessId(...)
Process.GetProcessById(...)
```

可以拿到：

```text
窗口标题
进程名
```

但目前没有日志记录，所以用户无法知道程序实际读到了什么窗口。

## 3. 问题描述

当前排查到的第一问题是：

```text
默认窗口标题关键字 znt client 与真实 AdminUI 登录窗口标题 TAOI008.processor 不匹配。
```

但还存在第二类潜在问题：

```text
即使标题关键字改对了，如果进程名不在 java/javaw/javaws 中，或者程序读到的前台窗口不是用户肉眼看到的窗口，仍会不触发。
```

如果没有诊断日志，后续每次都只能靠猜：

- 当前前台窗口标题到底是什么？
- 当前前台窗口进程名到底是什么？
- 标题是否匹配？
- 进程名是否匹配？
- 是否进入了自动粘贴等待流程？
- 是否因为超时放弃？

因此本次需要同时做：

```text
修正默认关键字
增加自动粘贴诊断日志
```

## 4. 目标

本次目标：

- 将 AdminUI 自动粘贴默认窗口标题关键字改为更符合真实窗口标题的：

```text
processor
```

- 老配置如果仍是旧默认值 `znt client`，在加载/保存时应能升级到新默认值，避免老用户保留错误默认值。
- 增加自动粘贴诊断日志，记录等待过程中的关键判断。
- 日志必须能帮助判断：
  - 是否启用自动粘贴。
  - 初始等待、超时、轮询间隔是多少。
  - 每次轮询读到的前台窗口标题和进程名是什么。
  - 标题是否匹配。
  - 进程是否匹配。
  - 是否发送了 `Ctrl+V + Enter`。
  - 是否超时。
- 不改变 v127 的安全原则：
  - 不匹配不发送。
  - 只发送一次。
  - 不主动抢焦点。
  - 自动粘贴失败只状态提示，不弹错误框。

## 5. 非目标

本次不做以下事项：

- 不取消窗口标题关键字匹配。
- 不放宽到“只要是 Java 窗口就发送”。
- 不主动调用 `SetForegroundWindow(...)` 抢焦点。
- 不改用 UI Automation 定位密码输入框。
- 不重写 AdminUI 打开逻辑。
- 不修改 JNLP 下载和拼接逻辑。
- 不改变密码加密/剪贴板写入逻辑。
- 不把自动粘贴默认改成启用。

## 6. 推荐方案

### 6.1 默认关键字改为 processor

修改：

```text
VSLoader/Models/AdminUiConfig.cs
```

从：

```csharp
public string AutoPasteWindowTitleKeyword { get; set; } = "znt client";
```

改为：

```csharp
public string AutoPasteWindowTitleKeyword { get; set; } = "processor";
```

原因：

用户截图中的真实登录窗口标题：

```text
TAOI008.processor
```

`processor` 可以匹配：

```text
TAOI008.processor
TYLC001.processor
CommonUI.processor
MacMicCommonUI.processor
```

并且仍然比空关键字安全。

### 6.2 兼容旧默认值 znt client

老工作区配置已经保存过：

```json
"autoPasteWindowTitleKeyword": "znt client"
```

如果只改模型默认值，老配置不会自动变化。

因此需要在配置规范化时处理旧默认值。

修改：

```text
VSLoader/Models/Services/ConfigService.cs
```

在 `NormalizeConfig(...)` 中增加逻辑：

```csharp
NormalizeAdminUiAutoPasteConfig(config.AdminUi);
```

建议实现：

```csharp
private static void NormalizeAdminUiAutoPasteConfig(AdminUiConfig adminUi)
{
    if (string.Equals(adminUi.AutoPasteWindowTitleKeyword?.Trim(), "znt client", StringComparison.OrdinalIgnoreCase))
    {
        adminUi.AutoPasteWindowTitleKeyword = "processor";
    }

    if (string.IsNullOrWhiteSpace(adminUi.AutoPasteWindowTitleKeyword))
    {
        adminUi.AutoPasteWindowTitleKeyword = "processor";
    }

    if (string.IsNullOrWhiteSpace(adminUi.AutoPasteProcessNames))
    {
        adminUi.AutoPasteProcessNames = "java;javaw;javaws";
    }

    adminUi.AutoPasteTimeoutSeconds = Math.Clamp(adminUi.AutoPasteTimeoutSeconds, 1, 60);
    adminUi.AutoPasteInitialDelayMilliseconds = Math.Clamp(adminUi.AutoPasteInitialDelayMilliseconds, 0, 30000);
    adminUi.AutoPastePollIntervalMilliseconds = Math.Clamp(adminUi.AutoPastePollIntervalMilliseconds, 100, 2000);
}
```

注意：

```text
只迁移旧默认值 znt client。
如果用户手动改成其它值，不要覆盖。
```

### 6.3 增加自动粘贴诊断日志

新增服务：

```text
VSLoader/Models/Services/AdminUiAutoPasteLogService.cs
```

职责：

- 写入自动粘贴诊断日志。
- 日志路径建议：

```text
%LocalAppData%\VSLoader\logs\adminui-autopaste.log
```

或：

```text
%LocalAppData%\VSLoader\logs\adminui-autopaste-yyyyMMdd.log
```

推荐按天滚动：

```text
adminui-autopaste-20260706.log
```

原因：

- 不污染工作区数据。
- 自动化诊断属于程序运行日志。
- 便于用户把日志发给开发者。

### 6.4 日志内容格式

每行写一条简单文本，包含时间戳：

```text
2026-07-06 10:23:12.123 [Start] enabled=True timeout=12s initialDelay=2500ms poll=300ms titleKeyword="processor" processNames="java;javaw;javaws"
2026-07-06 10:23:14.731 [Poll] title="VSLoader v1.0.0 - 碳化硅线" process="VSLoader" titleMatch=False processMatch=False
2026-07-06 10:23:15.032 [Poll] title="TAOI008.processor" process="javaw" titleMatch=True processMatch=True
2026-07-06 10:23:15.033 [Send] title="TAOI008.processor" process="javaw"
```

失败示例：

```text
2026-07-06 10:24:20.123 [Timeout] message="等待超时，未检测到 AdminUI 前台窗口。"
```

异常示例：

```text
2026-07-06 10:24:20.456 [Error] message="..."
```

要求：

- 日志失败不能影响主流程。
- 捕获并吞掉日志写入异常。
- 不记录密码。
- 不记录剪贴板内容。

### 6.5 AdminUiAutoPasteService 接入日志

修改：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

新增依赖：

```csharp
private readonly AdminUiAutoPasteLogService logService;
```

构造函数默认：

```csharp
public AdminUiAutoPasteService()
    : this(new ForegroundWindowService(), new KeyboardInputService(), new AdminUiAutoPasteLogService())
{
}
```

为了测试，保留内部构造函数，允许注入 no-op logger 或 fake logger。

轮询中记录：

```csharp
var window = getForegroundWindowInfo();
var match = EvaluateAdminUiWindow(window, config);
logService.LogPoll(window, match.TitleMatch, match.ProcessMatch);
if (match.IsMatch)
{
    sendPasteAndEnter();
    logService.LogSend(window!);
    return AdminUiAutoPasteResult.Ok(window!);
}
```

为避免重复计算，可将当前 `IsAdminUiWindow(...)` 拆成：

```csharp
internal static AdminUiWindowMatch EvaluateAdminUiWindow(ForegroundWindowInfo? window, AdminUiConfig config)
```

模型：

```csharp
internal sealed class AdminUiWindowMatch
{
    public bool IsMatch { get; init; }
    public bool TitleMatch { get; init; }
    public bool ProcessMatch { get; init; }
}
```

保留原：

```csharp
internal static bool IsAdminUiWindow(...)
```

用于兼容现有测试：

```csharp
return EvaluateAdminUiWindow(window, config).IsMatch;
```

## 7. 设置页文案优化

当前设置项名：

```text
窗口标题关键字
```

建议改为：

```text
登录窗口标题关键字
```

并可增加 ToolTip：

```text
用于识别 AdminUI 登录弹窗，例如 TAOI008.processor 可填写 processor。
```

这样用户不容易误以为应该填写 JNLP 内部的 `znt client`。

## 8. 测试要求

### 8.1 AdminUiConfig 默认值测试

更新：

```text
VSLoader.Tests/AdminUiConfigTests.cs
```

将默认断言从：

```csharp
Assert.Equal("znt client", config.AutoPasteWindowTitleKeyword);
```

改为：

```csharp
Assert.Equal("processor", config.AutoPasteWindowTitleKeyword);
```

### 8.2 配置迁移测试

新增或更新：

```text
VSLoader.Tests/ConfigServiceAdminUiAutoPasteTests.cs
```

测试：

- 旧配置中的 `AutoPasteWindowTitleKeyword = "znt client"` 加载后变成 `processor`。
- 空关键字加载后变成 `processor`。
- 用户自定义关键字如 `"TAOI"` 不被覆盖。
- 空进程名加载后恢复默认 `java;javaw;javaws`。

### 8.3 匹配测试

更新：

```text
VSLoader.Tests/AdminUiAutoPasteServiceTests.cs
```

增加：

```csharp
[Fact]
public void IsAdminUiWindow_matches_real_processor_login_title_with_default_keyword()
{
    var config = new AdminUiConfig();
    var window = new ForegroundWindowInfo
    {
        Handle = new IntPtr(123),
        Title = "TAOI008.processor",
        ProcessName = "javaw"
    };

    Assert.True(AdminUiAutoPasteService.IsAdminUiWindow(window, config));
}
```

### 8.4 日志测试

新增：

```text
VSLoader.Tests/AdminUiAutoPasteLogServiceTests.cs
```

测试：

- `LogStart(...)` 会创建日志文件。
- 日志包含 `titleKeyword="processor"`。
- `LogPoll(...)` 会写入窗口标题、进程名、titleMatch、processMatch。
- 日志不包含密码字段。

日志测试应使用临时目录注入：

```csharp
var service = new AdminUiAutoPasteLogService(tempRoot);
```

### 8.5 自动粘贴服务日志调用测试

如果 `AdminUiAutoPasteLogService` 不方便 mock，可以通过临时目录真实日志验证：

- 一次成功匹配后日志包含 `[Send]`。
- 超时后日志包含 `[Timeout]`。

## 9. 手工验证场景

### 9.1 默认关键字匹配真实窗口

步骤：

1. 打开设置。
2. 启用自动粘贴密码并回车。
3. 确认登录窗口标题关键字为：

```text
processor
```

4. 打开 AdminUI。
5. AdminUI 登录窗口变成前台。

期望：

```text
自动粘贴密码并回车。
```

### 9.2 查看诊断日志

路径：

```text
%LocalAppData%\VSLoader\logs\
```

打开当天：

```text
adminui-autopaste-yyyyMMdd.log
```

期望看到：

```text
[Start]
[Poll] title="TAOI008.processor" process="javaw" titleMatch=True processMatch=True
[Send]
```

如果没有发送，日志应能看出是标题不匹配还是进程不匹配。

### 9.3 用户自定义关键字

如果某条线窗口标题不是 `.processor`，用户仍可在设置中填写其它关键字。

保存后不应被程序强制改回 `processor`。

## 10. 验证命令

定向测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~AdminUiConfigTests|FullyQualifiedName~AdminUiAutoPasteServiceTests|FullyQualifiedName~ConfigServiceAdminUiAutoPasteTests|FullyQualifiedName~AdminUiAutoPasteLogServiceTests" -p:BaseOutputPath=.\artifacts\test-output\
```

全量测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\test-output\
```

构建：

```powershell
dotnet build .\VSLoader.sln -c Debug --no-restore -p:BaseOutputPath=.\artifacts\test-output\
```

如需覆盖 Debug 输出目录，先从托盘彻底退出 VSLoader，再运行：

```powershell
dotnet build .\VSLoader.sln -c Debug --no-restore
```

## 11. 验收标准

实现完成后必须满足：

- 新建默认 AdminUiConfig 的 `AutoPasteWindowTitleKeyword` 为 `processor`。
- 老配置 `znt client` 能迁移为 `processor`。
- 用户自定义关键字不会被覆盖。
- `TAOI008.processor + javaw` 能被识别为 AdminUI。
- 自动粘贴流程写入诊断日志。
- 日志包含前台窗口标题、进程名、标题匹配结果、进程匹配结果。
- 日志不记录密码。
- 日志写入失败不影响主流程。
- 设置页文案改为“登录窗口标题关键字”。
- 定向测试通过。
- 全量测试通过。
- Debug 构建通过。

