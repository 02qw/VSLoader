# v129 AdminUI 自动粘贴分阶段焦点保护编码规格说明

## 1. 背景

v127 到 v128 已经实现并逐步修正 AdminUI 自动粘贴逻辑：

```text
打开 AdminUI -> 写入剪贴板 -> 等待 Java 登录窗口 -> 发送 Ctrl+V -> 等待 -> 发送 Enter
```

经过日志排查，当前已经明确几个事实：

```text
1. 剪贴板写入是正确的。
2. SendInput 能成功发送 Ctrl+V 和 Enter。
3. AdminUI 登录相关窗口标题通常是 xxx.processor。
4. Java 主框架窗口 className 是 SunAwtFrame。
5. Java 登录/失败提示等对话框 className 是 SunAwtDialog。
```

上一轮修复已经将 `SunAwtFrame` 排除，避免密码粘贴到主框架窗口。

但现在还需要进一步补强一个状态边界：

```text
粘贴前必须严格防护，避免密码粘错窗口。
一旦密码已经成功粘贴到 SunAwtDialog，后续 Enter 应继续完成登录。
```

用户指出：

```text
如果在 SunAwtDialog 中密码粘贴成功了，此时即使用户焦点切走，也应该正常执行 Enter。
防护逻辑只应在密码还没成功输入到 SunAwtDialog 前起作用。
```

这个判断是合理的。当前代码把 `Ctrl+V + Enter` 放在一个整体方法里，缺少明确的阶段状态，因此无法精确表达：

```text
粘贴前防护
粘贴成功后继续提交
```

## 2. 当前相关代码

### 2.1 自动粘贴等待服务

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

当前主要流程：

```csharp
var window = getForegroundWindowInfo();
var match = EvaluateAdminUiWindow(window, config);
if (match.IsMatch)
{
    sendPasteAndEnter(window!);
    return AdminUiAutoPasteResult.Ok(window!);
}
```

当前匹配逻辑已经包含：

```text
标题匹配
进程匹配
className 匹配
```

其中 `SunAwtFrame` 会被排除，避免把 Java 主框架当成登录目标。

### 2.2 键盘输入服务

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

当前主要流程：

```csharp
SetForegroundWindow(targetWindow.Handle);
Thread.Sleep(FocusSettleDelay);

SendInput(Ctrl+V);

Thread.Sleep(PasteBeforeEnterDelay);

SendInput(Enter);
```

当前不足：

```text
1. 没有显式区分 BeforePaste / PasteSent / BeforeEnter 阶段。
2. Ctrl+V 前没有强校验当前前台窗口是否仍是目标 SunAwtDialog。
3. Ctrl+V 成功后没有标记“粘贴阶段已完成”。
4. Enter 前没有基于“粘贴已成功发送给目标窗口”决定是否继续。
5. 如果用户焦点切走，当前逻辑缺少明确策略：什么时候中断，什么时候重新聚焦并继续。
```

## 3. 核心问题

自动粘贴不是一个单动作，而是一个有状态流程。

必须区分以下阶段：

```text
阶段 1：等待目标登录对话框
阶段 2：粘贴前确认
阶段 3：发送 Ctrl+V
阶段 4：粘贴后等待
阶段 5：发送 Enter
阶段 6：完成
```

每个阶段的安全规则不同：

```text
粘贴前：必须严格确认目标窗口，用户切走则中断或继续等待，绝不粘贴到其它窗口。
粘贴后：密码已经进入目标登录框，应优先完成 Enter，避免半截状态。
```

因此不能用同一套“焦点切走就全部中断”的规则处理整个流程。

## 4. 目标

本次目标：

- 将 AdminUI 自动粘贴拆成清晰的分阶段状态机。
- `Ctrl+V` 发送前必须确认前台窗口仍是同一个目标 `SunAwtDialog`。
- 如果 `Ctrl+V` 前用户切走，不发送密码。
- 如果 `Ctrl+V` 已经成功发送给目标 `SunAwtDialog`，记录 `PasteSent` 状态。
- 进入 `PasteSent` 后，即使用户焦点切走，也应重新聚焦原目标 `SunAwtDialog` 并继续发送 `Enter`。
- `Enter` 必须发送给原来已经粘贴过的目标窗口，而不是当前任意前台窗口。
- 日志要明确记录状态流转和焦点处理结果。
- 不记录密码明文。
- 不改变 AdminUI 下载、JNLP 拼接、密码加密、剪贴板写入逻辑。

## 5. 非目标

本次不做以下事项：

- 不读取 Java 密码框真实内容。
- 不使用 UI Automation 解析 Java Swing 控件树。
- 不判断登录是否真正成功。
- 不对失败弹窗自动点击“是/否”。
- 不改 AdminUI 的 JNLP 启动逻辑。
- 不取消标题、进程、className 匹配。
- 不把自动粘贴默认改成启用。
- 不记录密码明文或剪贴板明文。

## 6. 推荐方案

### 6.1 引入自动粘贴阶段枚举

新增内部枚举，建议放在：

```text
VSLoader/Models/Services/AdminUiAutoPasteStage.cs
```

或作为 `KeyboardInputService` 的 internal enum：

```csharp
internal enum AdminUiAutoPasteStage
{
    WaitingForDialog,
    BeforePaste,
    PasteSent,
    BeforeEnter,
    EnterSent,
    Completed,
    Aborted
}
```

说明：

- `BeforePaste`：还未发送密码，必须强防护。
- `PasteSent`：已经向目标窗口发送过 `Ctrl+V`，进入提交阶段。
- `BeforeEnter`：准备发送 Enter，需要重新聚焦原目标窗口。

### 6.2 将 KeyboardInputService 拆成分阶段方法

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

推荐将当前 `SendPasteAndEnter(...)` 内部拆成私有步骤：

```csharp
public void SendPasteAndEnter(ForegroundWindowInfo targetWindow, AdminUiAutoPasteLogService? logService = null)
{
    LogPlan(...);

    EnsureTargetForegroundBeforePaste(targetWindow, logService);
    SendPasteShortcut(logService);
    MarkPasteSent(...);

    Thread.Sleep(PasteBeforeEnterDelay);

    EnsureTargetForegroundBeforeEnter(targetWindow, logService);
    SendEnterShortcut(logService);

    LogCompleted(...);
}
```

各步骤职责：

```text
EnsureTargetForegroundBeforePaste:
  - SetForegroundWindow(targetWindow.Handle)
  - 等待焦点稳定
  - 再读取 GetForegroundWindowInfo()
  - 必须确认 handle 与 targetWindow.Handle 一致
  - 必须确认 className 仍是 SunAwtDialog 或兼容空 className
  - 不一致则抛出 InvalidOperationException，中断流程

SendPasteShortcut:
  - 发送 Ctrl+V
  - SendInput 成功后进入 PasteSent

EnsureTargetForegroundBeforeEnter:
  - 不管当前用户焦点在哪里，都尝试 SetForegroundWindow(targetWindow.Handle)
  - 等待焦点稳定
  - 再读取 GetForegroundWindowInfo()
  - 如果能回到 targetWindow，则继续 Enter
  - 如果目标窗口已不存在或无法回到前台，则抛出异常，提示用户手动回车

SendEnterShortcut:
  - 只对重新聚焦后的原目标窗口发送 Enter
```

### 6.3 粘贴前防护规则

`Ctrl+V` 前必须满足：

```text
1. targetWindow.Handle 非 0。
2. SetForegroundWindow(targetWindow.Handle) 执行过。
3. 焦点稳定后，前台窗口 handle 与 targetWindow.Handle 一致。
4. 前台窗口标题仍包含配置关键字。
5. 前台窗口进程仍在允许列表中。
6. className 不是 SunAwtFrame。
```

如果不满足：

```text
不发送 Ctrl+V。
返回失败结果。
状态栏提示：未能确认 AdminUI 登录窗口处于前台，已保留剪贴板密码，请手动粘贴。
```

原因：

```text
此阶段密码还没有进入登录框，安全优先。
```

### 6.4 粘贴后继续提交规则

一旦 `Ctrl+V` 已通过 `SendInput` 成功发送给目标 `SunAwtDialog`：

```text
进入 PasteSent。
```

此后即使用户切走焦点：

```text
不直接中断。
尝试重新聚焦同一个 targetWindow.Handle。
如果目标窗口还存在并可回到前台，则发送 Enter。
```

注意：

```text
Enter 不能发给用户当前所在窗口。
Enter 必须发给同一个 targetWindow.Handle。
```

如果重新聚焦失败：

```text
不向当前窗口发送 Enter。
返回失败结果。
提示用户：密码可能已粘贴，请手动确认登录。
```

### 6.5 目标窗口匹配规则保持现状但语义更明确

`AdminUiAutoPasteService.EvaluateAdminUiWindow(...)` 继续负责等待可粘贴窗口。

当前已有规则：

```text
标题包含 processor
进程名属于 java/javaw/javaws
className 不是 SunAwtFrame
```

建议进一步明确：

```text
className 为空：兼容测试或极端环境，可允许。
className = SunAwtDialog：优先允许。
className = SunAwtFrame：拒绝。
其它 SunAwt*：暂时允许或记录 classMatch=True，但日志必须显示 className。
```

如果后续日志发现失败弹窗也会被错误匹配为 `SunAwtDialog`，再追加更细规则。

本次不做失败弹窗自动识别。

## 7. 日志要求

继续写入：

```text
%LocalAppData%\VSLoader\logs\adminui-autopaste-yyyyMMdd.log
```

新增或强化以下日志：

### 7.1 阶段日志

示例：

```text
[Stage] stage="BeforePaste" targetHandle=123 title="TWBO035.processor" class="SunAwtDialog"
[Stage] stage="PasteSent" targetHandle=123
[Stage] stage="BeforeEnter" targetHandle=123
[Stage] stage="EnterSent" targetHandle=123
[Stage] stage="Completed" targetHandle=123
[Stage] stage="Aborted" reason="Foreground changed before paste"
```

### 7.2 焦点确认日志

示例：

```text
[FocusCheck] stage="BeforePaste" expectedHandle=123 actualHandle=123 matched=True actualClass="SunAwtDialog"
[FocusCheck] stage="BeforePaste" expectedHandle=123 actualHandle=456 matched=False actualTitle="微信"
[FocusCheck] stage="BeforeEnter" expectedHandle=123 actualHandle=123 matched=True
```

### 7.3 键盘发送日志

保留现有：

```text
[KeyboardStep] step="SendInput" shortcut="Ctrl+V" requested=4 sent=4 success=True elapsedMs=2 nativeErrorCode=0
[KeyboardStep] step="SendInput" shortcut="Enter" requested=2 sent=2 success=True elapsedMs=1 nativeErrorCode=0
```

### 7.4 剪贴板检查日志

保留现有：

```text
[ClipboardCheck] expectedLength=9 clipboardLength=9 matchesExpectedText=True
```

要求：

```text
不记录密码明文。
不记录剪贴板明文。
只记录长度和是否一致。
```

## 8. 错误处理与提示

### 8.1 粘贴前失败

场景：

```text
用户在 Ctrl+V 之前切走焦点。
目标窗口不是同一个 SunAwtDialog。
目标窗口消失。
```

处理：

```text
不发送 Ctrl+V。
返回失败。
状态提示：未能确认 AdminUI 登录窗口处于前台，密码已复制到剪贴板，请手动粘贴。
```

### 8.2 粘贴后 Enter 前失败

场景：

```text
Ctrl+V 已成功发送。
等待期间用户切走焦点。
程序尝试重新聚焦 targetWindow，但失败。
```

处理：

```text
不向当前前台窗口发送 Enter。
返回失败。
状态提示：密码可能已粘贴到 AdminUI，请手动确认登录。
```

### 8.3 粘贴后重新聚焦成功

场景：

```text
Ctrl+V 已成功发送。
用户切走焦点。
程序重新聚焦原 targetWindow 成功。
```

处理：

```text
继续发送 Enter。
返回成功。
```

## 9. 测试要求

### 9.1 窗口匹配测试

文件：

```text
VSLoader.Tests/AdminUiAutoPasteServiceTests.cs
```

覆盖：

- `SunAwtFrame` 不匹配。
- `SunAwtDialog` 匹配。
- 空 className 保持兼容。
- 先看到 `SunAwtFrame`，后看到 `SunAwtDialog` 时，只对 `SunAwtDialog` 发送。

### 9.2 键盘状态机测试

新增或更新：

```text
VSLoader.Tests/KeyboardInputServiceTests.cs
```

建议通过可注入的前台窗口读取函数和 SendInput 包装函数测试，避免真实发键。

覆盖：

- `BeforePaste` 前台窗口是目标窗口 -> 发送 `Ctrl+V`。
- `BeforePaste` 前台窗口不是目标窗口 -> 不发送 `Ctrl+V`，返回失败。
- `PasteSent` 后前台窗口切走，但重新聚焦目标成功 -> 继续发送 `Enter`。
- `PasteSent` 后重新聚焦目标失败 -> 不向当前窗口发送 `Enter`。
- 日志包含 `BeforePaste / PasteSent / BeforeEnter / EnterSent / Completed / Aborted`。

### 9.3 日志测试

文件：

```text
VSLoader.Tests/AdminUiAutoPasteLogServiceTests.cs
```

覆盖：

- `LogStage(...)` 写入阶段名和 targetHandle。
- `LogFocusCheck(...)` 写入 expectedHandle、actualHandle、matched。
- 日志不包含密码字段或剪贴板明文。

### 9.4 主流程测试

保留现有源码结构测试：

```text
VSLoader.Tests/MainViewModelAdminUiAutoPasteSourceTests.cs
```

继续确认：

- 只有剪贴板写入成功后才进入自动粘贴。
- 自动粘贴失败不弹错误框，只状态提示。
- 剪贴板检查日志在自动粘贴前执行。

## 10. 手工验证场景

### 10.1 正常登录

步骤：

1. 启用自动粘贴。
2. 点击 AdminUI。
3. 等登录对话框出现。

期望：

```text
密码粘贴到登录对话框。
自动 Enter。
登录成功。
日志显示 SunAwtDialog 被匹配，阶段完整到 Completed。
```

### 10.2 粘贴前用户切走

步骤：

1. 点击 AdminUI。
2. 登录对话框出现前或粘贴前切到其它应用。

期望：

```text
不会把密码粘贴到其它应用。
自动粘贴失败，提示手动粘贴。
日志显示 Aborted，原因是 BeforePaste 焦点不匹配。
```

### 10.3 粘贴后用户切走

步骤：

1. 点击 AdminUI。
2. 程序已经对 SunAwtDialog 发送 Ctrl+V 后，用户切到其它应用。

期望：

```text
程序尝试重新聚焦同一个 SunAwtDialog。
如果聚焦成功，继续 Enter。
不向用户当前窗口发送 Enter。
```

### 10.4 目标窗口消失

步骤：

1. 点击 AdminUI。
2. 粘贴后、Enter 前关闭登录窗口。

期望：

```text
不向其它窗口发送 Enter。
提示密码可能已粘贴，请手动确认。
日志显示 BeforeEnter 聚焦失败。
```

## 11. 验证命令

定向测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~AdminUiAutoPasteServiceTests|FullyQualifiedName~KeyboardInputServiceTests|FullyQualifiedName~AdminUiAutoPasteLogServiceTests|FullyQualifiedName~MainViewModelAdminUiAutoPasteSourceTests" -p:BaseOutputPath=.\artifacts\test-output\ -p:UseSharedCompilation=false
```

全量测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\test-output\ -p:UseSharedCompilation=false
```

构建：

```powershell
dotnet build .\VSLoader.sln -c Debug --no-restore -p:BaseOutputPath=.\artifacts\test-output\ -p:UseSharedCompilation=false
```

覆盖 Debug 输出目录：

```powershell
dotnet build .\VSLoader.sln -c Debug --no-restore -p:UseSharedCompilation=false
```

覆盖 Debug 前必须先从托盘彻底退出 VSLoader，避免 `VSLoader.exe` 被占用。

## 12. 验收标准

实现完成后必须满足：

- 自动粘贴流程分阶段记录状态。
- `Ctrl+V` 前必须确认同一个目标 `SunAwtDialog` 在前台。
- `Ctrl+V` 前用户切走时，不发送密码。
- `Ctrl+V` 成功发送后进入 `PasteSent`。
- `PasteSent` 后用户切走时，程序会尝试重新聚焦原目标窗口。
- 重新聚焦成功后继续发送 Enter。
- 重新聚焦失败时不向当前窗口发送 Enter。
- 日志能区分粘贴前中断、粘贴后 Enter 失败、完整成功。
- 日志不包含密码明文。
- 不影响现有 AdminUI 下载、打开、剪贴板复制逻辑。
- 定向测试通过。
- 全量测试通过。
- Debug 构建通过。
