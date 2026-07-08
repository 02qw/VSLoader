# v140 AdminUI 关键输入全屏遮罩兜底编码规格说明

## 1. 背景

v139 已经把 AdminUI 自动登录改为：

```text
扫描并锁定 SunAwtDialog
强制拉回目标窗口
确认焦点
关键输入阶段短暂 BlockInput
Ctrl+V
Enter
finally 释放输入锁
```

用户实测后发现：

```text
BlockInput(true) 返回失败
nativeErrorCode=5
自动化因此被安全策略中止
```

日志证据：

```text
[WindowMatch] ... class="SunAwtDialog"
[FocusCheck] ... matched=True
[InputBlock] requestedBlock=True success=False nativeErrorCode=5
[Error] 关键输入阶段锁定输入失败，未发送自动粘贴按键。nativeErrorCode=5
```

`nativeErrorCode=5` 表示 Access Denied / 权限不足。说明当前普通权限运行的 VSLoader 不能稳定调用 Windows `BlockInput(true)`。

但用户仍然希望保留自动登录能力，并且避免此前出现的极端问题：

```text
用户疯狂点击旁边 Word
Ctrl+V 被 Word 抢走
密码粘贴到了 Word
```

因此本次采用方案 3：

```text
BlockInput 成功时继续使用 BlockInput。
BlockInput 失败时，进入短时全屏输入遮罩兜底。
遮罩拦截用户鼠标点击，减少焦点被 Word 等程序抢走的风险。
```

## 2. 当前相关代码

### 2.1 键盘输入服务

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

当前关键逻辑：

```csharp
ExecuteWithInputBlocked(targetWindow, logService, () =>
{
    EnsureTargetForeground(...);
    SendKeySequence("Ctrl+V", ...);
    sleep(PasteBeforeEnterDelay);
    EnsureTargetForeground(...);
    SendKeySequence("Enter", ...);
});
```

当前问题：

```text
ExecuteWithInputBlocked 中 BlockInput(true) 失败后直接抛异常。
导致自动登录完全不执行。
```

### 2.2 日志服务

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteLogService.cs
```

当前已有：

```text
[InputBlock] targetHandle=... requestedBlock=True success=False nativeErrorCode=5
```

本次需要补充遮罩兜底日志。

## 3. 核心问题

`SendInput(Ctrl+V)` 是全局前台输入，不是发给指定窗口句柄。

因此必须防止这个窗口期发生：

```text
确认 SunAwtDialog 是前台
用户鼠标点击 Word
Ctrl+V 被 Word 吃到
```

`BlockInput` 是强防护，但普通权限可能失败。

兜底遮罩的目标不是完全替代 `BlockInput`，而是：

```text
在关键 200ms - 500ms 内拦截鼠标点击
避免用户点击其它程序抢走焦点
让 Ctrl+V / Enter 更稳定地落到 SunAwtDialog
```

遮罩无法完全拦截系统级快捷键或所有键盘输入，因此仍然必须保留：

```text
每次 SendInput 前确认当前前台窗口还是目标 SunAwtDialog。
```

## 4. 目标

本次目标：

1. `BlockInput(true)` 成功时继续走当前强锁输入逻辑。
2. `BlockInput(true)` 失败时，不再直接放弃自动登录。
3. 失败后创建短时全屏顶层遮罩窗口，用于拦截鼠标点击抢焦点。
4. 遮罩显示后再次拉回并确认目标 `SunAwtDialog`。
5. 只有确认前台窗口是目标 `SunAwtDialog` 后才发送 `Ctrl+V`。
6. `Ctrl+V` 后短暂等待，再次拉回确认目标窗口，再发送 `Enter`。
7. 无论成功或失败，都必须关闭遮罩。
8. 遮罩持续时间尽可能短，让用户感知接近无感。
9. 日志记录 `BlockInput` 失败、遮罩启用、遮罩关闭、关键阶段结果。
10. 不记录密码明文。

## 5. 非目标

本次不做以下事项：

```text
不要求 VSLoader 以管理员权限运行。
不引入 Java Access Bridge。
不解析 Java Swing 控件树。
不读取密码框内容。
不判断登录是否成功。
不处理 Login failed 弹窗。
不改 AdminUI 下载、JNLP 拼接、配置保存逻辑。
不把全流程扫描阶段也遮罩。
不长时间禁用用户操作。
```

## 6. 推荐方案

### 6.1 新增关键输入遮罩服务

新增文件：

```text
VSLoader/Models/Services/CriticalInputOverlayService.cs
```

职责：

```text
创建覆盖所有屏幕的透明/极淡半透明顶层窗口。
窗口置顶。
窗口不在任务栏显示。
窗口拦截鼠标点击。
窗口生命周期极短，由 using/Dispose 管理。
```

推荐 API：

```csharp
public interface ICriticalInputOverlayScope : IDisposable
{
    bool IsActive { get; }
}

public sealed class CriticalInputOverlayService
{
    public ICriticalInputOverlayScope Show();
}
```

实现要点：

```text
遍历 System.Windows.Forms.Screen.AllScreens。
为每个屏幕创建一个 borderless Window。
WindowStyle=None。
AllowsTransparency=True。
Background 使用几乎透明的 Brush，例如 #01000000 或极淡遮罩。
Topmost=True。
ShowInTaskbar=False。
Focusable=False 或不主动抢键盘焦点。
IsHitTestVisible=True，用来吃掉鼠标点击。
```

注意：

```text
遮罩不能抢走键盘焦点。
遮罩只是鼠标点击拦截层。
真正焦点仍通过 SetForegroundWindow 拉回 SunAwtDialog。
```

### 6.2 KeyboardInputService 支持输入保护模式

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

当前：

```text
BlockInput 失败 -> 直接异常
```

改为：

```text
BlockInput 成功：
  使用 BlockInput 保护关键阶段。

BlockInput 失败：
  记录 nativeErrorCode。
  启动 CriticalInputOverlayService.Show()。
  在遮罩保护下执行关键阶段。
  finally 关闭遮罩。
```

推荐伪代码：

```csharp
var blockResult = blockInput(true);
if (blockResult.Success)
{
    try
    {
        action();
    }
    finally
    {
        blockInput(false);
    }
    return;
}

using var overlay = criticalInputOverlayService.Show();
if (!overlay.IsActive)
{
    throw new InvalidOperationException("关键输入阶段保护失败，未发送自动粘贴按键。");
}

action();
```

### 6.3 关键阶段仍必须二次确认焦点

遮罩兜底不能取消焦点确认。

关键阶段保持：

```text
EnsureTargetForeground(BeforePaste, maxAttempts=3)
SendInput(Ctrl+V)
Sleep(80ms)
EnsureTargetForeground(BeforeEnter, maxAttempts=3)
SendInput(Enter)
```

当前参数建议保持：

```text
FocusSettleDelayMilliseconds = 80
ForceFocusRetryIntervalMilliseconds = 40
CriticalInputFocusMaxAttempts = 3
PasteBeforeEnterDelayMilliseconds = 80
```

### 6.4 遮罩显示时长控制

遮罩只包住关键输入阶段，不包住长时间窗口扫描。

预计实际持续时间：

```text
正常情况：
  80ms 焦点稳定
  Ctrl+V
  80ms 粘贴处理
  80ms 焦点稳定
  Enter
  合计约 250ms - 400ms

极端重试：
  可能接近 500ms - 800ms
```

不得超过：

```text
单次关键阶段保护不应超过 1500ms。
```

如果超过，应失败并释放遮罩。

### 6.5 日志增强

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteLogService.cs
```

新增日志：

```text
[InputProtection] mode="BlockInput" success=True
[InputProtection] mode="BlockInput" success=False nativeErrorCode=5
[InputProtection] mode="Overlay" active=True
[InputProtection] mode="Overlay" active=False reason="disposed"
```

也可以继续保留：

```text
[InputBlock]
```

但必须能看出：

```text
BlockInput 失败后是否进入了 Overlay 兜底。
Overlay 是否成功显示。
Overlay 是否被关闭。
```

## 7. 边界情况

### 7.1 BlockInput 成功

预期：

```text
不显示遮罩。
使用 BlockInput 执行关键输入。
finally 释放 BlockInput。
```

### 7.2 BlockInput 失败 nativeErrorCode=5

预期：

```text
显示遮罩兜底。
继续自动登录。
不直接中止。
```

### 7.3 遮罩创建失败

预期：

```text
不发送 Ctrl+V。
记录错误。
提示用户手动粘贴。
```

### 7.4 遮罩显示后用户疯狂点击 Word

预期：

```text
鼠标点击被遮罩吃掉。
程序继续拉回 SunAwtDialog。
Ctrl+V 和 Enter 只在焦点确认后发送。
```

### 7.5 用户使用键盘 Alt+Tab 抢焦点

预期：

```text
遮罩不保证完全拦截系统级键盘。
但发送 Ctrl+V/Enter 前仍会再次确认目标焦点。
如果确认失败则重试。
最终失败也不应把按键发给其它窗口。
```

### 7.6 关键阶段异常

预期：

```text
finally 必须关闭遮罩或释放 BlockInput。
不能留下不可点击屏幕。
```

## 8. 测试要求

### 8.1 KeyboardInputService 测试

测试文件：

```text
VSLoader.Tests/KeyboardInputServiceTests.cs
```

新增/调整测试：

```text
1. BlockInput 成功时，不启动 Overlay。
2. BlockInput 失败时，启动 Overlay 后继续发送 Ctrl+V/Enter。
3. BlockInput 失败且 Overlay 创建失败时，不发送 Ctrl+V/Enter。
4. 发送 Enter 失败时，Overlay 仍会 Dispose。
5. 遮罩兜底阶段仍会执行 BeforePaste / BeforeEnter 焦点确认。
```

### 8.2 CriticalInputOverlayService 测试

建议静态或轻量单元测试：

```text
1. 服务类型存在。
2. Show 返回 IDisposable scope。
3. scope Dispose 可重复调用不抛异常。
```

WPF 窗口真实显示可通过构建和人工验证完成，不强行做复杂 UI 自动化。

### 8.3 日志测试

测试文件：

```text
VSLoader.Tests/AdminUiAutoPasteLogServiceTests.cs
```

新增：

```text
1. LogInputProtection 写入 mode/success/nativeErrorCode。
2. 不包含密码明文。
```

## 9. 实施步骤

### 阶段 1：抽象关键输入保护

1. 新增 `CriticalInputOverlayService` 和 `ICriticalInputOverlayScope`。
2. `KeyboardInputService` 增加 overlay service 注入点，便于测试。
3. 保持默认构造可直接创建真实 overlay service。

### 阶段 2：改造 ExecuteWithInputBlocked

1. `BlockInput(true)` 成功：原逻辑不变。
2. `BlockInput(true)` 失败：记录错误码，创建 overlay scope。
3. overlay active：继续执行关键输入。
4. overlay inactive：抛异常，不发送按键。
5. finally 确保释放保护。

### 阶段 3：日志增强

1. 增加 `LogInputProtection`。
2. 保留 `LogInputBlock`。
3. 日志仍使用 2000 行滚动上限。

### 阶段 4：验证

执行：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~KeyboardInputServiceTests|FullyQualifiedName~AdminUiAutoPasteLogServiceTests"
dotnet build .\VSLoader.sln -c Debug --no-restore
```

如果 Debug 目录被 VSLoader 占用，先关闭程序再构建。

### 阶段 5：人工验证

验证步骤：

```text
1. 打开 AdminUI。
2. 在 SunAwtDialog 出现时疯狂点击旁边 Word。
3. 观察 Word 是否不再收到密码。
4. 观察 AdminUI 是否完成粘贴和回车。
5. 查看 adminui-autopaste.log 是否出现：
   BlockInput failure nativeErrorCode=5
   Overlay active=True
   SendInput Ctrl+V
   SendInput Enter
```

## 10. 验收标准

验收时满足：

```text
1. BlockInput 成功时走 BlockInput。
2. BlockInput 权限失败时不再直接终止自动登录。
3. BlockInput 失败后进入遮罩兜底。
4. 遮罩期间疯狂鼠标点击其它程序，不应把密码粘到其它程序。
5. Ctrl+V/Enter 发送前仍确认 SunAwtDialog 是前台。
6. 异常时遮罩一定关闭。
7. 日志能清楚看到保护模式和失败原因。
8. Debug 构建通过。
```

