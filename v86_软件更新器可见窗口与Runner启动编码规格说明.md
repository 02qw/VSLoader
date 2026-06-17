# v86 软件更新器可见窗口与 Runner 启动编码规格说明

## 1. 背景

当前 VSLoader 已实现 v85 自动更新能力：

1. 主程序读取 `manifest.json`。
2. 根据 manifest 找到更新 zip。
3. 校验 SHA256。
4. 解压更新包到 `%LocalAppData%` 或配置的更新临时目录。
5. 启动 `VSLoader.Updater.exe`。
6. 主程序退出。
7. 更新器替换主程序文件并重启 VSLoader。

当前用户测试发现：

> 点击“更新软件”后，主程序退出，但后续没有明显界面反馈，用户不知道程序是否正在更新、是否失败、是否卡住。

经代码检查，`VSLoader.Updater` 项目已有基础 WPF 窗口，但当前启动链路存在设计隐患：

1. 主程序从 `staging` 解压目录中直接启动 `VSLoader.Updater.exe`。
2. updater 更新成功后会清理 `Updates\staging`。
3. 这意味着 updater 运行在将要被清理的目录里，存在“运行中的程序尝试删除自身所在目录”的风险。
4. 主程序启动 updater 后立刻退出，没有确认 updater 已成功启动并可见。
5. updater 对启动期异常、参数异常、窗口显示失败缺少足够明显的用户反馈和错误日志兜底。

本次 v86 的目标是：在不推翻 v85 自动更新主流程的前提下，修复更新器可见性和启动稳定性。

## 2. 需求目标

### 2.1 用户体验目标

用户点击主界面“更新软件”后：

1. 主程序先显示原有覆盖进度层，完成下载、校验、解压准备。
2. 主程序启动独立更新器窗口。
3. 更新器窗口必须明确显示在屏幕上。
4. 用户能看到当前更新阶段：
   - 正在等待主程序退出
   - 正在备份旧版本
   - 正在替换程序文件
   - 正在清理临时文件
   - 正在启动新版程序
5. 更新期间用户不能关闭更新器窗口。
6. 更新成功后，更新器自动启动新版 VSLoader，然后关闭自身。
7. 更新失败时，更新器窗口停留，显示错误原因，并提供打开日志目录按钮。

### 2.2 技术目标

1. 不再从 `staging` 目录直接运行 updater。
2. 新增独立的 updater runner 目录。
3. updater runner 目录用于承载本次正在运行的更新器程序。
4. staging 目录只作为新版本文件来源。
5. 更新成功后可以安全清理 `download` 和 `staging`。
6. runner 目录本次不强制删除，由下一次更新前覆盖清理。
7. 主程序启动 updater 失败时，不退出主程序，并弹出错误提示。
8. updater 启动期异常必须写入 `%LocalAppData%\VSLoader\errorLog`。

## 3. 核心方案

采用“Runner 启动目录”方案。

### 3.1 当前旧流程

```text
manifest.json
  ↓
复制 zip 到 download
  ↓
解压 zip 到 staging\版本号
  ↓
从 staging\版本号 启动 VSLoader.Updater.exe
  ↓
主程序退出
  ↓
updater 替换 targetDir
  ↓
updater 清理 staging
```

旧流程的问题是：updater 自己就在 staging 里面运行，却又要清理 staging。

### 3.2 新流程

```text
manifest.json
  ↓
复制 zip 到 download
  ↓
解压 zip 到 staging\版本号
  ↓
复制 updater 运行所需文件到 runner
  ↓
从 runner 启动 VSLoader.Updater.exe
  ↓
主程序退出
  ↓
updater 从 staging\版本号 复制新版本到 targetDir
  ↓
updater 清理 download 和 staging
  ↓
updater 启动新版 VSLoader
```

runner 目录建议：

```text
%LocalAppData%\VSLoader\softwareUpdates\runner
```

如果当前代码中 `_softwareUpdatesRoot` 已经代表：

```text
%LocalAppData%\VSLoader\softwareUpdates
```

则 runner 目录为：

```text
Path.Combine(request.UpdatesRoot, "runner")
```

## 4. 涉及文件

### 4.1 主程序

需要检查和修改：

```text
VSLoader\Models\Services\SoftwareUpdateService.cs
VSLoader\Models\Services\SoftwareUpdateModels.cs
VSLoader\ViewModels\MainViewModel.cs
```

可能需要新增或扩展测试：

```text
VSLoader.Tests\SoftwareUpdateServiceTests.cs
VSLoader.Tests\MainViewModelSoftwareUpdateTests.cs
```

### 4.2 更新器程序

需要检查和修改：

```text
VSLoader.Updater\App.xaml.cs
VSLoader.Updater\MainWindow.xaml
VSLoader.Updater\MainWindow.xaml.cs
VSLoader.Updater\Services\UpdaterApplyService.cs
VSLoader.Updater\Services\UpdaterArgumentParser.cs
VSLoader.Updater\Services\UpdaterOptions.cs
```

可能需要新增或扩展测试：

```text
VSLoader.Tests\UpdaterArgumentParserTests.cs
VSLoader.Tests\UpdaterApplyServiceTests.cs
```

## 5. 数据结构调整

### 5.1 SoftwareUpdateResult

当前 `SoftwareUpdateResult` 已包含：

```csharp
public string? UpdaterPath { get; init; }
public string UpdaterArguments { get; init; } = string.Empty;
```

本次可继续沿用，不一定新增字段。

但 `UpdaterPath` 的含义需要调整：

旧含义：

```text
staging 目录中的 VSLoader.Updater.exe
```

新含义：

```text
runner 目录中的 VSLoader.Updater.exe
```

### 5.2 SoftwareUpdateRequest

当前已有：

```csharp
public string UpdatesRoot { get; init; } = string.Empty;
```

本次可以基于 `UpdatesRoot` 推导：

```csharp
var runnerDirectory = Path.Combine(request.UpdatesRoot, "runner");
```

不强制新增字段。

## 6. 主程序修改规格

### 6.1 SoftwareUpdateService.PrepareUpdateAsync

在现有流程中，解压完成并确认 staging 内包含：

```text
VSLoader.exe
VSLoader.Updater.exe
```

之后，新增 runner 准备步骤。

推荐流程：

```csharp
var runnerDirectory = Path.Combine(request.UpdatesRoot, "runner");
PrepareCleanDirectory(runnerDirectory);
CopyUpdaterRunnerFiles(stagingDirectory, runnerDirectory, request.UpdaterExeName);
var updaterPath = Path.Combine(runnerDirectory, request.UpdaterExeName);
```

### 6.2 runner 文件复制范围

为了保证 updater 是自包含可运行的，runner 目录不要只复制 `VSLoader.Updater.exe`。

推荐最小稳定策略：

1. 从 staging 目录复制所有以 `VSLoader.Updater` 开头的文件：
   - `VSLoader.Updater.exe`
   - `VSLoader.Updater.dll`
   - `VSLoader.Updater.deps.json`
   - `VSLoader.Updater.runtimeconfig.json`
   - `VSLoader.Updater.pdb` 如果存在也可复制
2. 如果发布包是 self-contained，updater 可能还依赖运行时 dll。
3. 为避免漏依赖，推荐 runner 直接复制 staging 目录中的全部文件到 runner。

注意：

runner 复制全部文件不会影响 target 更新，因为 updater 仍然从 `stagingDirectory` 复制到 `targetDirectory`。

推荐实现：

```csharp
PrepareCleanDirectory(runnerDirectory);
CopyDirectory(stagingDirectory, runnerDirectory);
var updaterPath = Path.Combine(runnerDirectory, request.UpdaterExeName);
```

这样 updater 在 runner 中拥有完整运行依赖，不容易出现“exe 启动不了”的问题。

### 6.3 BuildUpdaterArguments

参数继续传：

```text
--processId
--targetDir
--stagingDir
--mainExeName
--updatesRoot
```

不需要把 runner 目录传给 updater，除非后续要让 updater 自清理旧 runner。

本次不建议让 updater 删除当前 runner，避免再次出现“自己删自己”。

### 6.4 MainViewModel.StartUpdater

当前启动代码：

```csharp
Process.Start(new ProcessStartInfo
{
    FileName = path,
    Arguments = arguments,
    UseShellExecute = true
});
```

需要调整为可感知失败。

要求：

1. 如果 `Process.Start` 返回 null，视为启动失败。
2. 如果抛异常，弹出错误提示。
3. 启动失败时不要执行 `RequestApplicationExit()`。
4. 启动成功后再退出主程序。

建议将 `StartUpdater` 从 `Action<string, string>` 调整为：

```csharp
public Func<string, string, bool> StartUpdater { get; set; }
```

默认实现：

```csharp
public Func<string, string, bool> StartUpdater { get; set; } = static (path, arguments) =>
{
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = path,
        Arguments = arguments,
        WorkingDirectory = Path.GetDirectoryName(path),
        UseShellExecute = true
    });

    return process is not null;
};
```

调用处：

```csharp
var started = StartUpdater(result.UpdaterPath, result.UpdaterArguments);
if (!started)
{
    _dialogService.ShowError("更新器启动失败，主程序不会退出。");
    return;
}

RequestApplicationExit();
```

如果为了最小改动，不想修改委托类型，也必须在 `UpdateSoftwareAsync` 内对启动异常做 try/catch，并确保启动失败不退出主程序。

## 7. 更新器窗口修改规格

### 7.1 App.xaml.cs 全局异常兜底

更新器启动时需要注册：

```csharp
DispatcherUnhandledException
AppDomain.CurrentDomain.UnhandledException
TaskScheduler.UnobservedTaskException
```

要求：

1. 异常写入 `%LocalAppData%\VSLoader\errorLog`。
2. 如果窗口还没显示，应尽量显示一个错误窗口。
3. 错误信息不能只写日志后静默退出。

### 7.2 MainWindow 显示策略

当前窗口已经有：

```xml
WindowStartupLocation="CenterScreen"
```

本次建议补充：

```xml
ShowInTaskbar="True"
Topmost="True"
```

并在窗口 Loaded 后短暂取消 Topmost：

```csharp
Topmost = true;
Activate();
Topmost = false;
```

目标是确保用户能看见 updater 窗口。

### 7.3 更新期间禁止关闭

保留现有逻辑：

```csharp
if (isUpdating)
{
    e.Cancel = true;
    MessageBox.Show(this, "正在更新，暂时不能关闭。", ...);
}
```

但建议将系统 MessageBox 后续逐步替换为项目统一自定义弹窗。本次不是必须。

### 7.4 失败时窗口停留

当前失败时：

```csharp
isUpdating = false;
HasError = true;
StatusText = message;
```

这个逻辑保留。

要求补充：

1. 失败时进度条不要停在误导性数值。
2. 可以设置为当前失败阶段的值。
3. “打开日志目录”按钮必须可用。
4. 如果没有日志路径，也打开默认日志目录。

### 7.5 成功时关闭策略

成功时：

1. 先显示 `更新完成，正在启动新版程序...`。
2. 启动新版 `VSLoader.exe`。
3. 等待 300-800ms，让用户能感知完成。
4. 关闭 updater。

示例：

```csharp
SetProgress(100, "更新完成，正在启动新版程序...");
await Task.Delay(500);
isUpdating = false;
Close();
```

## 8. 更新器替换逻辑修改规格

### 8.1 UpdaterApplyService.CleanupOnSuccess

当前清理：

```csharp
DeleteDirectoryIfExists(Path.Combine(updatesRoot, "download"));
DeleteDirectoryIfExists(Path.Combine(updatesRoot, "staging"));
KeepLatestBackup(updatesRoot);
```

保留。

但需要确认 updater 已经不从 `staging` 运行。

### 8.2 runner 清理策略

本次不在 updater 运行结束时删除当前 runner。

原因：

1. updater 正在 runner 内运行。
2. 删除自身所在目录仍有风险。
3. runner 文件体积可接受。

清理策略：

1. 下一次主程序准备更新时，先删除旧 runner。
2. 再复制新 runner。

也就是 `SoftwareUpdateService.PrepareUpdateAsync` 中执行：

```csharp
PrepareCleanDirectory(runnerDirectory);
```

## 9. 错误日志规格

更新失败或 updater 启动期异常，日志统一写入：

```text
%LocalAppData%\VSLoader\errorLog
```

日志文件名：

```text
yyyyMMdd_HHmmss_fff.log
```

日志内容至少包含：

```text
Time
Step
TargetDirectory
StagingDirectory
ProcessId
Exception
RollbackException 如果存在
```

如果是 App.xaml.cs 启动期异常，至少包含：

```text
Time
Source: VSLoader.Updater startup
Args
Exception
```

## 10. 测试要求

### 10.1 SoftwareUpdateServiceTests

新增或修改测试：

#### 测试 1：PrepareUpdateAsync 返回 runner 中的 updater 路径

期望：

```text
result.UpdaterPath 包含 Updates\runner\VSLoader.Updater.exe
```

而不是：

```text
Updates\staging\版本号\VSLoader.Updater.exe
```

#### 测试 2：runner 目录包含 updater 文件

测试包内包含：

```text
VSLoader.exe
VSLoader.Updater.exe
VSLoader.Updater.dll
VSLoader.Updater.runtimeconfig.json
```

执行后断言：

```text
Updates\runner\VSLoader.Updater.exe 存在
Updates\runner\VSLoader.Updater.dll 存在
```

#### 测试 3：旧 runner 会被清理

先创建：

```text
Updates\runner\old.txt
```

执行 `PrepareUpdateAsync` 后断言：

```text
old.txt 不存在
```

### 10.2 MainViewModelSoftwareUpdateTests

新增测试：

#### 测试 1：updater 启动成功后才请求退出

`StartUpdater` 返回 true：

```text
exitRequested == true
```

#### 测试 2：updater 启动失败时不退出主程序

`StartUpdater` 返回 false：

```text
exitRequested == false
dialogService.Errors 包含 更新器启动失败
```

如果委托仍使用 Action，则测试启动异常：

```csharp
viewModel.StartUpdater = (_, _) => throw new InvalidOperationException("boom");
```

期望：

```text
exitRequested == false
dialogService.Errors 包含 boom 或 更新器启动失败
```

### 10.3 UpdaterApplyServiceTests

保留现有测试：

1. 替换文件成功。
2. 成功后清理 download 和 staging。
3. 失败后回滚并写日志。

新增确认：

1. `CleanupOnSuccess` 不清理 runner。
2. 如果存在 `Updates\runner`，更新成功后该目录仍存在。

## 11. 手工验证流程

### 11.1 构建验证

执行：

```powershell
dotnet test .\VSLoader.sln
```

期望：

```text
全部测试通过
```

执行：

```powershell
.\build-release.ps1
```

期望生成：

```text
release-update\manifest.json
release-update\VSLoader_版本号_win-x64.zip
installer\Output\VSLoader_Setup_版本号.exe
```

### 11.2 Debug 更新验证

可用 Debug 输出目录做更新测试。

步骤：

1. 运行 Debug 版 VSLoader。
2. 设置软件更新 manifest 路径为当前 `release-update\manifest.json`。
3. 点击“更新软件”。
4. 主程序显示准备更新进度。
5. 主程序退出前，更新器窗口应出现或立刻出现。
6. 更新器窗口显示更新阶段。
7. 更新完成后新版程序启动。

### 11.3 异常验证

可临时制造错误：

1. 删除 staging 中的 `VSLoader.exe`。
2. 或让目标目录文件被占用。
3. 或传入错误参数启动 updater。

期望：

1. updater 窗口显示错误。
2. 不静默退出。
3. `%LocalAppData%\VSLoader\errorLog` 生成日志。
4. 用户可以通过按钮打开日志目录。

## 12. 非目标

本次不做以下内容：

1. 不重做 v85 manifest 格式。
2. 不改变 SHA256 校验规则。
3. 不改变安装包构建逻辑。
4. 不改变工作区更新检测逻辑。
5. 不新增在线下载协议。
6. 不做差分更新。
7. 不做后台静默更新。
8. 不让 updater 删除自身 runner 目录。

## 13. 风险与注意事项

### 13.1 runner 复制全部文件体积较大

复制 staging 全部文件到 runner 会占用更多临时空间。

但优点是：

1. updater 运行依赖最完整。
2. 不容易漏 dll。
3. 实现简单稳定。

考虑到更新是低频操作，该成本可以接受。

### 13.2 Debug 目录测试可能污染 Debug 输出

如果用户用 Debug 输出目录直接测试更新，Release 包可能会覆盖 Debug 目录。

这是可接受的测试方式，但测试后建议执行：

```powershell
Remove-Item .\VSLoader\bin\Debug\net8.0-windows -Recurse -Force
dotnet build .\VSLoader.sln -c Debug
```

### 13.3 主程序退出时机

主程序必须在确认 updater 已成功启动后再退出。

不能先退出再尝试启动 updater。

### 13.4 updater 窗口可见性

updater 窗口不应依赖后台日志来证明存在。

用户必须能在桌面或任务栏中看到更新窗口。

## 14. 验收标准

满足以下条件视为完成：

1. 点击“更新软件”后，不再出现“主程序退出后没有任何下文”的体验。
2. updater 从 runner 目录启动，而不是 staging 目录。
3. updater 窗口可见，显示进度和当前步骤。
4. 更新成功后能启动新版 VSLoader。
5. 更新失败时窗口停留并显示错误。
6. 失败日志写入 `%LocalAppData%\VSLoader\errorLog`。
7. 主程序启动 updater 失败时不会退出。
8. `dotnet test .\VSLoader.sln` 通过。
9. `.\build-release.ps1` 通过。

