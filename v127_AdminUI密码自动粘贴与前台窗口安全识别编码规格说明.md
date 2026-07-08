# v127 AdminUI 密码自动粘贴与前台窗口安全识别编码规格说明

## 1. 背景

当前 VSLoader 打开 AdminUI 的流程是：

```text
用户选择快捷项 -> 点击 AdminUI -> 打开本地 JNLP -> 解密 AdminUI 密码 -> 写入剪贴板 -> 用户手动 Ctrl+V + Enter
```

对应代码位于：

```text
VSLoader/ViewModels/MainViewModel.cs
OpenAdminUiAsync()
```

现有逻辑：

```csharp
var result = await _adminUiService.OpenAdminUiAsync(SelectedShortcut, _config.AdminUi);
...
var password = _passwordProtectionService.Unprotect(_config.AdminUi.ProtectedPassword);
...
var clipboardResult = await _clipboardService.SetTextWithRetryAsync(password);
```

用户希望在密码写入剪贴板后，程序自动执行：

```text
Ctrl + V
Enter
```

从而替代后续人工操作。

但这个需求有明显风险：

```text
如果 AdminUI 没启动好，快捷键会发早。
如果用户切到其它程序，密码可能被粘贴到其它窗口。
如果前台窗口不是 AdminUI，自动回车可能造成误操作。
```

因此本次设计必须以安全为优先，不能无条件发送键盘指令。

## 2. 当前相关代码

### 2.1 AdminUI 打开入口

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
```

当前入口：

```csharp
[RelayCommand(CanExecute = nameof(HasSelectedShortcut))]
private async Task OpenAdminUiAsync()
{
    if (SelectedShortcut is null)
    {
        return;
    }

    var result = await _adminUiService.OpenAdminUiAsync(SelectedShortcut, _config.AdminUi);
    if (!result.Success)
    {
        _dialogService.ShowError(result.ErrorMessage ?? "打开 AdminUI 失败。");
        return;
    }

    var password = _passwordProtectionService.Unprotect(_config.AdminUi.ProtectedPassword);
    if (string.IsNullOrEmpty(password))
    {
        ShowTemporaryStatusMessage("AdminUI 已打开，但未配置 AdminUI 密码。");
        return;
    }

    var clipboardResult = await _clipboardService.SetTextWithRetryAsync(password);
    if (clipboardResult.Success)
    {
        ShowTemporaryStatusMessage("AdminUI 已打开，密码已复制到剪贴板。");
        return;
    }

    _dialogService.ShowError($"AdminUI 已打开，但写入剪贴板失败：{clipboardResult.ErrorMessage}");
}
```

### 2.2 JNLP 启动逻辑

文件：

```text
VSLoader/Models/Services/AdminUiService.cs
```

当前启动方式：

```csharp
Process.Start(new ProcessStartInfo
{
    FileName = info.LocalJnlpPath,
    UseShellExecute = true
});
```

注意：

```text
UseShellExecute=true 打开的是 .jnlp 文件。
真正启动的 AdminUI 窗口通常由 Java Web Start / Java Swing 创建。
Process.Start 返回值不能可靠代表最终 AdminUI 窗口。
```

因此不能只根据 `Process.Start` 返回值判断 AdminUI 已准备好。

### 2.3 密码配置

文件：

```text
VSLoader/Models/AdminUiConfig.cs
```

当前配置包含：

```csharp
public string ProtectedPassword { get; set; } = string.Empty;
```

密码存储在工作区配置中，经 `PasswordProtectionService` 加密/解密。

## 3. 核心问题

本需求真正难点不是发送键盘，而是：

```text
什么时候可以安全发送 Ctrl+V + Enter？
```

必须满足：

```text
当前前台窗口确实是 AdminUI。
```

如果第一时间 AdminUI 不是前台窗口，但用户几秒后切到 AdminUI，程序仍应能自动化。

因此不能只检测一次，而应设计为：

```text
在一个短时间窗口内轮询当前前台窗口。
只要检测到前台窗口匹配 AdminUI，就发送一次 Ctrl+V + Enter。
超时仍未匹配，则放弃自动发送，保留剪贴板密码。
```

## 4. 目标

本次目标：

- 在 AdminUI 密码成功写入剪贴板后，可选自动执行 `Ctrl+V` 和 `Enter`。
- 只有当前前台窗口匹配 AdminUI 特征时才发送键盘指令。
- 如果 AdminUI 一开始不是前台，但用户在等待时间内切到 AdminUI 前台，也应能自动发送。
- 在等待期间如果前台是微信、浏览器、VSLoader 或其它程序，不发送密码。
- 发送键盘指令只执行一次。
- 超时未匹配时不报错，只提示用户密码已复制，可手动粘贴。
- 功能必须可配置，避免不同工作区/产线环境差异导致误触发。
- 默认策略必须偏安全。

## 5. 非目标

本次不做以下事项：

- 不使用 UI Automation 精确查找密码输入框。
- 不解析 Java Swing 控件树。
- 不修改 AdminUI JNLP 文件内容。
- 不改变 AdminUI 下载/拼接算法。
- 不改变密码加密方式。
- 不在未识别窗口时强制发送键盘。
- 不反复发送多次 `Ctrl+V + Enter`。
- 不主动把任意 Java 窗口强行置顶。
- 不做跨用户、跨权限进程注入。

## 6. 推荐方案

### 6.1 新增工作区级 AdminUI 自动登录配置

在：

```text
VSLoader/Models/AdminUiConfig.cs
```

新增配置项：

```csharp
public bool AutoPastePasswordEnabled { get; set; } = false;

public int AutoPasteTimeoutSeconds { get; set; } = 12;

public int AutoPasteInitialDelayMilliseconds { get; set; } = 2500;

public int AutoPastePollIntervalMilliseconds { get; set; } = 300;

public string AutoPasteWindowTitleKeyword { get; set; } = "znt client";

public string AutoPasteProcessNames { get; set; } = "java;javaw;javaws";
```

说明：

- 放在 `AdminUiConfig` 中，属于工作区配置，不属于程序全局配置。
- 不同工作区可能使用不同 AdminUI 标题/Java 启动器，因此应跟随工作区。
- 默认 `AutoPastePasswordEnabled=false`，安全优先，由用户主动开启。

### 6.2 设置页增加配置入口

在设置页 AdminUI 区域新增：

```text
自动粘贴密码并回车
等待超时秒数
初始等待毫秒
窗口标题关键字
允许进程名
```

第一版可以保持简单：

```text
复选框：自动粘贴密码并回车
输入框：窗口标题关键字
输入框：等待超时秒数
```

`初始等待毫秒`、`轮询间隔`、`允许进程名` 可以先使用配置默认值，不一定全部暴露 UI。

推荐 UI 暴露：

- `自动粘贴密码并回车`
- `窗口标题关键字`
- `等待超时秒数`

原因：

- 用户最需要调整的是标题关键字和超时。
- 进程名一般稳定为 `java/javaw/javaws`。
- 轮询间隔和初始延迟属于技术参数，不宜让 UI 过重。

### 6.3 新增前台窗口识别服务

新增服务文件：

```text
VSLoader/Models/Services/ForegroundWindowService.cs
```

职责：

- 调用 Win32 `GetForegroundWindow()` 获取当前前台窗口句柄。
- 调用 `GetWindowText(...)` 获取窗口标题。
- 调用 `GetWindowThreadProcessId(...)` 获取进程 ID。
- 通过 `Process.GetProcessById(...)` 获取进程名。

返回模型：

```csharp
public sealed class ForegroundWindowInfo
{
    public IntPtr Handle { get; init; }

    public string Title { get; init; } = string.Empty;

    public string ProcessName { get; init; } = string.Empty;
}
```

服务接口建议：

```csharp
public sealed class ForegroundWindowService
{
    public ForegroundWindowInfo? GetForegroundWindowInfo()
    {
        ...
    }
}
```

异常处理：

- 取不到前台窗口时返回 `null`。
- 取不到进程名时返回空字符串。
- 不向上抛 Win32/Process 异常。

### 6.4 新增 AdminUI 自动粘贴服务

新增服务文件：

```text
VSLoader/Models/Services/AdminUiAutoPasteService.cs
```

职责：

- 根据配置等待 AdminUI 成为前台窗口。
- 匹配成功后发送一次 `Ctrl+V` 和 `Enter`。
- 超时未匹配则返回失败结果，但不弹窗。

推荐结果模型：

```csharp
public sealed class AdminUiAutoPasteResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public ForegroundWindowInfo? MatchedWindow { get; init; }

    public static AdminUiAutoPasteResult Ok(ForegroundWindowInfo matchedWindow)
    {
        return new AdminUiAutoPasteResult
        {
            Success = true,
            MatchedWindow = matchedWindow,
            Message = "已自动粘贴密码并回车。"
        };
    }

    public static AdminUiAutoPasteResult Fail(string message)
    {
        return new AdminUiAutoPasteResult
        {
            Success = false,
            Message = message
        };
    }
}
```

核心方法：

```csharp
public async Task<AdminUiAutoPasteResult> TryPasteAsync(
    AdminUiConfig config,
    CancellationToken cancellationToken = default)
{
    if (!config.AutoPastePasswordEnabled)
    {
        return AdminUiAutoPasteResult.Fail("未启用自动粘贴。");
    }

    await Task.Delay(GetInitialDelay(config), cancellationToken);

    var timeout = TimeSpan.FromSeconds(Math.Clamp(config.AutoPasteTimeoutSeconds, 1, 60));
    var pollInterval = TimeSpan.FromMilliseconds(Math.Clamp(config.AutoPastePollIntervalMilliseconds, 100, 2000));
    var stopAt = DateTimeOffset.Now + timeout;

    while (DateTimeOffset.Now < stopAt)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var window = foregroundWindowService.GetForegroundWindowInfo();
        if (IsAdminUiWindow(window, config))
        {
            keyboardInputService.SendPasteAndEnter();
            return AdminUiAutoPasteResult.Ok(window!);
        }

        await Task.Delay(pollInterval, cancellationToken);
    }

    return AdminUiAutoPasteResult.Fail("等待超时，未检测到 AdminUI 前台窗口。");
}
```

### 6.5 窗口匹配规则

匹配函数：

```csharp
internal static bool IsAdminUiWindow(ForegroundWindowInfo? window, AdminUiConfig config)
{
    if (window is null)
    {
        return false;
    }

    if (string.IsNullOrWhiteSpace(window.Title) || string.IsNullOrWhiteSpace(window.ProcessName))
    {
        return false;
    }

    var titleKeyword = config.AutoPasteWindowTitleKeyword.Trim();
    if (string.IsNullOrWhiteSpace(titleKeyword)
        || !window.Title.Contains(titleKeyword, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var allowedProcesses = ParseProcessNames(config.AutoPasteProcessNames);
    return allowedProcesses.Contains(window.ProcessName.Trim(), StringComparer.OrdinalIgnoreCase);
}
```

默认匹配：

```text
标题包含：znt client
进程名属于：java / javaw / javaws
```

### 6.6 键盘发送服务

新增服务文件：

```text
VSLoader/Models/Services/KeyboardInputService.cs
```

职责：

- 封装 `SendKeys.SendWait(...)`。
- 不在 ViewModel 中直接调用 WinForms。

实现建议：

```csharp
public sealed class KeyboardInputService
{
    public void SendPasteAndEnter()
    {
        System.Windows.Forms.SendKeys.SendWait("^v");
        System.Windows.Forms.SendKeys.SendWait("{ENTER}");
    }
}
```

如果后续发现 Java Swing 对 `SendWait` 接收不稳定，可再升级为 Win32 `SendInput`。

第一版不直接上 `SendInput`，降低复杂度。

## 7. 主流程调整

修改：

```text
VSLoader/ViewModels/MainViewModel.cs
OpenAdminUiAsync()
```

当前密码复制成功后：

```csharp
ShowTemporaryStatusMessage("AdminUI 已打开，密码已复制到剪贴板。");
return;
```

调整为：

```csharp
if (!adminUiConfig.AutoPastePasswordEnabled)
{
    ShowTemporaryStatusMessage("AdminUI 已打开，密码已复制到剪贴板。");
    return;
}

ShowTemporaryStatusMessage("AdminUI 已打开，密码已复制到剪贴板，正在等待 AdminUI 前台窗口...");
var pasteResult = await _adminUiAutoPasteService.TryPasteAsync(adminUiConfig);
if (pasteResult.Success)
{
    ShowTemporaryStatusMessage("AdminUI 已打开，密码已自动粘贴并回车。");
    return;
}

ShowTemporaryStatusMessage($"AdminUI 已打开，密码已复制到剪贴板。{pasteResult.Message}请手动粘贴。");
```

注意：

- 只有剪贴板写入成功后才执行自动粘贴。
- 自动粘贴失败不应弹错误框。
- 失败时只提示用户手动粘贴。
- 不应清空剪贴板。

## 8. 安全策略

### 8.1 不匹配不发送

以下情况一律不发送键盘：

- 前台窗口为空。
- 前台窗口标题为空。
- 前台窗口进程名为空。
- 标题不包含配置关键字。
- 进程名不在允许列表。
- 等待超时。
- 用户切到微信、浏览器、IDE、资源管理器等非 AdminUI 程序。

### 8.2 只发送一次

匹配成功后：

```text
Ctrl+V
Enter
停止轮询
```

不允许循环发送。

### 8.3 不主动抢焦点

第一版不主动调用 `SetForegroundWindow(...)` 抢焦点。

原因：

- 抢焦点可能违背用户当前操作意图。
- Windows 对跨进程抢焦点有限制，行为不稳定。
- 安全性不如“用户自己切到 AdminUI 前台后再发送”。

### 8.4 等待窗口允许用户手动切换

自动粘贴服务在超时时间内轮询当前前台窗口。

因此可以覆盖：

```text
AdminUI 启动较慢。
AdminUI 一开始没在前台。
用户几秒后手动切到 AdminUI。
```

只要在超时时间内 AdminUI 成为前台并匹配，自动粘贴即可执行。

## 9. 配置保存与兼容

### 9.1 AdminUiConfig Clone

修改：

```text
VSLoader/Models/AdminUiConfig.cs
```

`Clone()` 必须复制新增字段：

```csharp
AutoPastePasswordEnabled = AutoPastePasswordEnabled,
AutoPasteTimeoutSeconds = AutoPasteTimeoutSeconds,
AutoPasteInitialDelayMilliseconds = AutoPasteInitialDelayMilliseconds,
AutoPastePollIntervalMilliseconds = AutoPastePollIntervalMilliseconds,
AutoPasteWindowTitleKeyword = AutoPasteWindowTitleKeyword,
AutoPasteProcessNames = AutoPasteProcessNames
```

### 9.2 ConfigService 兼容旧配置

旧工作区配置文件没有新增字段。

由于新增字段都有默认值，反序列化后应自然使用默认值。

如现有 `ConfigService.NormalizeConfig(...)` 会确保：

```csharp
config.AdminUi ??= new AdminUiConfig();
```

不需要额外迁移文件格式。

### 9.3 SettingsViewModel 校验

在保存设置时校验：

- `AutoPasteTimeoutSeconds` 限制在 1 到 60。
- `AutoPasteWindowTitleKeyword` 在启用自动粘贴时不能为空。
- `AutoPasteProcessNames` 在启用自动粘贴时不能为空。

如果为空，提示：

```text
启用自动粘贴时，请配置 AdminUI 窗口标题关键字和允许进程名。
```

## 10. 测试要求

### 10.1 AdminUiConfig 测试

新增或更新：

```text
VSLoader.Tests/AdminUiConfigTests.cs
```

测试：

- 新字段默认值符合安全策略。
- `Clone()` 会复制所有自动粘贴字段。

### 10.2 窗口匹配测试

新增：

```text
VSLoader.Tests/AdminUiAutoPasteServiceTests.cs
```

测试 `IsAdminUiWindow(...)`：

- 标题包含 `znt client` 且进程 `javaw` => true。
- 标题不匹配 => false。
- 进程不匹配 => false。
- 标题为空 => false。
- 窗口信息为 null => false。
- 进程名大小写不同仍匹配。
- 多个进程名用分号分隔可匹配。

### 10.3 自动粘贴轮询测试

`AdminUiAutoPasteService` 应支持注入假的：

- 前台窗口读取函数。
- 键盘发送函数。
- 延迟函数或短轮询参数。

测试：

- 第一次前台不是 AdminUI，第二次变成 AdminUI => 最终发送一次。
- 一直不是 AdminUI => 不发送，返回失败。
- 匹配成功后不会继续轮询发送第二次。
- 未启用自动粘贴 => 不发送。

### 10.4 MainViewModel 调用路径测试

现有 MainViewModel 单元测试风格如果不方便直接构造服务，可使用源码结构测试兜底：

- 剪贴板成功后，如果启用自动粘贴，会调用 `_adminUiAutoPasteService.TryPasteAsync(...)`。
- 自动粘贴失败时不会调用 `_dialogService.ShowError(...)`。
- 未配置密码时不调用自动粘贴。
- 剪贴板失败时不调用自动粘贴。

## 11. 手工验证场景

### 11.1 正常自动登录

步骤：

1. 设置中启用“自动粘贴密码并回车”。
2. 配置窗口标题关键字为 `znt client`。
3. 配置密码。
4. 点击快捷项 AdminUI。
5. 等 AdminUI 成为前台窗口。

期望：

```text
密码自动粘贴。
自动回车。
状态栏提示已自动粘贴并回车。
```

### 11.2 AdminUI 启动慢

步骤：

1. 点击 AdminUI。
2. AdminUI 过几秒才显示。

期望：

```text
只要 AdminUI 在超时时间内成为前台，仍自动粘贴。
```

### 11.3 用户先切到其它程序，再切回 AdminUI

步骤：

1. 点击 AdminUI。
2. 立即切到其它程序。
3. 等几秒后切到 AdminUI。

期望：

```text
在其它程序前台期间不会发送密码。
切到 AdminUI 后，如果仍在超时时间内，则自动粘贴一次。
```

### 11.4 超时未切到 AdminUI

步骤：

1. 点击 AdminUI。
2. 一直停留在其它程序或 VSLoader。

期望：

```text
不会发送 Ctrl+V。
状态栏提示密码已复制，请手动粘贴。
```

### 11.5 未启用自动粘贴

步骤：

1. 设置中关闭自动粘贴。
2. 点击 AdminUI。

期望：

```text
保持旧行为：只复制密码到剪贴板，不自动按键。
```

## 12. 验证命令

定向测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~AdminUiAutoPasteServiceTests -p:BaseOutputPath=.\artifacts\test-output\
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

## 13. 验收标准

实现完成后必须满足：

- 设置中可以启用/关闭 AdminUI 自动粘贴。
- 默认关闭自动粘贴。
- 密码写入剪贴板失败时不执行自动粘贴。
- 未配置密码时不执行自动粘贴。
- 自动粘贴只在前台窗口匹配 AdminUI 时执行。
- 用户在超时时间内切到 AdminUI 前台时，可以自动粘贴。
- 前台窗口不是 AdminUI 时绝不发送键盘指令。
- 自动粘贴成功后只发送一次。
- 自动粘贴失败不弹错误框，只状态提示。
- 不影响原有 AdminUI 打开和剪贴板复制行为。
- 新增测试通过。
- 全量测试通过。
- Debug 构建通过。

