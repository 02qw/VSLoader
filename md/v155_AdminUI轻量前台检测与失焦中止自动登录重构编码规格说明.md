# v155 AdminUI 轻量前台检测与失焦中止自动登录重构编码规格说明

## 1. 背景

当前 AdminUI 自动登录已经经历多轮增强，具备以下能力：

```text
启动 JNLP
写入剪贴板并重试
枚举全部顶层窗口
严格匹配 SunAwtDialog
强制 SetForegroundWindow
焦点重试
BlockInput 输入锁
透明 Overlay 兜底
Ctrl+V 或 Unicode 直接输入
Enter 前再次抢回焦点
全过程高频日志
```

这些逻辑解决了密码误输入、剪贴板被占用和用户主动切换窗口等问题，但当前实现已经出现严重性能问题：

```text
1. 自动登录等待期间偶发明显卡顿。
2. 某些情况下 VSLoader 界面短暂无响应。
3. BlockInput 生效时，用户会感知到整个系统无法操作，体感接近卡死。
4. 高频窗口枚举和日志重写造成不必要的 CPU、进程查询和磁盘开销。
```

结合当前代码确认，主要性能风险包括：

```text
1. 每 50ms 执行一次 EnumWindows，遍历全部可见顶层窗口。
2. 每次枚举对多个窗口执行 Process.GetProcessById。
3. 每轮扫描写入多条日志。
4. RollingLogFileWriter 每次写日志都会读取并重写最多 2000 行日志。
5. 自动登录异步任务没有显式切换到后台线程，延续逻辑通常恢复到 WPF UI 线程。
6. SetForegroundWindow 重试过程包含 Thread.Sleep。
7. BlockInput 会冻结整个用户输入环境。
8. BlockInput 失败后创建覆盖全部显示器的 WPF 透明遮罩。
```

本次需求将自动登录策略从“强制控制型”改为“前台机会型”：

```text
只有 AdminUI 登录框自然处于前台时才执行自动输入。
用户一旦让目标登录框失焦，立即中止本轮自动化。
程序不再抢回焦点，不再锁定用户输入，不再创建遮罩，也不再高频枚举全部窗口。
```

本规格替代以下旧方案中与自动登录执行链路冲突的部分：

```text
v142 AdminUI 后台任务与强控制极速自动登录编码规格说明
v154 AdminUI 剪贴板失败逐字符输入兜底编码规格说明
```

## 2. 重构目标

### 2.1 核心目标

```text
1. 自动登录等待和输入过程不得阻塞 WPF UI 线程。
2. 不再枚举全部顶层窗口，只检查当前前台窗口。
3. 不再调用 SetForegroundWindow 强制抢焦点。
4. 不再调用 BlockInput。
5. 不再创建 CriticalInputOverlay。
6. 自动登录默认直接使用 Unicode SendInput 输入密码，不依赖剪贴板。
7. 输入前或 Enter 前发现目标窗口失焦，立即中止。
8. 日志由轮询型改为事件型，不记录每次扫描。
9. 新任务取消旧任务，程序退出时显式取消任务。
10. 清理被新方案淘汰的代码、配置字段和测试，不遗留双轨逻辑。
```

### 2.2 用户视角

用户点击 AdminUI 后：

```text
1. VSLoader 启动对应 AdminUI JNLP。
2. 如果启用了自动登录，立即启动轻量后台等待任务。
3. VSLoader 主界面保持可操作。
4. AdminUI 的 SunAwtDialog 自然成为前台后，程序快速输入密码。
5. 密码输入后等待 10ms。
6. 如果登录框仍然是前台，则发送 Enter。
7. 如果用户中途切到其它程序，自动化立即中止，不抢回 AdminUI。
```

## 3. 新的完整运行流程

### 3.1 自动登录开启

```text
用户点击 AdminUI
    ↓
解析并启动 JNLP
    ↓
读取当前工作区 AdminUI 密码
    ↓
密码为空？
    ├─ 是：提示未配置密码，结束
    └─ 否：启动后台自动登录任务
              ↓
         每 100ms 读取一次当前前台窗口
              ↓
         当前窗口是否严格匹配 SunAwtDialog？
              ├─ 否：继续等待，最长默认 12 秒
              └─ 是：记录目标窗口句柄
                        ↓
                   进行一次短稳定确认
                        ↓
                   前台仍是同一窗口句柄？
                        ├─ 否：中止自动化
                        └─ 是：Unicode 输入密码
                                  ↓
                             等待 10ms
                                  ↓
                             前台仍是同一窗口句柄？
                                  ├─ 否：中止，不发送 Enter
                                  └─ 是：发送 Enter，任务结束
```

### 3.2 自动登录关闭

关闭“自动粘贴密码并回车”时保留手动使用方式：

```text
启动 AdminUI
读取密码
尝试写入剪贴板
成功：提示密码已复制
失败：显示可追溯错误信息
```

自动登录关闭时不得绕过用户设置执行 Unicode 输入。

### 3.3 自动登录失败

以下任一情况发生时，本轮任务立即结束：

```text
1. 等待超时仍未发现前台 SunAwtDialog。
2. 输入密码前目标窗口失焦。
3. 密码输入后、Enter 前目标窗口失焦。
4. SendInput 返回数量不完整。
5. 任务被新一轮 AdminUI 启动取消。
6. VSLoader 正在退出。
```

失败后不得自动抢回窗口，也不得无限重试。

## 4. 严格窗口匹配规则

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

只检查 `ForegroundWindowService.GetForegroundWindowInfo()` 返回的当前前台窗口。

必须同时满足：

```text
1. Handle != IntPtr.Zero。
2. Title 包含 AutoPasteWindowTitleKeyword，默认 processor，不区分大小写。
3. ProcessName 属于 AutoPasteProcessNames，默认 java、javaw、javaws。
4. ClassName 精确等于 SunAwtDialog，不区分大小写。
```

禁止以下行为：

```text
1. 禁止回退到 SunAwtFrame。
2. 禁止使用标题为空或进程名为空的窗口。
3. 禁止对后台窗口执行输入。
4. 禁止枚举所有顶层窗口寻找后台 SunAwtDialog。
5. 禁止在窗口失焦后通过 SetForegroundWindow 拉回。
```

## 5. 线程与任务模型

### 5.1 AdminUiAutoLoginCoordinator

文件：

```text
VSLoader/Models/Services/AdminUiAutoLoginCoordinator.cs
```

保留该协调器，但重构为真正的后台任务协调入口。

职责：

```text
1. 为每次启动创建 sessionId。
2. 取消上一轮仍在运行的任务。
3. 使用 Task.Run 或等价方式确保等待、窗口检查和键盘输入不恢复到 WPF UI 线程。
4. 使用 SemaphoreSlim 保证同一时间只有一个输入序列。
5. 通过回调把最终结果交给 MainViewModel。
6. MainViewModel 通过 Dispatcher 更新 UI。
7. Shutdown/Dispose 时取消当前任务。
```

要求：

```text
1. 不允许 fire-and-forget 异常成为未观察异常。
2. OperationCanceledException 只记录取消事件，不显示错误弹窗。
3. 新任务不得与旧任务同时发送密码或 Enter。
4. 取消令牌必须在轮询、输入前、Enter 前检查。
```

### 5.2 MainViewModel 生命周期

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
VSLoader/MainWindow.xaml.cs
```

新增或保留清晰的关闭入口，例如：

```csharp
public void ShutdownAdminUiAutomation();
```

`MainWindow.CleanupForClose()` 或真实退出流程必须调用该入口。

托盘隐藏主窗口不等于退出，不应取消正在等待的 AdminUI 自动登录任务。

## 6. MainViewModel 调整

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
```

### 6.1 自动登录开启时

调整为：

```text
1. 启动 AdminUI。
2. 读取密码。
3. 不执行主流程剪贴板写入和 15 次重试。
4. 调用 AdminUiAutoLoginCoordinator.Start(config, password, ...)。
5. 立即返回，不等待自动化任务结束。
```

状态提示：

```text
开始等待：AdminUI 已打开，正在等待登录窗口...
输入发送完成：AdminUI 登录信息已自动填写并确认。
失焦中止：登录窗口已失焦，已停止自动登录，请手动处理。
等待超时：未检测到前台 AdminUI 登录窗口，请手动处理。
输入失败：自动填写失败：{具体原因}
```

禁止继续使用“AdminUI 已自动登录”作为成功提示。

原因：

```text
SendInput 成功只能证明密码和 Enter 已发送，不能证明密码正确或服务端登录成功。
```

### 6.2 自动登录关闭时

继续使用 `ClipboardService.SetTextWithRetryAsync()`，作为用户手动粘贴的便利功能。

该剪贴板路径不进入 `AdminUiAutoLoginCoordinator`。

## 7. AdminUiAutoPasteService 重构

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

重构后职责：

```text
1. 在后台任务中轻量轮询当前前台窗口。
2. 严格判断当前窗口是否为目标 SunAwtDialog。
3. 匹配后调用 KeyboardInputService.SendTextAndEnterIfFocused(...)。
4. 返回成功、失焦、超时、取消或输入失败结果。
```

建议接口：

```csharp
public Task<AdminUiAutoPasteResult> TryAutoLoginAsync(
    AdminUiConfig config,
    string password,
    CancellationToken cancellationToken = default);
```

轮询策略：

```text
默认间隔：100ms。
默认超时：12秒。
每轮只调用一次 GetForegroundWindowInfo。
不写 Poll、WindowScanStart、WindowScanEnd 日志。
```

匹配成功后建议进行一次短稳定确认：

```text
等待 30ms。
再次读取当前前台窗口。
只有句柄仍与第一次匹配结果一致时才开始输入。
```

这 30ms 用于避免窗口刚创建、前台状态仍在切换时过早输入，不属于强制聚焦等待。

## 8. KeyboardInputService 重构

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

建议保留单一自动登录入口：

```csharp
public AdminUiAutoPasteResult SendTextAndEnterIfFocused(
    ForegroundWindowInfo targetWindow,
    string password,
    CancellationToken cancellationToken,
    AdminUiAutoPasteLogService? logService = null);
```

执行顺序：

```text
检查取消令牌
检查当前前台句柄 == targetWindow.Handle
Unicode SendInput 输入密码
等待 10ms
检查取消令牌
再次检查当前前台句柄 == targetWindow.Handle
仍一致：SendInput Enter
不一致：返回 FocusLost，不发送 Enter
```

必须删除：

```text
1. SendPasteAndEnter 自动登录路径。
2. SetForegroundWindow 调用。
3. EnsureTargetForeground。
4. EnsureTargetForegroundWhenNeeded。
5. ForceFocusRetryTimeoutMilliseconds。
6. ForceFocusRetryIntervalMilliseconds。
7. CriticalInputFocusMaxAttempts。
8. FocusSettleDelayMilliseconds 及相关 Thread.Sleep。
9. BlockInput P/Invoke 和执行逻辑。
10. CriticalInputOverlayService 自动登录兜底。
```

继续保留：

```text
1. KEYEVENTF_UNICODE 输入实现。
2. SendInput 返回数量检查。
3. Password 文本为空检查。
4. 密码正文不写日志。
5. Unicode 输入与 Enter 之间固定 10ms 间隔。
```

注意：

```text
不使用输入锁后，前台检查和 SendInput 之间仍存在极小的系统级竞态窗口。
本方案通过缩短输入序列、输入前检查、Enter 前检查和失焦立即终止降低风险。
不得为了消除该竞态重新引入 BlockInput 或强制抢焦点。
```

## 9. 结果模型

建议扩展 `AdminUiAutoPasteResult`，避免只用布尔值和拼接文本表达状态。

建议新增结果状态枚举：

```csharp
public enum AdminUiAutoLoginStatus
{
    InputSubmitted,
    FocusLostBeforeInput,
    FocusLostBeforeEnter,
    TimedOut,
    Canceled,
    PasswordEmpty,
    InputFailed
}
```

结果对象至少包含：

```text
Status
Message
MatchedWindow
```

`Success` 只表示输入序列已发送完成，不得解释为业务登录成功。

## 10. 日志重构

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteLogService.cs
```

日志改为事件型，只记录：

```text
[TaskStart] sessionId timeoutSeconds pollIntervalMs passwordLength
[DialogMatched] sessionId handle title process class elapsedMs
[StabilityCheck] sessionId expectedHandle actualHandle matched
[InputStart] sessionId handle textLength
[TextSent] sessionId requested sent elapsedMs
[FocusLost] sessionId stage expectedHandle actualHandle
[EnterSent] sessionId requested sent elapsedMs
[TaskCompleted] sessionId status elapsedMs
[TaskCancel] sessionId reason
[Timeout] sessionId elapsedMs
[Error] sessionId type message nativeErrorCode
```

必须删除或停止调用：

```text
[Poll]
[WindowScanStart]
[WindowScanEnd]
[WindowCandidate]
[FocusRetry]
[FocusRetryResult]
[InputBlock]
[InputProtection]
```

日志要求：

```text
1. 一个任务正常不超过 10 至 15 条日志。
2. 不记录每次 100ms 轮询。
3. 不记录密码正文，只记录 textLength。
4. 继续只写 adminui-autopaste.log。
5. 继续最多保留最新 2000 行。
6. 日志失败不得影响自动登录。
```

## 11. 配置清理与兼容

继续保留：

```text
AutoPastePasswordEnabled
AutoPasteTimeoutSeconds
AutoPasteWindowTitleKeyword
AutoPasteProcessNames
ProtectedPassword
```

删除：

```text
AutoPasteInitialDelayMilliseconds
AutoPastePollIntervalMilliseconds
```

原因：

```text
初始等待固定为 0。
前台检测间隔固定为代码内部 100ms。
这两个字段当前没有必要继续暴露或持久化，会让旧强控制方案残留在配置模型中。
```

兼容规则：

```text
System.Text.Json 默认忽略旧 config.json 中多余字段。
旧配置里的 AutoPasteInitialDelayMilliseconds 和 AutoPastePollIntervalMilliseconds 可以安全保留在旧文件中，加载时忽略。
下一次保存配置时自然移除这些字段。
不得因为旧字段存在而判定配置无效。
```

密码保存格式本次不修改，继续遵循当前明文配置要求，并兼容旧 DPAPI 密文读取。

## 12. 废弃代码清理

实现完成后检查引用并删除无用代码：

```text
VSLoader/Models/Services/TopLevelWindowService.cs
VSLoader/Models/Services/CriticalInputOverlayService.cs
VSLoader/Models/Services/AdminUiAutoInputMode.cs
```

删除条件：

```text
只有确认这些类型不再被其它业务功能引用时才删除文件。
如果仍有非 AdminUI 业务引用，则保留类型，但必须解除 AdminUI 自动登录链路引用。
```

同时清理：

```text
1. KeyboardInputService 中旧 Ctrl+V 自动登录接口。
2. 强制焦点和输入保护相关 P/Invoke。
3. 旧日志方法。
4. 只验证旧强控制行为的测试。
5. MainViewModel 中自动登录开启时的剪贴板优先分支。
6. 已无意义的注释、兼容构造函数和测试注入重载。
```

## 13. 边界情况

### 13.1 AdminUI 启动较慢

```text
后台任务持续检查当前前台窗口。
最长等待 AutoPasteTimeoutSeconds，默认 12 秒。
等待期间不枚举后台窗口、不写轮询日志、不阻塞 UI。
```

### 13.2 用户在 AdminUI 启动过程中操作其它软件

```text
继续等待，不抢焦点。
如果 SunAwtDialog 后续自然成为前台，则可以执行自动输入。
如果 SunAwtDialog 始终在后台，则最终超时。
```

### 13.3 用户在密码输入前切走

```text
前台句柄检查失败。
立即结束任务。
不输入密码，不拉回窗口。
```

### 13.4 用户在密码输入后、Enter 前切走

```text
不发送 Enter。
提示登录窗口已失焦，请用户手动确认。
不拉回窗口。
```

### 13.5 连续点击多个 AdminUI

```text
新任务取消旧任务。
SemaphoreSlim 保证输入序列不重叠。
旧任务已输入部分密码时无法撤销，但必须在 Enter 前响应取消并停止。
```

### 13.6 同时存在多个 SunAwtDialog

```text
只处理当前前台的 SunAwtDialog。
不枚举后台窗口，因此不存在随机选择后台旧登录框的问题。
```

### 13.7 剪贴板被占用

```text
自动登录开启时不依赖剪贴板，不受影响。
自动登录关闭时保留现有剪贴板失败提示。
```

### 13.8 程序退出

```text
显式取消后台任务。
由于新方案没有 BlockInput 和 Overlay，不存在遗留输入锁或遮罩风险。
```

## 14. 性能约束

实现后必须满足：

```text
1. 等待阶段每秒最多约 10 次前台窗口读取。
2. 等待阶段不得调用 EnumWindows。
3. 等待阶段不得遍历所有进程。
4. 等待阶段不得写轮询日志。
5. 不得在 WPF UI 线程执行 Thread.Sleep。
6. 不得创建全屏透明窗口。
7. 不得调用 BlockInput。
8. 不得强制切换前台窗口。
9. AdminUI 等待期间主界面搜索、滚动、地图交互应保持正常响应。
```

## 15. 测试要求

建议重构或新增：

```text
VSLoader.Tests/AdminUiAutoLoginCoordinatorTests.cs
VSLoader.Tests/AdminUiAutoPasteServiceTests.cs
VSLoader.Tests/KeyboardInputServiceTests.cs
VSLoader.Tests/MainViewModelAdminUiAutoPasteSourceTests.cs
VSLoader.Tests/AdminUiAutoPasteLogServiceTests.cs
VSLoader.Tests/ConfigServiceAdminUiAutoPasteTests.cs
```

必须覆盖：

```text
1. 自动登录开启时不调用 ClipboardService。
2. 自动登录关闭时仍复制密码到剪贴板。
3. 自动登录任务不会在调用线程的 WPF SynchronizationContext 上执行核心轮询。
4. 每轮只检查当前前台窗口。
5. 后台 SunAwtDialog 不触发输入。
6. 前台 SunAwtDialog 严格匹配后触发输入。
7. SunAwtFrame 不触发输入。
8. 标题、进程或类名任一不匹配时不触发输入。
9. 输入前失焦时不发送密码。
10. 密码输入后失焦时不发送 Enter。
11. 整个流程不调用 SetForegroundWindow。
12. 整个流程不调用 BlockInput。
13. 整个流程不创建 Overlay。
14. Unicode 输入后只等待 10ms。
15. 新任务取消旧任务且不会出现两个输入序列。
16. 程序退出时取消等待任务。
17. 日志不包含 Poll 或 WindowScan 高频记录。
18. 日志不包含密码正文。
19. 日志仍只保留最新 2000 行。
20. 旧配置包含已删除字段时仍能正常加载。
```

## 16. 人工验证

### 16.1 正常自动登录

```text
1. 启用自动登录并配置密码。
2. 点击 AdminUI。
3. 不操作其它窗口。
4. 确认 SunAwtDialog 成为前台后快速输入密码并发送 Enter。
5. 确认 VSLoader 和地图在等待阶段保持流畅。
```

### 16.2 输入前失焦

```text
1. 点击 AdminUI。
2. SunAwtDialog 出现时立即切到其它程序。
3. 确认程序不把 AdminUI 拉回。
4. 确认密码没有输入到其它程序。
5. 确认状态提示本轮自动登录已中止。
```

### 16.3 Enter 前失焦

```text
1. 在密码输入完成后的短窗口内切走焦点。
2. 确认程序不向其它程序发送 Enter。
3. 确认 AdminUI 密码框可能已有密码，但需用户手动确认。
```

### 16.4 后台登录框

```text
1. 让 SunAwtDialog 被其它程序遮挡并保持后台。
2. 确认程序不抢焦点、不自动输入。
3. 用户手动切回 SunAwtDialog 后，如果任务仍未超时，可以继续自动输入。
```

### 16.5 性能验证

```text
1. 启动 AdminUI 后让登录框延迟 10 秒出现。
2. 等待期间持续滚动主界面、搜索和操作地图。
3. 确认没有明显卡顿、掉帧或整机输入冻结。
4. 检查日志，确认没有每 100ms 产生扫描记录。
```

## 17. 验证命令

实现阶段每一步完成后执行目标测试，最终执行：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~AdminUiAutoPaste|FullyQualifiedName~AdminUiAutoLogin|FullyQualifiedName~KeyboardInput|FullyQualifiedName~ClipboardService|FullyQualifiedName~ConfigServiceAdminUiAutoPaste|FullyQualifiedName~MainViewModelAdminUiAutoPaste"
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore
dotnet build .\VSLoader.sln -c Debug --no-restore
```

如果出现文件占用，先确认 VSLoader、测试进程和构建进程已退出，再重新构建。不得通过删除用户配置或清空源码改动解决构建占用。

## 18. 非目标

本次不做：

```text
1. 不修改 AdminUI JNLP 下载和路径解析规则。
2. 不判断服务端是否真正登录成功。
3. 不读取 Java 密码输入框内容。
4. 不自动寻找或激活后台 SunAwtDialog。
5. 不恢复强制抢焦点能力。
6. 不恢复 BlockInput 或全屏遮罩。
7. 不新增用户可配置的扫描间隔。
8. 不改变 AdminUI 密码当前的保存格式。
9. 不修改 WebUI、VSCode 或地图打开逻辑。
```

## 19. 验收标准

全部满足才算完成：

```text
1. 自动登录等待阶段不再使用 EnumWindows。
2. 自动登录核心任务不在 WPF UI 线程执行。
3. 自动登录开启时不再依赖剪贴板。
4. 只对当前前台且严格匹配的 SunAwtDialog 输入密码。
5. 失焦后立即中止，不抢焦点、不重试。
6. 密码输入后失焦时不发送 Enter。
7. 不再使用 SetForegroundWindow、BlockInput 和 Overlay。
8. 不再写高频轮询日志。
9. 新旧任务不会并发输入。
10. 程序退出时自动化任务被显式取消。
11. 自动输入完成提示不再宣称已经业务登录成功。
12. 旧配置可以平滑加载。
13. AdminUI 相关测试全部通过。
14. 全量测试通过。
15. Debug 构建 0 错误。
16. 人工验证等待期间无明显卡顿或系统输入冻结。
```
