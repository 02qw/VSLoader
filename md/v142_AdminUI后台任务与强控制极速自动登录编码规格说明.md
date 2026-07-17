# v142 AdminUI 后台任务与强控制极速自动登录编码规格说明

## 1. 背景

当前 AdminUI 自动登录已经具备以下能力：

```text
打开 AdminUI
写入密码到剪贴板
扫描顶层窗口
找到严格匹配的 SunAwtDialog
强制拉回目标窗口
短暂输入保护
Ctrl+V
Enter
```

用户实测后反馈：

```text
SunAwtDialog 出现后，到真正粘贴密码并回车之间仍然有明显等待。
这段等待体感接近 2 秒，自动登录不够“立刻接管”。
```

结合日志和现有代码分析后确认：

```text
1. 初始等待已经是 0ms。
2. 登录窗口扫描间隔默认是 50ms。
3. 主要耗时来自等待 SunAwtDialog 真正被枚举识别，以及匹配后的多次焦点确认/等待。
4. 当前 OpenAdminUiAsync 会 await 自动粘贴流程，主流程体验也被自动化任务拖住。
```

本次需求收束为：

```text
打开 AdminUI 后，自动登录任务交给后台任务等待 SunAwtDialog。
主界面不阻塞等待自动登录完成。
一旦后台任务匹配到 SunAwtDialog，立刻进入强控制关键输入阶段。
关键输入阶段必须强控制，用户不可操作。
Ctrl+V 后只等待 10ms，然后立即 Enter。
```

## 2. 当前相关代码

### 2.1 主流程

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
```

当前相关方法：

```csharp
private async Task OpenAdminUiAsync()
```

当前行为：

```text
打开 AdminUI
读取 AdminUI 密码
写入剪贴板
await _adminUiAutoPasteService.TryPasteAsync(adminUiConfig)
根据 TryPasteAsync 结果显示状态提示
```

当前问题：

```text
OpenAdminUiAsync 会等待整个自动登录流程结束。
即使扫描过程是异步 Task.Delay，用户体验上仍然像“打开 AdminUI 后 VSLoader 在等自动登录完成”。
```

### 2.2 自动登录等待服务

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

当前行为：

```text
根据配置轮询顶层窗口
只接受严格匹配 SunAwtDialog 的目标窗口
匹配后调用 KeyboardInputService.SendPasteAndEnter(...)
```

需要保留：

```text
只允许严格匹配 SunAwtDialog。
不允许回退到普通 SunAwtFrame 或其它前台窗口执行 Ctrl+V/Enter。
```

### 2.3 键盘输入服务

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

当前关键常量：

```csharp
internal const int FocusSettleDelayMilliseconds = 80;
internal const int PasteBeforeEnterDelayMilliseconds = 80;
internal const int ForceFocusRetryTimeoutMilliseconds = 1500;
internal const int ForceFocusRetryIntervalMilliseconds = 40;
internal const int CriticalInputFocusMaxAttempts = 3;
```

当前关键行为：

```text
粘贴前完整确认焦点
进入输入保护后再次完整确认焦点
Ctrl+V
等待 80ms
Enter 前再次完整确认焦点
Enter
```

当前问题：

```text
强控制阶段内存在重复焦点确认和重复等待。
在已经有 BlockInput/Overlay 保护的前提下，正常成功路径可以更激进。
```

## 3. 目标行为

### 3.1 用户视角

用户点击 AdminUI 后：

```text
1. VSLoader 打开 AdminUI。
2. VSLoader 写入密码到剪贴板。
3. VSLoader 立即恢复可操作，不等待自动登录任务结束。
4. 状态栏提示：AdminUI 已打开，密码已复制，正在后台等待登录窗口。
5. 后台任务检测到 SunAwtDialog 后，短暂强制接管输入。
6. 自动粘贴密码并回车。
7. 状态栏提示：AdminUI 已自动登录。
```

### 3.2 关键输入阶段

匹配到 SunAwtDialog 后必须进入强控制：

```text
匹配 SunAwtDialog
进入关键输入阶段
启用 BlockInput 或 Overlay 保护
强制 SetForegroundWindow(SunAwtDialog)
确认当前前台窗口是目标 SunAwtDialog
SendInput Ctrl+V
等待 10ms
SendInput Enter
退出关键输入阶段
释放输入保护
```

关键原则：

```text
Ctrl+V 和 Enter 只能发给已确认的目标 SunAwtDialog。
强控制失败或焦点无法确认时，宁可不自动输入，也不能把密码发到其它软件。
```

## 4. 设计方案

### 4.1 新增后台任务协调器

建议新增服务：

```text
VSLoader/Models/Services/AdminUiAutoLoginCoordinator.cs
```

职责：

```text
1. 启动 AdminUI 自动登录后台任务。
2. 取消上一轮仍在等待 SunAwtDialog 的任务。
3. 保证同一时间只有一个任务进入关键输入阶段。
4. 记录 taskId/sessionId，便于日志追踪。
5. 将任务成功/失败/超时结果回调给 MainViewModel 更新状态栏。
```

建议接口：

```csharp
public sealed class AdminUiAutoLoginCoordinator
{
    public void Start(
        AdminUiConfig config,
        Action<AdminUiAutoPasteResult> onCompleted,
        Action<Exception> onError);

    public void CancelWaitingTask();

    public void Shutdown();
}
```

说明：

```text
具体接口可以按项目现有风格微调。
核心是 MainViewModel 不再直接 await TryPasteAsync。
```

### 4.2 并发与取消规则

规则：

```text
1. 等待 SunAwtDialog 阶段可以取消。
2. 已进入关键输入阶段后不取消。
3. 用户连续点击 AdminUI：
   - 取消上一轮仍在等待的任务。
   - 启动新任务。
4. 如果上一轮已经进入关键输入阶段：
   - 新任务等待或直接放弃，由实现选择更简单稳定的策略。
   - 推荐：关键输入阶段持有 SemaphoreSlim，新任务不能打断它。
5. VSLoader 退出：
   - 取消等待中的任务。
   - 关键输入阶段尽快完成 finally 释放保护。
```

### 4.3 MainViewModel 调整

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
```

调整前：

```csharp
var pasteResult = await _adminUiAutoPasteService.TryPasteAsync(adminUiConfig);
```

调整后：

```text
写入剪贴板成功后：
1. 如果自动粘贴关闭，只提示密码已复制。
2. 如果自动粘贴开启，调用 coordinator.Start(...)。
3. 立即返回，不 await 自动登录完成。
4. 后台任务完成后通过 UI Dispatcher 更新状态栏。
```

状态提示：

```text
启动后台任务：AdminUI 已打开，密码已复制，正在后台等待登录窗口...
成功：AdminUI 已自动登录。
超时：未检测到 AdminUI 登录窗口，请手动粘贴。
失败：自动登录失败，请手动粘贴。
取消旧任务：只写日志，不弹窗。
```

### 4.4 关键输入阶段时间优化

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

建议常量调整：

```csharp
internal const int FocusSettleDelayMilliseconds = 35;
internal const int PasteBeforeEnterDelayMilliseconds = 10;
```

输入保护内重复焦点确认优化：

```text
当前：
保护前确认一次焦点。
进入保护后再完整确认一次焦点。

调整：
保护前可以保留一次快速确认。
进入保护后执行一次强制 SetForegroundWindow + 短确认。
如果刚刚确认过同一目标句柄，且进入保护后前台仍是该句柄，则不重复完整等待。
```

Enter 前确认优化：

```text
当前：
Ctrl+V 后等待 80ms。
Enter 前完整 EnsureTargetForeground。

调整：
Ctrl+V 后等待 10ms。
Enter 前只做轻量前台句柄检查：
  - 如果当前前台仍是目标 SunAwtDialog，立即 Enter。
  - 如果不是，再尝试 SetForegroundWindow + 35ms 短确认。
  - 仍失败则不发送 Enter，并返回失败提示“密码可能已粘贴，请手动确认”。
```

注意：

```text
不允许在没有确认目标窗口前发送 Ctrl+V 或 Enter。
```

### 4.5 自动登录服务职责保持清晰

`AdminUiAutoPasteService` 仍负责：

```text
扫描 SunAwtDialog
匹配窗口
调用键盘输入服务
返回结果
```

`AdminUiAutoLoginCoordinator` 负责：

```text
后台任务生命周期
取消旧任务
串行化关键输入阶段
回调 UI 状态
日志 sessionId
```

不要把后台任务生命周期塞进 `KeyboardInputService`。

## 5. 日志要求

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteLogService.cs
```

需要补充或保留以下日志：

```text
[TaskStart] sessionId=... timeoutSeconds=... pollIntervalMs=...
[TaskCancel] sessionId=... reason="NewAdminUiLaunch"
[TaskEnterCritical] sessionId=... targetHandle=...
[TaskExitCritical] sessionId=... success=True/False elapsedMs=...
[WindowMatch] sessionId=... elapsedFromTaskStartMs=...
[KeyboardPlan] focusSettleMs=35 pasteBeforeEnterDelayMs=10
[KeyboardDelay] reason="AfterPasteBeforeEnter" delayMs=10
[TaskCompleted] sessionId=... success=True/False message="..."
```

日志文件仍遵循现有上限策略：

```text
只写 adminui-autopaste.log
最多保留最新 2000 条
```

## 6. 配置兼容

现有配置继续保留：

```text
AutoPastePasswordEnabled
AutoPasteTimeoutSeconds
AutoPasteInitialDelayMilliseconds
AutoPastePollIntervalMilliseconds
AutoPasteWindowTitleKeyword
AutoPasteProcessNames
```

本次不新增 UI 配置项。

`PasteBeforeEnterDelayMilliseconds = 10` 暂时作为代码常量，不暴露给用户设置。

原因：

```text
该值过小可能增加 Java AWT 空白回车风险。
该值过大又影响自动登录速度。
先按明确需求固定为 10ms，避免用户误调。
```

## 7. 边界情况

### 7.1 未检测到 SunAwtDialog

行为：

```text
后台任务超时结束。
状态栏提示请手动粘贴。
不弹阻塞窗口。
不影响 VSLoader 使用。
```

### 7.2 连续打开多个 AdminUI

行为：

```text
新任务启动时取消旧的等待任务。
旧任务如果已经进入关键输入阶段，不强行中断。
同一时刻只能有一个任务执行 Ctrl+V/Enter。
```

### 7.3 BlockInput 失败

行为：

```text
继续使用 Overlay 兜底。
Overlay 成功才允许继续输入。
BlockInput 和 Overlay 都失败，则不发送 Ctrl+V/Enter。
```

### 7.4 焦点无法拉回 SunAwtDialog

行为：

```text
不发送 Ctrl+V/Enter。
返回失败。
状态栏提示自动登录失败，请手动粘贴。
日志记录 SetForegroundWindow 和 FocusCheck 结果。
```

### 7.5 Ctrl+V 成功但 Enter 前焦点丢失

行为：

```text
尝试短拉回目标窗口。
拉回成功则 Enter。
拉回失败则不 Enter，提示“密码可能已粘贴，请手动确认”。
```

### 7.6 VSLoader 退出

行为：

```text
取消等待中的后台任务。
关键输入阶段必须 finally 释放 BlockInput/Overlay。
不得遗留输入锁或遮罩窗口。
```

## 8. 测试要求

建议新增或更新测试：

```text
VSLoader.Tests/AdminUiAutoLoginCoordinatorTests.cs
VSLoader.Tests/AdminUiAutoPasteServiceTests.cs
VSLoader.Tests/KeyboardInputServiceTests.cs
VSLoader.Tests/MainViewModelAdminUiAutoPasteSourceTests.cs
```

必须覆盖：

```text
1. OpenAdminUiAsync 写入剪贴板成功后启动后台任务，不 await TryPasteAsync。
2. 连续启动时取消上一轮等待任务。
3. 已进入关键输入阶段时不会被新任务打断。
4. KeyboardInputService 使用 10ms PasteBeforeEnterDelay。
5. Enter 前当前前台仍是目标窗口时，不执行完整 80ms 等待。
6. Enter 前前台不是目标窗口时，会短拉回，失败则不发送 Enter。
7. BlockInput 失败但 Overlay 成功时继续执行。
8. BlockInput 和 Overlay 都失败时不发送 Ctrl+V/Enter。
9. 超时未找到 SunAwtDialog 时只返回失败结果，不抛未处理异常。
10. 日志包含 sessionId/taskId，能区分多轮任务。
```

## 9. 验证命令

实现完成后至少执行：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~AdminUiAutoPaste|FullyQualifiedName~KeyboardInput|FullyQualifiedName~MainViewModelAdminUiAutoPaste"
dotnet build .\VSLoader.sln -c Debug --no-restore
```

如果出现文件占用或编译服务缓存问题，先执行：

```powershell
dotnet build-server shutdown
```

然后重新运行测试和构建。

## 10. 非目标

本次不做：

```text
1. 不改变 AdminUI JNLP 拼接策略。
2. 不改变 AdminUI 密码保存格式。
3. 不新增自动登录延迟 UI 配置。
4. 不取消 SunAwtDialog 严格匹配。
5. 不允许向普通 SunAwtFrame 或其它前台窗口发送密码。
6. 不使用纯后台窗口句柄发送密码；SendInput 仍必须基于前台焦点安全确认。
```

