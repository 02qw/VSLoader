# v138 AdminUI 后台发现 SunAwtDialog 并极速自动登录编码规格说明

## 1. 背景

当前 AdminUI 自动粘贴登录流程已经经过 v127、v128、v129 多轮优化，现有逻辑大致为：

```text
打开 AdminUI -> 写入剪贴板 -> 轮询当前前台窗口 -> 如果前台窗口匹配 AdminUI -> Ctrl+V -> Enter
```

当前有效参数为：

```text
自动粘贴启动等待：0ms
前台窗口轮询间隔：150ms
总等待超时：12s
焦点稳定等待：120ms
Ctrl+V 后到 Enter 等待：0ms
```

用户现在希望进一步把全流程延迟压到最低：

```text
持续监控 SunAwtDialog。
一旦检测到 AdminUI 登录对话框，就立刻执行粘贴和登录确认。
即使当前 VSLoader 或 AdminUI 不是前台，也能在后台找到 SunAwtDialog 后完成操作。
```

这里需要明确一个技术边界：

```text
Windows SendInput 只能可靠发送给当前前台焦点窗口。
不能稳定地把 Ctrl+V / Enter 直接发送给真正后台窗口。
```

因此本次正确方向不是“后台硬发按键”，而是：

```text
后台枚举所有顶层窗口 -> 找到 SunAwtDialog -> 主动激活该窗口 -> 确认焦点 -> Ctrl+V -> Enter
```

这样可以实现用户感知上的“后台发现、自动接管、极速登录”，同时避免密码误粘贴到其它窗口。

## 2. 当前相关代码

### 2.1 AdminUI 打开入口

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
```

当前流程：

```text
OpenAdminUiAsync()
  -> AdminUiService.OpenAdminUiAsync(...)
  -> 解密 AdminUI 密码
  -> ClipboardService.SetTextWithRetryAsync(...)
  -> AdminUiAutoPasteService.TryPasteAsync(...)
```

本次不改变该入口的业务含义，只替换自动粘贴服务内部的窗口发现策略。

### 2.2 自动粘贴等待服务

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

当前核心逻辑：

```csharp
var window = getForegroundWindowInfo();
var match = EvaluateAdminUiWindow(window, config);
if (match.IsMatch)
{
    sendPasteAndEnter(window!);
}
```

不足：

```text
只检查当前前台窗口。
如果 SunAwtDialog 已经出现，但用户焦点在别的程序，就不会立即处理。
需要等用户手动切到 AdminUI 登录框前台，才能触发自动化。
```

### 2.3 键盘输入服务

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

当前核心逻辑：

```text
SetForegroundWindow(targetWindow.Handle)
等待 FocusSettleDelay
确认前台窗口仍是目标窗口
SendInput(Ctrl+V)
等待 PasteBeforeEnterDelay
确认/恢复目标窗口
SendInput(Enter)
```

本次仍复用该分阶段保护策略，不取消焦点确认。

### 2.4 AdminUI 自动登录配置

文件：

```text
VSLoader/Models/AdminUiConfig.cs
```

当前相关字段：

```csharp
public bool AutoPastePasswordEnabled { get; set; } = true;
public int AutoPasteTimeoutSeconds { get; set; } = 12;
public int AutoPasteInitialDelayMilliseconds { get; set; } = 0;
public int AutoPastePollIntervalMilliseconds { get; set; } = 150;
public string AutoPasteWindowTitleKeyword { get; set; } = "processor";
public string AutoPasteProcessNames { get; set; } = "java;javaw;javaws";
```

本次继续沿用这些工作区级配置。

## 3. 核心问题

当前用户真正想优化的是：

```text
不要等用户把 AdminUI 登录框切到前台。
只要 AdminUI 登录框出现，VSLoader 就应该主动发现并尽快处理。
```

但自动登录涉及密码输入，不能牺牲安全性：

```text
不能把密码粘贴到微信、浏览器、VSCode 或 VSLoader 自己。
不能把密码粘贴到 Java 主框架 SunAwtFrame。
不能对未知 Java 弹窗误回车。
不能因为检测太频繁导致 UI 卡顿。
不能因为窗口刚创建但尚未稳定就发送失败。
```

因此新逻辑必须同时做到：

```text
更快发现
更主动激活
更严格确认
失败可追踪
不误粘贴
```

## 4. 目标

本次目标：

1. AdminUI 自动粘贴不再只轮询前台窗口。
2. 新增顶层窗口枚举能力，主动查找所有可见的 Java `SunAwtDialog`。
3. 一旦发现匹配的 AdminUI 登录对话框，立即尝试激活到前台。
4. 激活成功并确认焦点后，立刻执行 `Ctrl+V`。
5. `Ctrl+V` 成功发送给目标窗口后，进入提交阶段，再执行 `Enter`。
6. 如果用户当前焦点在其它程序，也允许 VSLoader 主动切换焦点到 AdminUI 登录框。
7. 自动化只执行一次。
8. 超时未找到目标窗口时，保留当前“密码已复制，请手动粘贴”的提示。
9. 日志必须记录窗口发现、匹配、激活、焦点确认、按键发送结果。
10. 不记录密码明文。

## 5. 非目标

本次不做以下事项：

```text
不实现真正后台无焦点键盘输入。
不使用 Java Access Bridge。
不解析 Java Swing 控件树。
不读取密码输入框内容。
不判断登录是否真正成功。
不自动处理登录失败弹窗。
不修改 AdminUI JNLP 下载和拼接算法。
不修改 AdminUI 密码存储方式。
不修改剪贴板重试服务。
不新增复杂 UI 设置项。
```

## 6. 推荐方案

### 6.1 新增顶层窗口枚举服务

新增文件：

```text
VSLoader/Models/Services/TopLevelWindowService.cs
```

职责：

```text
枚举当前桌面所有顶层窗口。
读取窗口句柄、标题、进程名、窗口类名、可见状态、最小化状态。
返回 ForegroundWindowInfo 或新的 WindowInfo 数据结构。
```

建议复用现有：

```text
ForegroundWindowInfo
ForegroundWindowService 中获取标题、进程名、className 的 Win32 逻辑
```

需要封装的 Win32 API：

```text
EnumWindows
IsWindowVisible
IsIconic
GetWindowText
GetClassName
GetWindowThreadProcessId
```

过滤规则：

```text
排除 Handle == IntPtr.Zero
排除不可见窗口
排除最小化窗口
排除标题为空且 className 为空的窗口
```

### 6.2 AdminUI 窗口匹配规则升级

继续使用：

```text
AdminUiAutoPasteService.EvaluateAdminUiWindow(...)
```

但需要收紧 className 规则：

```text
优先匹配 SunAwtDialog。
明确排除 SunAwtFrame。
如果 className 为空，默认不作为极速后台发现目标。
```

推荐新增方法：

```csharp
internal static AdminUiWindowMatch EvaluateAdminUiDialogWindow(
    ForegroundWindowInfo? window,
    AdminUiConfig config)
```

匹配条件：

```text
标题包含 AutoPasteWindowTitleKeyword，例如 processor
进程名在 AutoPasteProcessNames 中，例如 java/javaw/javaws
className 等于 SunAwtDialog
窗口可见且未最小化
```

说明：

```text
旧的前台匹配可以保留为兼容兜底。
新的后台发现必须更严格，只认 SunAwtDialog。
```

### 6.3 TryPasteAsync 流程重构

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

推荐流程：

```text
TryPasteAsync(config)
  -> 检查 AutoPastePasswordEnabled
  -> 记录 Start 日志
  -> initialDelay，目前默认 0ms
  -> 在 timeout 内循环
       1. 先枚举所有顶层窗口，查找匹配的 SunAwtDialog
       2. 找到则调用 KeyboardInputService.SendPasteAndEnter(targetWindow)
       3. 成功则返回 Ok
       4. 如果没有找到，再可选检查当前前台窗口作为兼容兜底
       5. 等待 pollInterval
  -> 超时返回 Fail
```

推荐检测间隔：

```text
默认 50ms
允许配置范围 50ms - 2000ms
```

原因：

```text
50ms 对用户感知接近实时。
枚举顶层窗口数量通常很少，性能成本可控。
不建议低于 50ms，避免无意义 CPU 抖动和日志过密。
```

配置兼容：

```text
历史 150ms 可以在配置归一化中调整为 50ms。
如果用户手动设置过更大的值，应尊重用户设置。
```

### 6.4 键盘发送策略

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

继续沿用现有策略：

```text
粘贴前：
  SetForegroundWindow(targetWindow.Handle)
  等待 FocusSettleDelay
  确认当前前台窗口就是 targetWindow
  发送 Ctrl+V

粘贴后：
  进入 PasteSent 阶段
  重新聚焦同一个 targetWindow
  发送 Enter
```

推荐延迟：

```text
FocusSettleDelayMilliseconds：保留 120ms，第一版不再继续降低。
PasteBeforeEnterDelayMilliseconds：保留 0ms。
```

说明：

```text
本次提速主要来自“主动发现后台 SunAwtDialog”，不是盲目继续压缩焦点稳定等待。
120ms 是防止 SetForegroundWindow 后焦点尚未稳定的安全垫。
```

### 6.5 日志增强

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteLogService.cs
```

新增日志内容：

```text
[WindowScanStart] pollIndex=...
[WindowCandidate] handle=... title=... process=... class=... visible=... iconic=...
[WindowMatch] handle=... reason=SunAwtDialogMatched
[WindowScanEnd] candidateCount=... matchCount=...
[ActivateTarget] handle=... result=...
```

日志要求：

```text
不记录密码明文。
不记录剪贴板明文。
候选窗口日志可以限制数量，避免刷屏。
只在 Debug 或自动登录日志文件中记录。
继续使用现有滚动日志上限机制。
```

### 6.6 设置界面处理

本次原则上不新增 UI 项。

保留现有：

```text
自动粘贴密码并回车
窗口标题关键字
等待超时时间
```

如果当前设置页没有展示轮询间隔，也不强行新增，避免用户理解负担。

默认行为调整由配置默认值和 ConfigService 归一化完成。

## 7. 边界情况

### 7.1 用户当前在其它程序

```text
允许 VSLoader 主动激活 AdminUI 登录框。
激活成功后执行粘贴和回车。
这是本次需求的核心行为。
```

### 7.2 AdminUI 主窗口 SunAwtFrame 已出现，但登录框未出现

```text
不执行自动粘贴。
继续等待 SunAwtDialog。
```

### 7.3 出现多个 SunAwtDialog

处理策略：

```text
优先选择标题包含配置关键字的窗口。
如果多个都匹配，选择最近枚举到且可激活的第一个。
记录 MultipleMatch 日志。
```

不建议对多个窗口循环发送，避免误操作。

### 7.4 SunAwtDialog 出现后马上消失

```text
激活或焦点确认失败则记录日志。
继续轮询直到超时。
```

### 7.5 SetForegroundWindow 失败

```text
不发送 Ctrl+V。
记录失败。
继续轮询。
```

### 7.6 发送 Ctrl+V 成功后用户切走

沿用 v129 规则：

```text
密码已经发送给目标窗口后，应重新激活同一个目标窗口并继续 Enter。
```

### 7.7 未找到目标窗口超时

保持现有用户体验：

```text
状态栏提示：AdminUI 已打开，密码已复制到剪贴板。等待超时，未检测到 AdminUI 登录窗口，请手动粘贴。
```

### 7.8 权限边界

如果 AdminUI 以管理员权限运行，而 VSLoader 非管理员：

```text
窗口可能可枚举，但 SetForegroundWindow 或 SendInput 可能不稳定。
失败时记录日志并提示手动粘贴。
不做提权。
```

## 8. 测试要求

### 8.1 单元测试

新增或扩展测试文件：

```text
VSLoader.Tests/AdminUiAutoPasteServiceTests.cs
VSLoader.Tests/KeyboardInputServiceTests.cs
```

测试点：

```text
1. 能从窗口列表中选中匹配的 SunAwtDialog。
2. 不选择 SunAwtFrame。
3. 不选择进程名不匹配的窗口。
4. 不选择标题关键字不匹配的窗口。
5. 多个候选窗口时只发送一次。
6. 未找到窗口时按 pollInterval 等待直到超时。
7. 找到窗口后调用 sendPasteAndEnter 的目标句柄正确。
```

### 8.2 静态测试

可增加静态断言：

```text
AdminUiAutoPasteService 使用窗口枚举服务。
后台发现逻辑包含 SunAwtDialog。
仍明确排除 SunAwtFrame。
```

### 8.3 构建验证

执行：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore
dotnet build .\VSLoader.sln -c Debug --no-restore
```

如果 Debug 输出目录被正在运行的 VSLoader 锁定，需要提示用户关闭程序后再构建。

## 9. 实施步骤

### 阶段 1：窗口枚举服务

1. 新增 `TopLevelWindowService`。
2. 抽象可测试的窗口枚举结果。
3. 补充窗口枚举相关测试。

### 阶段 2：AdminUI 匹配策略升级

1. 新增 `EvaluateAdminUiDialogWindow`。
2. 明确后台发现只接受 `SunAwtDialog`。
3. 保留旧前台匹配作为兼容兜底。

### 阶段 3：自动粘贴流程改造

1. `AdminUiAutoPasteService` 注入窗口枚举服务。
2. 轮询逻辑优先扫描所有顶层窗口。
3. 找到目标后调用现有键盘服务。
4. 保证一次流程只发送一次。

### 阶段 4：日志增强

1. 增加窗口扫描日志。
2. 增加候选窗口匹配原因日志。
3. 增加激活目标窗口结果日志。

### 阶段 5：配置归一化

1. 默认轮询间隔调整为 50ms。
2. 配置合法范围调整为 50ms - 2000ms。
3. 历史默认值 150ms 可归一化为 50ms。

### 阶段 6：验证

1. 跑 AdminUI 自动粘贴相关测试。
2. 跑全量测试。
3. 构建 Debug。
4. 如有条件，人工验证：

```text
打开 AdminUI 后立即切到其它程序。
等待 SunAwtDialog 出现。
确认 VSLoader 能主动拉起登录框并完成粘贴回车。
```

## 10. 验收标准

验收时满足以下条件：

```text
1. 用户点击 AdminUI 后，即使当前焦点不在 AdminUI，程序也能发现 SunAwtDialog。
2. SunAwtDialog 出现后，自动登录响应明显快于旧版前台轮询逻辑。
3. 不会对 SunAwtFrame 执行粘贴。
4. 不会对非 java/javaw/javaws 进程执行粘贴。
5. 不会在未确认目标窗口时发送 Ctrl+V。
6. 自动登录只执行一次。
7. 失败时不会崩溃，日志可追踪原因。
8. 现有 AdminUI 打开、下载、密码复制逻辑不受影响。
```

## 11. 风险说明

### 11.1 前台焦点抢占

本方案会在发现目标登录框后主动把它拉到前台。

这是为了保证 `SendInput` 安全可靠，属于必要行为。

### 11.2 极低延迟与稳定性的取舍

不建议把所有延迟全部压到 0。

推荐：

```text
窗口扫描：50ms
焦点稳定：120ms
Ctrl+V 到 Enter：0ms
```

原因：

```text
窗口出现可以快速发现。
但窗口激活后仍需要极短稳定时间，避免密码发送到错误焦点。
```

### 11.3 Java 窗口差异

不同 Java Web Start 环境下，className 可能有差异。

如果实际日志显示登录窗口不是 `SunAwtDialog`，后续可扩展允许 className 白名单，但第一版必须保守。

