# v139 AdminUI 锁定 SunAwtDialog 强制拉回并完成登录编码规格说明

## 1. 背景

v138 已经把 AdminUI 自动粘贴登录从“只看前台窗口”升级为：

```text
打开 AdminUI -> 写入剪贴板 -> 枚举顶层窗口 -> 找到匹配的 SunAwtDialog -> 激活 -> Ctrl+V -> Enter
```

当前用户进一步明确需求：

```text
只要本次流程已经识别到了目标 SunAwtDialog，
后续即使用户把焦点切到别的软件，
也不要直接打断失败，
而是强制把这个 SunAwtDialog 拉回前台，
继续完成粘贴和回车。
```

也就是说，当前期望从“谨慎自动化”进一步变成“锁定目标后的强制自动化”：

```text
发现目标窗口后，目标窗口句柄就是唯一合法登录目标。
焦点不在目标上时，不是失败，而是重试拉回。
只有反复拉不回来，才失败并提示手动粘贴。
```

## 2. 当前相关代码

### 2.1 自动粘贴等待服务

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

当前关键逻辑：

```csharp
var window = FindAdminUiDialogWindow(config) ?? getForegroundWindowInfo();
var match = IsStrictDialogWindow(window)
    ? EvaluateAdminUiDialogWindow(window, config)
    : EvaluateAdminUiWindow(window, config);
```

当前问题：

```text
1. 找不到后台 SunAwtDialog 时仍会回退到旧前台窗口匹配。
2. 旧前台匹配不是严格 SunAwtDialog，只要标题/进程匹配且不是 SunAwtFrame，也可能进入自动粘贴。
3. 如果目标已经锁定，后续键盘阶段仍可能因为焦点被用户切走而失败。
```

本次应收紧为：

```text
自动登录只接受严格匹配的 SunAwtDialog。
不再使用旧前台兜底执行自动登录。
```

### 2.2 键盘输入服务

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

当前核心逻辑：

```csharp
EnsureTargetForeground(targetWindow, BeforePaste, "粘贴前", logService);
SendKeySequence("Ctrl+V", logService);
...
EnsureTargetForeground(targetWindow, BeforeEnter, "Enter 前", logService);
SendKeySequence("Enter", logService);
```

当前 `EnsureTargetForeground(...)` 逻辑是：

```text
SetForegroundWindow(targetWindow.Handle)
等待 120ms
检查当前前台窗口是否是目标窗口
如果不是，直接失败
```

当前问题：

```text
如果用户在自动登录过程中切走焦点，
当前策略会直接打断流程。
```

新需求期望：

```text
如果已经锁定目标 SunAwtDialog，
焦点不在目标时应反复拉回，
而不是第一次失败就中断。
```

## 3. 核心原则

本次不是取消安全校验。

必须保留以下底线：

```text
Ctrl+V 和 Enter 只能在确认当前前台窗口就是目标 SunAwtDialog 后发送。
```

本次改变的是失败策略：

```text
旧策略：焦点不是目标 -> 失败/打断。
新策略：焦点不是目标 -> 继续尝试激活目标窗口 -> 确认成功后再发送。
```

因此新逻辑是：

```text
锁定目标
强制拉回
确认目标
再发送按键
```

而不是：

```text
无视焦点
盲目发送按键
```

## 4. 目标

本次目标：

1. AdminUI 自动登录只锁定严格匹配的 `SunAwtDialog`。
2. 不再使用旧前台窗口兜底执行自动粘贴。
3. 找到目标 `SunAwtDialog` 后，把该窗口句柄作为唯一目标。
4. 粘贴前如果焦点不在目标窗口，持续尝试激活目标。
5. 粘贴前只有确认当前前台窗口是目标窗口后，才发送 `Ctrl+V`。
6. `Ctrl+V` 发送成功后进入提交阶段。
7. 提交阶段即使用户切走焦点，也持续尝试激活同一个目标窗口。
8. 只有确认当前前台窗口重新回到同一个目标窗口后，才发送 `Enter`。
9. 每个阶段都有短超时，避免无限卡住。
10. 日志记录拉回重试次数、成功/失败原因。
11. 不记录密码明文。

## 5. 非目标

本次不做以下事项：

```text
不实现后台无焦点输入。
不使用 Java Access Bridge。
不解析 Java Swing 控件树。
不读取密码框内容。
不判断登录是否成功。
不处理登录失败弹窗。
不修改 AdminUI 下载、JNLP 拼接、WebUI 等逻辑。
不新增设置界面复杂选项。
不降低密码剪贴板写入重试逻辑。
```

## 6. 推荐方案

### 6.1 AdminUiAutoPasteService 只接受 SunAwtDialog

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

将当前逻辑：

```csharp
var window = FindAdminUiDialogWindow(config) ?? getForegroundWindowInfo();
var match = IsStrictDialogWindow(window)
    ? EvaluateAdminUiDialogWindow(window, config)
    : EvaluateAdminUiWindow(window, config);
```

改为：

```text
只调用 FindAdminUiDialogWindow(config)。
如果找不到，继续等待下一轮扫描。
不再用 getForegroundWindowInfo() 作为自动登录目标兜底。
```

推荐伪代码：

```csharp
var window = FindAdminUiDialogWindow(config);
if (window is not null)
{
    sendPasteAndEnter(window);
    return Ok(window);
}
```

说明：

```text
getForegroundWindowInfo 仍可保留给 KeyboardInputService 做焦点确认。
但 AdminUiAutoPasteService 不再把前台窗口当成候选登录目标。
```

### 6.2 KeyboardInputService 引入强制拉回等待

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

将当前单次确认：

```text
SetForegroundWindow
Sleep(120ms)
如果焦点不是目标 -> 失败
```

改为短时间重试：

```text
在阶段超时内循环：
  SetForegroundWindow(target)
  Sleep(FocusSettleDelay)
  读取当前前台窗口
  如果 handle == target.Handle -> 成功返回
  否则继续下一轮

超过阶段超时仍不是目标 -> 失败
```

建议新增常量：

```csharp
internal const int ForceFocusRetryTimeoutMilliseconds = 1500;
internal const int ForceFocusRetryIntervalMilliseconds = 80;
```

说明：

```text
FocusSettleDelayMilliseconds 继续保留 120ms，用于 SetForegroundWindow 后等待系统焦点稳定。
ForceFocusRetryTimeoutMilliseconds 控制一个阶段最多拉回多久，避免卡死。
ForceFocusRetryIntervalMilliseconds 控制失败后的下一次尝试间隔。
```

第一版可简化为：

```text
每轮 SetForegroundWindow 后等待 120ms。
如果失败，再等 80ms 后继续。
最多 1500ms。
```

### 6.3 分阶段策略

粘贴前：

```text
阶段：BeforePaste
目标：确保当前前台窗口是锁定的 SunAwtDialog
成功：发送 Ctrl+V
失败：不发送 Ctrl+V，返回失败，提示手动粘贴
```

粘贴后：

```text
阶段：BeforeEnter
目标：重新拉回同一个 SunAwtDialog
成功：发送 Enter
失败：返回失败，提示用户可能已经粘贴密码但未确认，需要手动检查
```

注意：

```text
BeforePaste 失败时，密码尚未发送。
BeforeEnter 失败时，密码可能已经发送到目标登录框，提示文案应更谨慎。
```

### 6.4 日志增强

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteLogService.cs
```

新增日志：

```text
[FocusRetry] stage=BeforePaste targetHandle=... attempt=... setForegroundResult=...
[FocusRetryResult] stage=BeforePaste targetHandle=... success=True attempts=... elapsedMs=...
[FocusRetryResult] stage=BeforeEnter targetHandle=... success=False attempts=... elapsedMs=...
```

日志要求：

```text
不记录密码明文。
不记录剪贴板明文。
继续使用 RollingLogFileWriter。
日志单文件保留最新 2000 行。
```

### 6.5 错误文案

当前失败统一返回：

```text
自动粘贴按键发送失败：xxx
```

本次建议保持主流程不复杂化，但异常消息应更清楚：

```text
粘贴前无法拉回 AdminUI 登录窗口。
Enter 前无法拉回 AdminUI 登录窗口，密码可能已粘贴，请手动确认。
```

## 7. 边界情况

### 7.1 用户切到其它程序

预期行为：

```text
程序继续尝试把锁定的 SunAwtDialog 拉回前台。
拉回成功后继续 Ctrl+V 或 Enter。
```

### 7.2 用户手动关闭了 SunAwtDialog

预期行为：

```text
SetForegroundWindow 或焦点确认持续失败。
超过阶段超时后返回失败。
不崩溃。
```

### 7.3 另一个 SunAwtDialog 后续出现

预期行为：

```text
一旦进入键盘发送阶段，只使用最初锁定的目标窗口句柄。
不在中途切换到新的 SunAwtDialog。
```

### 7.4 找不到 SunAwtDialog

预期行为：

```text
AdminUiAutoPasteService 在总超时内继续扫描。
超时后提示未检测到 AdminUI 登录窗口。
```

### 7.5 SetForegroundWindow 被系统限制

预期行为：

```text
在阶段超时内多次尝试。
始终失败则返回失败。
不发送按键。
```

### 7.6 Ctrl+V 已成功，Enter 拉回失败

预期行为：

```text
不把 Enter 发给其它窗口。
返回失败。
提示用户密码可能已粘贴，需要手动确认。
```

## 8. 测试要求

### 8.1 AdminUiAutoPasteService 测试

测试文件：

```text
VSLoader.Tests/AdminUiAutoPasteServiceTests.cs
```

新增/调整测试：

```text
1. 前台窗口匹配但不是 SunAwtDialog 时，不自动发送。
2. 顶层窗口存在 SunAwtDialog 时，发送给该窗口。
3. 找不到 SunAwtDialog 时等待到超时。
4. 不再依赖旧前台兜底作为登录目标。
```

### 8.2 KeyboardInputService 测试

测试文件：

```text
VSLoader.Tests/KeyboardInputServiceTests.cs
```

新增/调整测试：

```text
1. 粘贴前第一次焦点不是目标时，会重试拉回并最终发送 Ctrl+V。
2. 粘贴前始终无法拉回目标时，不发送 Ctrl+V。
3. Ctrl+V 成功后，Enter 前即使焦点切走，也会重试拉回同一目标并发送 Enter。
4. Enter 前始终无法拉回目标时，不发送 Enter。
5. 重试次数和等待逻辑不会无限循环。
```

### 8.3 日志测试

测试文件：

```text
VSLoader.Tests/AdminUiAutoPasteLogServiceTests.cs
```

新增/调整测试：

```text
1. FocusRetry 日志包含阶段、目标句柄、尝试次数。
2. 日志不包含密码明文。
3. 日志仍只保留最新 2000 行。
```

## 9. 实施步骤

### 阶段 1：收紧目标来源

1. 修改 `AdminUiAutoPasteService.TryPasteAsync`。
2. 删除旧前台窗口兜底作为自动登录目标。
3. 保留前台窗口读取供键盘阶段确认使用。
4. 更新测试。

### 阶段 2：键盘阶段强制拉回

1. 修改 `KeyboardInputService.EnsureTargetForeground`。
2. 从单次 SetForegroundWindow 改为带超时的重试循环。
3. 每次发送按键前都必须确认前台窗口 handle 与目标一致。
4. 更新测试。

### 阶段 3：日志增强

1. 增加 FocusRetry / FocusRetryResult 日志。
2. 控制日志量，不逐轮输出过多窗口信息。
3. 继续使用 2000 行滚动上限。

### 阶段 4：验证

执行：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~AdminUiAutoPasteServiceTests|FullyQualifiedName~KeyboardInputServiceTests|FullyQualifiedName~AdminUiAutoPasteLogServiceTests"
dotnet build .\VSLoader.sln -c Debug --no-restore
```

必要时再跑全量测试。

## 10. 验收标准

验收时满足：

```text
1. 自动登录只针对 SunAwtDialog。
2. 用户切到其它程序后，程序仍会尝试拉回锁定的 SunAwtDialog。
3. Ctrl+V 只在确认前台窗口是目标 SunAwtDialog 后发送。
4. Enter 只在确认前台窗口是同一个目标 SunAwtDialog 后发送。
5. 拉回失败不会把按键发到其它软件。
6. 拉回失败不会卡死，阶段超时后返回失败。
7. 日志可追踪拉回次数和失败阶段。
8. 现有 AdminUI 下载、密码复制、配置保存逻辑不受影响。
```

