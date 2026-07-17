# v156 AdminUI 自动登录失败剪贴板兜底编码规格说明

## 1. 背景

v155 已将 AdminUI 自动登录重构为轻量前台检测方案：

```text
自动登录开启
等待前台 SunAwtDialog
Unicode SendInput 直接输入密码
确认焦点仍在目标窗口
发送 Enter
```

该方案已经移除：

```text
EnumWindows 全窗口枚举
SetForegroundWindow 强制抢焦点
BlockInput
全屏透明 Overlay
Ctrl+V 自动登录路径
高频轮询日志
```

当前缺口是：

```text
自动登录开启时不再提前写入剪贴板。
如果 Unicode 输入、焦点检查、等待登录框或 Enter 发送失败，程序只提示“请手动处理”。
此时剪贴板里没有密码，用户无法直接手动粘贴。
```

本次需求收束为：

```text
自动登录开启：Unicode 直接输入优先，失败后写入剪贴板兜底。
自动登录关闭：继续直接写入剪贴板，不执行自动化。
```

## 2. 目标行为

### 2.1 自动登录开启

```text
启动 AdminUI
读取密码
启动轻量后台自动登录任务
等待前台 SunAwtDialog
Unicode 输入密码
确认焦点
发送 Enter
```

自动登录成功：

```text
提示：AdminUI 登录信息已自动填写并确认。
不写入剪贴板。
```

自动登录失败：

```text
回到 WPF UI 线程
调用 ClipboardService.SetTextWithRetryAsync(password)
写入成功：提示自动登录失败，密码已复制到剪贴板，请手动粘贴
写入失败：展示自动登录失败原因和剪贴板写入失败原因
```

### 2.2 自动登录关闭

行为保持不变：

```text
启动 AdminUI
读取密码
不扫描登录窗口
不执行 Unicode 输入
不发送 Enter
直接写入剪贴板
```

写入成功：

```text
提示：AdminUI 已打开，密码已复制到剪贴板。
```

写入失败：

```text
弹出：AdminUI 已打开，但写入剪贴板失败：{详细原因}
```

## 3. 完整逻辑链路

```text
用户点击 AdminUI
    ↓
启动对应 JNLP
    ↓
读取当前工作区密码
    ↓
密码为空？
    ├─ 是：提示未配置密码，结束
    └─ 否：判断 AutoPastePasswordEnabled
              ↓
        ┌─────┴─────┐
        │           │
      开启         关闭
        │           │
        │           └→ 直接写入剪贴板
        │               ├─ 成功：提示已复制
        │               └─ 失败：弹出详细错误
        ↓
   启动后台自动登录
        ↓
   Unicode 输入与 Enter
        ↓
   自动登录结果
        ├─ InputSubmitted
        │      └→ 提示输入已发送，不写剪贴板
        │
        └─ 其它失败结果
               ↓
          切回 WPF UI 线程
               ↓
          写入剪贴板
               ├─ 成功：提示密码已复制，请手动粘贴
               └─ 失败：显示两部分失败原因
```

## 4. 自动登录失败范围

以下结果都必须触发剪贴板兜底：

```text
AdminUiAutoLoginStatus.FocusLostBeforeInput
AdminUiAutoLoginStatus.FocusLostBeforeEnter
AdminUiAutoLoginStatus.TimedOut
AdminUiAutoLoginStatus.PasswordEmpty（仅防御性处理）
AdminUiAutoLoginStatus.InputFailed
协调器 onError 回调收到的未预期异常
```

以下结果不得写入剪贴板：

```text
AdminUiAutoLoginStatus.InputSubmitted
任务被新一轮 AdminUI 启动取消
程序退出导致的任务取消
```

原因：

```text
取消旧任务通常意味着用户已启动新任务。
如果旧任务取消后仍写入剪贴板，可能用旧快捷项密码覆盖新任务状态。
程序退出时也不应继续进行异步剪贴板操作。
```

## 5. MainViewModel 调整

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
```

当前自动登录回调只负责显示结果：

```csharp
pasteResult => ShowAdminUiAutoPasteResult(pasteResult)
```

需要把本轮密码安全地传给结果处理入口：

```csharp
pasteResult => HandleAdminUiAutoLoginResultAsync(pasteResult, password)
```

异常入口同样需要密码：

```csharp
exception => HandleAdminUiAutoLoginErrorAsync(exception, password)
```

建议新增：

```csharp
private Task HandleAdminUiAutoLoginResultAsync(
    AdminUiAutoPasteResult result,
    string password);

private Task HandleAdminUiAutoLoginErrorAsync(
    Exception exception,
    string password);

private Task CopyAdminUiPasswordFallbackAsync(
    string password,
    string automationFailureMessage);
```

接口名称可以按现有项目风格调整，但必须保持职责清晰：

```text
结果处理负责判断是否需要兜底。
剪贴板方法只负责切换线程、写入剪贴板和组合提示。
```

## 6. WPF 线程约束

`AdminUiAutoLoginCoordinator` 当前通过 `Task.Run` 执行自动化，并在后台线程触发完成或异常回调。

`System.Windows.Clipboard` 依赖 STA/WPF UI 线程，因此禁止在协调器后台线程直接调用：

```csharp
_clipboardService.SetTextWithRetryAsync(password)
```

必须通过 `Application.Current.Dispatcher` 回到 UI 线程后再开始剪贴板操作。

建议模式：

```csharp
private void BeginAdminUiClipboardFallback(...)
{
    var dispatcher = Application.Current?.Dispatcher;
    if (dispatcher is null)
    {
        // 返回可追溯失败提示或日志，不能静默丢失。
        return;
    }

    _ = dispatcher.InvokeAsync(async () =>
    {
        await CopyAdminUiPasswordFallbackAsync(...);
    });
}
```

实现时注意：

```text
1. 不允许 async void 传播未处理异常。
2. Dispatcher 回调内部必须 try/catch。
3. 剪贴板失败不得导致 UI 线程崩溃。
4. 程序正在退出时不启动新的剪贴板兜底。
5. 本轮密码不得写入日志。
```

## 7. 剪贴板服务复用

文件：

```text
VSLoader/Models/Services/ClipboardService.cs
```

继续复用现有接口：

```csharp
SetTextWithRetryAsync(
    string text,
    int maxAttempts = 15,
    int delayMilliseconds = 120)
```

本次不新增第二套剪贴板实现。

现有行为：

```text
最多尝试 15 次。
失败间隔 120ms。
成功后内容持久写入剪贴板。
最终失败信息包含尝试次数、HResult 和异常消息。
```

自动登录失败后允许沿用完整重试策略，因为此时自动化已经结束，不影响 Unicode 正常成功路径的速度。

## 8. 并发与过期结果控制

必须防止旧任务失败结果覆盖新任务状态或剪贴板。

建议由协调器或 MainViewModel 返回/维护 `sessionId`，结果回调执行前确认它仍是当前会话。

最低要求：

```text
1. 新一轮 AdminUI 启动会取消旧任务。
2. 被取消的旧任务不触发完成或错误回调。
3. 只有当前有效任务的失败结果可以写入剪贴板。
4. 同一任务最多执行一次剪贴板兜底。
5. 连续回调或重复异常不得重复写入剪贴板。
```

如果当前协调器已经保证取消任务不会调用回调，可以复用该约束，但必须通过测试锁定。

## 9. 提示语义

### 9.1 自动输入成功

```text
AdminUI 登录信息已自动填写并确认。
```

不得写成：

```text
AdminUI 已登录成功。
```

因为程序没有验证服务端登录结果。

### 9.2 失焦后剪贴板兜底成功

输入前失焦：

```text
登录窗口已失焦，已停止自动登录。密码已复制到剪贴板，请手动粘贴。
```

Enter 前失焦：

```text
密码可能已填写，但登录窗口已失焦，未发送确认键。密码已复制到剪贴板，请手动处理。
```

### 9.3 超时后剪贴板兜底成功

```text
未检测到前台 AdminUI 登录窗口。密码已复制到剪贴板，请手动粘贴。
```

### 9.4 Unicode 输入失败后剪贴板兜底成功

```text
自动填写失败：{原因}。密码已复制到剪贴板，请手动粘贴。
```

### 9.5 自动化与剪贴板均失败

必须同时展示两个原因：

```text
自动登录失败：{自动化失败原因}
密码写入剪贴板也失败：{剪贴板失败原因}
请手动输入密码。
```

不得只展示最后一次剪贴板错误并丢失最初的自动化失败原因。

## 10. 日志要求

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteLogService.cs
```

建议新增事件型日志：

```text
[ClipboardFallbackStart] sessionId reasonStatus textLength
[ClipboardFallbackCompleted] sessionId success=True attempts=...
[ClipboardFallbackFailed] sessionId success=False hResult=... message=...
```

日志约束：

```text
1. 不记录密码正文。
2. 只允许记录密码长度。
3. 不恢复 Poll 或 WindowScan 高频日志。
4. 继续只写 adminui-autopaste.log。
5. 继续保留最新 2000 行。
6. 日志写入失败不得影响剪贴板兜底。
```

如果不修改 `ClipboardService` 返回模型，日志中的 attempts 可以使用当前默认值 15；不得为了日志重新执行剪贴板操作。

## 11. 退出与取消边界

程序退出时：

```text
MainWindow 调用 ShutdownAdminUiAutomation。
协调器取消当前任务。
被取消任务不触发剪贴板兜底。
已经排队但尚未执行的 Dispatcher 兜底必须检查退出状态。
不得在 Application.Shutdown 后访问 Clipboard。
```

用户连续打开多个 AdminUI 时：

```text
旧任务取消。
旧任务不写剪贴板。
新任务独立执行 Unicode 自动登录。
只有新任务自身失败时才允许写入剪贴板。
```

## 12. 非目标

本次不做：

```text
1. 不恢复自动登录开始前的剪贴板写入。
2. 不恢复 Ctrl+V 自动登录。
3. 不恢复 EnumWindows。
4. 不恢复 SetForegroundWindow。
5. 不恢复 BlockInput 或 Overlay。
6. 不改变 Unicode 输入和 10ms Enter 等待。
7. 不改变 SunAwtDialog 严格匹配规则。
8. 不判断服务端是否真正登录成功。
9. 不修改 AdminUI 密码保存格式。
10. 不新增用户可配置的剪贴板重试参数。
```

## 13. 测试要求

建议更新或新增：

```text
VSLoader.Tests/MainViewModelAdminUiAutoPasteSourceTests.cs
VSLoader.Tests/AdminUiAutoLoginCoordinatorTests.cs
VSLoader.Tests/ClipboardServiceTests.cs
VSLoader.Tests/AdminUiAutoPasteLogServiceTests.cs
```

必须覆盖：

```text
1. 自动登录开启时，正常路径不提前写剪贴板。
2. InputSubmitted 不触发剪贴板兜底。
3. FocusLostBeforeInput 触发一次剪贴板兜底。
4. FocusLostBeforeEnter 触发一次剪贴板兜底。
5. TimedOut 触发一次剪贴板兜底。
6. InputFailed 触发一次剪贴板兜底。
7. 协调器 onError 触发一次剪贴板兜底。
8. 被取消任务不触发剪贴板兜底。
9. 程序退出后不触发剪贴板兜底。
10. 剪贴板操作在 WPF Dispatcher/UI 线程执行。
11. 剪贴板兜底成功时提示包含原始自动化失败原因。
12. 剪贴板兜底失败时提示同时包含两种失败原因。
13. 自动登录关闭时仍直接写剪贴板。
14. 自动登录关闭且剪贴板失败时仍展示详细错误。
15. 密码正文不进入日志。
16. 同一任务不会重复执行剪贴板兜底。
```

## 14. 验证命令

实现完成后执行：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~AdminUiAutoPaste|FullyQualifiedName~AdminUiAutoLogin|FullyQualifiedName~ClipboardService|FullyQualifiedName~MainViewModelAdminUiAutoPaste"
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore
dotnet build .\VSLoader.sln -c Debug --no-restore
```

## 15. 人工验证

### 15.1 Unicode 自动登录成功

```text
启用自动登录。
打开 AdminUI。
确认密码通过 Unicode 输入并发送 Enter。
确认剪贴板没有被本次成功任务覆盖。
```

### 15.2 输入前失焦

```text
启用自动登录。
SunAwtDialog 出现后切到其它程序。
确认程序不抢回焦点。
确认自动化中止后密码写入剪贴板。
确认可以手动粘贴密码。
```

### 15.3 Enter 前失焦

```text
密码输入后立即切走焦点。
确认程序不发送 Enter。
确认密码随后写入剪贴板。
```

### 15.4 等待超时

```text
让前台始终不出现符合条件的 SunAwtDialog。
等待超时后确认密码写入剪贴板。
```

### 15.5 自动登录关闭

```text
关闭自动登录。
打开 AdminUI。
确认程序不扫描、不输入、不发送 Enter。
确认密码直接写入剪贴板。
```

## 16. 验收标准

全部满足才算完成：

```text
1. Unicode 自动登录仍是开启自动登录时的唯一正常路径。
2. 正常成功路径不写剪贴板。
3. 所有有效失败结果都触发一次剪贴板兜底。
4. 取消和程序退出不触发剪贴板兜底。
5. 剪贴板操作回到 WPF UI 线程执行。
6. 自动登录关闭时继续直接写剪贴板。
7. 剪贴板失败信息可追溯。
8. 自动化失败原因不会被剪贴板错误覆盖。
9. 不恢复任何高开销强控制逻辑。
10. 日志不包含密码正文且最多保留 2000 行。
11. 相关目标测试通过。
12. 全量测试通过。
13. Debug 构建 0 错误、0 警告。
```
