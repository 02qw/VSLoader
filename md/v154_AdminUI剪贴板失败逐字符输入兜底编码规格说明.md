# v154 AdminUI 剪贴板失败逐字符输入兜底编码规格说明

## 1. 背景

用户在 Windows 10 设备上测试 AdminUI 自动登录时出现错误：

```text
AdminUI 已打开，但写入剪贴板失败：
OpenClipboard Failed (0x800401D0 (CLIPBRD_E_CANT_OPEN))
```

结合当前代码链路确认：

```text
1. VSLoader 已经成功打开 AdminUI。
2. VSLoader 随后尝试把 AdminUI 密码写入 Windows 剪贴板。
3. 剪贴板写入失败后，主流程直接弹错并中断。
4. 后续等待 SunAwtDialog、Ctrl+V、Enter 的自动登录流程没有启动。
```

`CLIPBRD_E_CANT_OPEN` 的含义是 Windows 剪贴板无法打开。常见原因：

```text
1. 剪贴板正被其它进程占用，例如微信、Office、远程桌面、剪贴板管理器、输入法、杀软。
2. Windows 10 剪贴板服务状态不稳定。
3. 程序短时间内打开剪贴板时机不合适。
```

本次需求收束为：

```text
正常设备继续走现有剪贴板 + Ctrl+V + Enter 路径。
只有剪贴板写入失败时，才启用逐字符输入密码的兜底路径。
兜底路径必须仍然严格锁定 SunAwtDialog，不能把密码输入到其它软件。
```

## 2. 当前相关代码

### 2.1 主流程

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
```

相关方法：

```csharp
private async Task OpenAdminUiAsync()
```

当前关键逻辑：

```text
OpenAdminUI
读取 AdminUI 密码
_clipboardService.SetTextWithRetryAsync(password)
剪贴板成功 -> 启动后台自动登录
剪贴板失败 -> ShowError 并中断
```

当前问题：

```text
剪贴板失败后，即使用户启用了“自动粘贴密码并回车”，程序也不会进入自动登录兜底。
```

### 2.2 剪贴板服务

文件：

```text
VSLoader/Models/Services/ClipboardService.cs
```

当前行为：

```text
最多重试 5 次
每次间隔 120ms
使用 System.Windows.Clipboard.SetDataObject(text, true)
失败后返回 SaveResult.Fail(...)
```

当前问题：

```text
总重试时间不到 1 秒。
对于 Windows 10 剪贴板偶发占用或慢释放场景不够稳。
```

### 2.3 自动登录服务

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

当前行为：

```text
扫描顶层窗口
严格匹配 SunAwtDialog
调用 KeyboardInputService.SendPasteAndEnter(...)
```

需要保留：

```text
只允许严格匹配 SunAwtDialog。
不允许向 SunAwtFrame 或其它普通窗口发送密码。
```

### 2.4 键盘输入服务

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

当前行为：

```text
强制拉回目标 SunAwtDialog
关键输入阶段保护
SendInput Ctrl+V
等待 10ms
SendInput Enter
```

当前限制：

```text
它只支持 Ctrl+V，不支持直接逐字符输入密码。
```

## 3. 目标行为

### 3.1 正常路径

剪贴板写入成功时，行为不变：

```text
打开 AdminUI
写入密码到剪贴板
后台等待 SunAwtDialog
强制拉回目标窗口
Ctrl+V
Enter
提示 AdminUI 已自动登录
```

原则：

```text
正常设备不应感知本次新增兜底逻辑。
正常路径不改为逐字符输入。
```

### 3.2 剪贴板失败兜底路径

剪贴板写入失败时，如果用户启用了自动登录：

```text
打开 AdminUI
写剪贴板失败
不立即弹阻塞错误
提示：剪贴板不可用，正在尝试直接输入密码...
后台等待 SunAwtDialog
强制拉回目标窗口
关键输入阶段保护
逐字符输入密码
Enter
提示 AdminUI 已自动登录
```

如果用户没有启用自动登录：

```text
保持当前行为，提示写入剪贴板失败。
因为关闭自动登录时没有安全的自动输入兜底入口。
```

### 3.3 安全原则

兜底路径必须满足：

```text
1. 只允许输入到严格匹配的 SunAwtDialog。
2. 输入每个密码前必须确认目标窗口是前台。
3. 进入关键输入阶段后继续使用 BlockInput 或 Overlay 保护。
4. 焦点无法确认时不输入密码。
5. 不记录密码明文。
```

## 4. 设计方案

### 4.1 增强剪贴板重试

文件：

```text
VSLoader/Models/Services/ClipboardService.cs
```

建议调整：

```text
maxAttempts: 5 -> 15 或 20
delayMilliseconds: 120 -> 分段递增或固定 150ms
总等待时间控制在 2 到 3 秒以内
```

推荐策略：

```text
前 5 次间隔 80ms
中间 5 次间隔 150ms
后续 5 次间隔 250ms
```

原因：

```text
短暂占用通常几百毫秒内释放。
Win10 慢释放场景给到 2 秒左右更稳。
不能无限等待，否则用户会误以为程序卡死。
```

### 4.2 新增剪贴板写入结果类型信息

当前 `SaveResult.Fail(...)` 只返回错误文本。

建议保留现有 `SaveResult` 对外形态，但在 `ClipboardService` 内部增强错误文本：

```text
包含尝试次数
包含最后一次异常类型
包含最后一次异常 HResult
包含最后一次异常 Message
```

示例：

```text
写入剪贴板失败，已重试 15 次。HResult=0x800401D0, Message=OpenClipboard Failed (CLIPBRD_E_CANT_OPEN)
```

### 4.3 新增自动登录输入模式

新增枚举：

```text
VSLoader/Models/Services/AdminUiAutoInputMode.cs
```

建议内容：

```csharp
namespace VSLoader.Services;

public enum AdminUiAutoInputMode
{
    PasteFromClipboard,
    TypePasswordText
}
```

用途：

```text
PasteFromClipboard：现有 Ctrl+V + Enter。
TypePasswordText：剪贴板失败时逐字符输入密码 + Enter。
```

### 4.4 扩展 AdminUiAutoPasteService

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

当前接口：

```csharp
public Task<AdminUiAutoPasteResult> TryPasteAsync(AdminUiConfig config, CancellationToken cancellationToken = default)
```

建议新增重载或参数：

```csharp
public Task<AdminUiAutoPasteResult> TryPasteAsync(
    AdminUiConfig config,
    AdminUiAutoInputMode inputMode,
    string? passwordText,
    CancellationToken cancellationToken = default)
```

兼容规则：

```text
现有调用默认使用 PasteFromClipboard。
兜底调用使用 TypePasswordText，并传入解密后的 password。
```

匹配到 SunAwtDialog 后：

```text
PasteFromClipboard -> keyboardInputService.SendPasteAndEnter(window, logService)
TypePasswordText -> keyboardInputService.SendTextAndEnter(window, passwordText, logService)
```

安全校验：

```text
TypePasswordText 模式下 passwordText 为空则直接返回失败。
日志只记录 passwordLength，不记录密码内容。
```

### 4.5 扩展后台协调器

文件：

```text
VSLoader/Models/Services/AdminUiAutoLoginCoordinator.cs
```

当前职责：

```text
启动后台任务
取消上一轮等待任务
调用 AdminUiAutoPasteService.TryPasteAsync(config)
回调结果
```

建议扩展：

```csharp
public void Start(
    AdminUiConfig config,
    AdminUiAutoInputMode inputMode,
    string? passwordText,
    Action<AdminUiAutoPasteResult> onCompleted,
    Action<Exception> onError);
```

兼容规则：

```text
保留现有 Start(config, onCompleted, onError)，内部转为 PasteFromClipboard。
新增重载仅用于剪贴板失败兜底。
```

### 4.6 扩展 KeyboardInputService

文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

新增方法：

```csharp
public void SendTextAndEnter(
    ForegroundWindowInfo targetWindow,
    string text,
    AdminUiAutoPasteLogService? logService = null)
```

行为：

```text
确认目标 SunAwtDialog 是前台
进入关键输入阶段保护
逐字符输入 text
等待 10ms
确认目标仍是前台
Enter
释放保护
```

逐字符输入实现建议：

```text
使用 SendInput 的 KEYEVENTF_UNICODE。
不要使用虚拟键码模拟字符。
不要依赖当前键盘布局或输入法。
```

原因：

```text
密码可能包含大小写、数字、符号。
虚拟键码会受键盘布局和输入法影响。
Unicode SendInput 更适合输入固定文本。
```

### 4.7 SendInput Unicode 实现要点

在 `KeyboardInputService` 中新增：

```text
KEYEVENTF_UNICODE = 0x0004
```

每个字符发送：

```text
KeyDown Unicode char
KeyUp Unicode char
```

伪代码：

```csharp
private static Input UnicodeKeyDown(char character)
{
    return new Input
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                Scan = character,
                Flags = KeyEventUnicode
            }
        }
    };
}
```

注意：

```text
VirtualKey 必须为 0。
Scan 写入字符。
KeyUp 时 Flags = KeyEventUnicode | KeyEventKeyUp。
```

### 4.8 MainViewModel 流程调整

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
```

调整逻辑：

```text
1. 打开 AdminUI。
2. 解密密码。
3. 尝试写剪贴板。
4. 剪贴板成功：
   - 自动登录关闭：提示密码已复制。
   - 自动登录开启：Start PasteFromClipboard。
5. 剪贴板失败：
   - 自动登录关闭：弹出写剪贴板失败。
   - 自动登录开启：Start TypePasswordText。
```

状态提示建议：

```text
剪贴板成功：AdminUI 已打开，密码已复制到剪贴板，正在后台等待登录窗口...
剪贴板失败但启用兜底：AdminUI 已打开，但剪贴板不可用，正在尝试直接输入密码...
兜底成功：AdminUI 已自动登录。
兜底失败：自动登录失败，请手动输入密码。
```

## 5. 日志要求

文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteLogService.cs
```

新增或增强日志：

```text
[ClipboardWriteStart] textLength=...
[ClipboardWriteAttempt] attempt=... success=False hResult=0x800401D0 message="..."
[ClipboardWriteCompleted] success=True attempts=...
[ClipboardWriteFailed] attempts=... hResult=... message="..."
[TaskStart] inputMode="PasteFromClipboard" passwordLength=0
[TaskStart] inputMode="TypePasswordText" passwordLength=...
[KeyboardPlan] mode="TypePasswordText" textLength=... shortcuts="UnicodeText,Enter"
[KeyboardStep] step="SendUnicodeText" requested=... sent=... success=True elapsedMs=...
```

日志原则：

```text
1. 不记录密码明文。
2. 可以记录 passwordLength。
3. 可以记录 HResult 和异常类型。
4. 日志仍写 adminui-autopaste.log。
5. 日志仍最多保留最新 2000 条。
```

## 6. 边界情况

### 6.1 正常电脑剪贴板成功

行为：

```text
完全走原有 Ctrl+V 路径。
不启用逐字符输入。
用户体验不变。
```

### 6.2 剪贴板被占用但短时间释放

行为：

```text
增强重试后写入成功。
仍走原有 Ctrl+V 路径。
```

### 6.3 剪贴板持续失败且启用自动登录

行为：

```text
启动 TypePasswordText 兜底。
等待 SunAwtDialog。
找到后逐字符输入密码并 Enter。
```

### 6.4 剪贴板失败且未启用自动登录

行为：

```text
弹出错误提示。
不自动输入密码。
```

原因：

```text
用户关闭自动登录时，不应该由系统绕过设置自动输入密码。
```

### 6.5 未检测到 SunAwtDialog

行为：

```text
后台任务超时。
状态栏提示自动登录失败，请手动输入密码。
不弹阻塞错误。
```

### 6.6 逐字符输入过程中焦点丢失

行为：

```text
发送前必须确认目标窗口。
确认失败则尝试短拉回。
仍失败则中止，不继续输入。
```

### 6.7 逐字符输入部分成功后失败

行为：

```text
不再发送 Enter。
提示自动登录失败，请检查登录框内容或手动输入。
日志记录已请求字符数和已发送输入数。
不记录具体密码。
```

### 6.8 密码包含中文或特殊 Unicode 字符

行为：

```text
使用 Unicode SendInput。
允许输入。
如果目标 Java AWT 控件不接受某些 Unicode 字符，则返回失败并提示手动输入。
```

### 6.9 BlockInput 失败

行为：

```text
继续沿用已有 Overlay 兜底。
Overlay 无法启用时不发送密码。
```

### 6.10 连续点击打开 AdminUI

行为：

```text
新任务取消旧的等待任务。
已经进入关键输入阶段的任务不强行打断。
同一时间只允许一个关键输入阶段。
```

## 7. 非目标

本次不做：

```text
1. 不改变 AdminUI JNLP 下载与启动逻辑。
2. 不改变 AdminUI 密码保存格式。
3. 不取消剪贴板正常路径。
4. 不把逐字符输入作为默认路径。
5. 不向 SunAwtFrame 或其它非 SunAwtDialog 窗口发送密码。
6. 不读取 AdminUI 密码框内容。
7. 不判断登录是否成功。
8. 不新增 UI 配置项。
9. 不记录密码明文。
```

## 8. 测试要求

建议新增或更新测试：

```text
VSLoader.Tests/ClipboardServiceTests.cs
VSLoader.Tests/AdminUiAutoPasteServiceTests.cs
VSLoader.Tests/AdminUiAutoLoginCoordinatorTests.cs
VSLoader.Tests/KeyboardInputServiceTests.cs
VSLoader.Tests/MainViewModelAdminUiAutoPasteSourceTests.cs
```

必须覆盖：

```text
1. ClipboardService 在 OpenClipboard 失败时会多次重试。
2. ClipboardService 最终失败时返回包含 HResult 和尝试次数的错误信息。
3. OpenAdminUiAsync 剪贴板成功时仍启动 PasteFromClipboard 模式。
4. OpenAdminUiAsync 剪贴板失败且自动登录开启时启动 TypePasswordText 模式。
5. OpenAdminUiAsync 剪贴板失败且自动登录关闭时不启动自动输入兜底。
6. AdminUiAutoPasteService 在 TypePasswordText 模式下要求 passwordText 非空。
7. AdminUiAutoPasteService 在 TypePasswordText 模式下仍严格匹配 SunAwtDialog。
8. KeyboardInputService.SendTextAndEnter 使用 Unicode SendInput。
9. SendTextAndEnter 不记录密码明文。
10. SendTextAndEnter 在焦点无法确认时不发送密码。
11. 逐字符输入后只等待 10ms 再 Enter。
12. BlockInput 失败时仍使用 Overlay 兜底。
```

## 9. 验证命令

实现完成后执行：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ClipboardService|FullyQualifiedName~AdminUiAutoPaste|FullyQualifiedName~KeyboardInput|FullyQualifiedName~MainViewModelAdminUiAutoPaste"
dotnet build .\VSLoader.sln -c Debug --no-restore
```

如果 Debug 目录被正在运行的 VSLoader 占用，先关闭 VSLoader 后重新构建。

## 10. 人工验证

### 10.1 正常设备

步骤：

```text
1. 配置 AdminUI 密码。
2. 启用自动粘贴密码并回车。
3. 打开 AdminUI。
4. 确认剪贴板成功时仍能快速 Ctrl+V + Enter。
5. 确认体验和旧版本一致。
```

### 10.2 Win10 剪贴板异常设备

步骤：

```text
1. 在出现 OpenClipboard Failed 的 Windows 10 设备上运行。
2. 打开 AdminUI。
3. 确认不再直接弹“写入剪贴板失败”阻塞错误。
4. 等待 SunAwtDialog 出现。
5. 确认程序会直接输入密码并回车。
6. 查看 adminui-autopaste.log，确认 inputMode="TypePasswordText"。
```

### 10.3 安全验证

步骤：

```text
1. SunAwtDialog 出现后尝试点击其它程序。
2. 确认密码不会输入到其它程序。
3. 如果焦点无法拉回，确认程序中止自动输入并提示手动处理。
```

## 11. 验收标准

满足以下条件才算完成：

```text
1. 正常剪贴板路径不受影响。
2. CLIPBRD_E_CANT_OPEN 时不会直接中断自动登录。
3. 剪贴板失败且自动登录开启时，会进入逐字符输入兜底。
4. 逐字符输入只发生在严格匹配的 SunAwtDialog。
5. 关键输入阶段仍有 BlockInput 或 Overlay 保护。
6. 焦点无法确认时不发送密码。
7. 日志能看出剪贴板失败原因和兜底模式。
8. 日志不包含密码明文。
9. 相关单元测试通过。
10. Debug 构建通过。
```
