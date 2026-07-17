# v161 右键菜单能力集合与 PowerShell/Web 自定义动作编码规格说明

## 1. 背景

当前 VSLoader 中，快捷项和工厂地图节点都提供右键菜单。

主界面快捷项菜单当前在 `MainWindow.xaml` 中静态定义：

```text
VSCode
WebUI
AdminUI
获取AdminUI连接
编辑
删除
```

工厂地图浏览模式下的节点菜单当前在 `FactoryMapWindow.xaml.cs` 中动态创建，但菜单内容仍然写死，并通过 `FactoryMapShortcutAction` 枚举转发给主窗口。

这两套菜单虽然展示了相同能力，但仍存在以下问题：

```text
1. 两个界面分别维护菜单项，容易出现顺序和功能不一致。
2. 用户无法隐藏、启用或调整现有业务能力的顺序。
3. 用户无法基于快捷项底层数据创建自己的操作。
4. VSCode、WebUI 等能力被固化为程序专用逻辑，无法扩展为通用能力。
5. 当前不存在统一的能力定义、执行上下文、校验、错误反馈和安全边界。
6. 直接把命令字符串加入现有点击事件，会把 UI、配置、进程执行和业务逻辑继续耦合。
```

本次需要把右键菜单从“写死的菜单项集合”升级为“工作区级右键菜单能力集合”。

## 2. 核心结论

本次必须遵循：

```text
1. 主界面快捷项和地图浏览模式节点使用同一份能力配置。
2. 能力配置属于当前工作区，不污染其他工作区。
3. 能力配置跟随全局配置导入和导出。
4. 第一版支持内建能力、PowerShell 命令能力和 Web 能力。
5. PowerShell 使用 Windows 10/11 自带的 powershell.exe，不依赖 PowerShell 7。
6. 不把快捷项路径直接拼进 PowerShell 脚本，而是通过环境变量传递。
7. Web 能力使用 URL 模板，并对变量值进行 URL 编码。
8. 导入配置中的自定义命令不得被配置文件自行标记为可信。
9. 编辑、删除仍是受保护的系统管理动作，不能改写其底层逻辑。
10. 地图编辑模式的节点、连接点、线段和空白区域拓扑菜单不纳入本次能力集合。
11. 任意自定义能力失败不得导致主窗口或地图窗口崩溃。
12. 旧配置缺少能力集合时，自动补齐当前默认菜单，保持现有使用体验。
```

## 3. 目标

本次必须实现：

```text
1. 设置页新增“右键菜单能力集合”独立区域。
2. 用户可以查看、启用、停用和调整业务能力顺序。
3. 用户可以新增、编辑、复制和删除自定义 PowerShell 能力。
4. 用户可以新增、编辑、复制和删除自定义 Web 能力。
5. VSCode、WebUI、AdminUI、获取AdminUI连接继续作为内建能力存在。
6. 编辑、删除固定放在菜单底部的系统管理区域。
7. 主界面和地图浏览模式节点按同一份配置生成菜单。
8. 地图节点执行能力时直接使用该节点关联的 ShortcutItem，不依赖主窗口选中状态。
9. PowerShell 执行支持中文、空格、UNC 网络路径和特殊字符。
10. PowerShell 执行支持可见终端和后台运行两种模式。
11. 后台运行支持超时、退出码、标准输出和错误输出。
12. Web 能力使用系统默认浏览器打开生成后的 URL。
13. 配置保存前完成结构、名称、模板、超时和协议校验。
14. 配置读取损坏时回退到默认能力集合并给出可追溯警告。
15. 全局配置导入导出完整包含能力定义，但不导出本机信任状态。
16. 设置保存成功后，已打开的主窗口和地图窗口立即使用新顺序。
```

## 4. 非目标

本次不做：

```text
1. 不实现第三方 DLL、插件程序集或脚本插件市场。
2. 不允许用户修改内建能力的底层 C# 执行逻辑。
3. 不允许自定义能力替换“编辑”和“删除”的系统语义。
4. 不把地图编辑模式中的“开始连接”“断开连接”等拓扑动作配置化。
5. 不支持 JavaScript、Python、Bash 等其他脚本引擎。
6. 不依赖 pwsh.exe 或要求用户安装 PowerShell 7。
7. 不在第一版开放 cmd.exe 执行器。
8. 不支持任意层级的嵌套子菜单。
9. 不实现复杂条件表达式语言。
10. 不自动在后台执行任何自定义能力。
11. 不允许 Web 能力执行 javascript:、data:、file: 或自定义协议。
12. 不允许配置包把自定义命令预先标记为已信任。
13. 不把 AdminUI 密码、剪贴板内容或其他敏感数据暴露给自定义能力。
14. 不修改快捷项、地图拓扑和窗口状态的数据模型语义。
```

## 5. 当前代码边界

### 5.1 主界面菜单

当前文件：

```text
VSLoader\MainWindow.xaml
```

当前菜单使用固定 `MenuItem` 和固定 `ICommand`，无法根据配置增删或排序。

### 5.2 地图节点菜单

当前文件：

```text
VSLoader\Views\FactoryMapWindow.xaml.cs
```

浏览模式节点菜单由 `CreateDeviceContextMenu` 创建，操作通过：

```text
FactoryMapShortcutAction
executeShortcutAction
MainWindow.ExecuteShortcutActionFromMap
```

转发给主窗口。

地图编辑模式则使用 `CreateTopologyDeviceContextMenu`，包含拓扑编辑操作。本次必须保持该菜单不变。

### 5.3 现有内建执行服务

必须复用：

```text
VSCodeLauncherService
WebUiService
AdminUiService
MainViewModel 中现有 AdminUI 下载、编辑、删除逻辑
```

不得为了统一菜单而把这些成熟逻辑改写成 PowerShell 字符串。

### 5.4 配置边界

工作区配置当前保存在：

```text
AppConfig
```

全局配置导出通过 `GlobalConfigPackageService.CloneWorkspaceConfig` 克隆当前工作区配置。

右键菜单能力集合应加入 `AppConfig`，因此：

```text
每个工作区拥有独立能力集合。
切换工作区后使用目标工作区自己的配置。
导出全局配置时包含当前工作区能力集合。
导入全局配置时只覆盖目标工作区，不污染其他工作区。
```

## 6. 能力分组

右键菜单分为两个语义区域。

### 6.1 业务能力区域

允许配置顺序和启用状态：

```text
VSCode
WebUI
AdminUI
获取AdminUI连接
用户自定义 PowerShell 能力
用户自定义 Web 能力
```

### 6.2 系统管理区域

固定显示在底部，并通过分隔线与业务能力隔开：

```text
编辑
删除
```

约束：

```text
1. 编辑和删除不允许被自定义脚本替换。
2. 删除继续使用危险操作样式和现有确认弹窗。
3. 第一版不允许把自定义能力拖到系统管理区域之后。
4. 地图编辑模式的拓扑菜单不显示业务能力区域。
```

## 7. 配置模型

### 7.1 根配置

新增：

```text
VSLoader\Models\ContextMenuCapabilityCollectionConfig.cs
```

模型定义：

```csharp
public sealed class ContextMenuCapabilityCollectionConfig
{
    public int SchemaVersion { get; set; } = 1;
    public List<ContextMenuCapabilityDefinition> Items { get; set; } = new();

    public ContextMenuCapabilityCollectionConfig Clone();
}
```

在 `AppConfig` 中新增：

```csharp
public ContextMenuCapabilityCollectionConfig ContextMenuCapabilities { get; set; } = new();
```

### 7.2 能力定义

新增：

```text
VSLoader\Models\ContextMenuCapabilityDefinition.cs
```

字段定义：

```csharp
public sealed class ContextMenuCapabilityDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string BuiltInActionId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
    public bool ShowInShortcutList { get; set; } = true;
    public bool ShowInFactoryMap { get; set; } = true;
    public bool ConfirmBeforeExecute { get; set; }
    public bool RequiresExistingTargetPath { get; set; } = true;
    public PowerShellCapabilityConfig PowerShell { get; set; } = new();
    public WebCapabilityConfig Web { get; set; } = new();

    public ContextMenuCapabilityDefinition Clone();
}
```

`Id` 规则：

```text
内建能力使用固定常量 ID。
自定义能力使用 Guid.NewGuid().ToString("N")。
显示名称修改时不得改变 ID。
复制能力时必须生成新 ID。
```

### 7.3 能力类型

新增：

```text
VSLoader\Models\ContextMenuCapabilityKinds.cs
```

取值：

```csharp
public const string BuiltIn = "builtIn";
public const string PowerShell = "powerShell";
public const string Web = "web";
```

未知类型必须：

```text
读取时保留原数据。
运行时禁用。
设置页显示“不支持的能力类型”。
不得导致整个配置加载失败。
```

### 7.4 内建能力 ID

新增：

```text
VSLoader\Models\ContextMenuBuiltInActionIds.cs
```

固定 ID：

```text
builtin.open-vscode
builtin.open-webui
builtin.open-adminui
builtin.download-adminui-link
```

内建能力允许：

```text
启用或停用。
调整业务能力区域内的顺序。
控制是否出现在主界面和地图浏览模式。
```

内建能力不允许：

```text
修改 Kind。
修改 BuiltInActionId。
删除定义。
编辑底层执行内容。
```

## 8. 默认能力集合

新增：

```text
VSLoader\Models\Services\ContextMenuCapabilityDefaults.cs
```

默认顺序必须与当前程序保持一致：

```text
1. VSCode
2. WebUI
3. AdminUI
4. 获取AdminUI连接
```

旧配置没有 `ContextMenuCapabilities` 或 `Items` 为空时：

```text
自动创建默认集合。
不弹出阻断性错误。
保存工作区配置时持久化补齐结果。
```

配置已有部分内建能力但缺少其他内建能力时：

```text
保留用户现有顺序和启用状态。
将缺少的内建能力追加到业务能力区域末尾。
不得因为软件升级重置用户自定义能力。
```

## 9. 配置规范化和校验

新增：

```text
VSLoader\Models\Services\ContextMenuCapabilityConfigService.cs
```

职责：

```text
创建默认配置。
克隆配置。
规范化 ID、名称、类型、顺序和子配置。
补齐缺失内建能力。
校验设置页保存内容。
生成当前界面可见的有序能力列表。
```

规范化规则：

```text
1. Items 为 null 时替换为空集合并补齐默认能力。
2. 自定义能力 ID 为空或重复时生成新 GUID。
3. 内建能力重复时保留排序最前的一项，忽略其余重复项并记录警告。
4. Name 执行 Trim，空名称拒绝保存。
5. Order 重新归一化为 0、10、20……，避免长期拖动造成重复值。
6. 至少启用一个展示位置，否则拒绝保存。
7. ConfirmBeforeExecute 对内建能力不改变现有确认语义。
8. PowerShell 超时限制为 1 至 300 秒。
9. Web URL 模板必须通过专用模板校验。
10. 配置问题只禁用受影响能力，不得让整个右键菜单不可用。
```

## 10. 统一执行上下文

新增：

```text
VSLoader\Models\ContextMenuCapabilityExecutionContext.cs
VSLoader\Models\ContextMenuCapabilitySurfaces.cs
```

模型定义：

```csharp
public sealed class ContextMenuCapabilityExecutionContext
{
    public ShortcutItem Shortcut { get; init; } = new();
    public string WorkspaceId { get; init; } = string.Empty;
    public string WorkspaceDirectory { get; init; } = string.Empty;
    public string AppBaseDirectory { get; init; } = string.Empty;
    public string Surface { get; init; } = string.Empty;
}
```

`Surface` 取值：

```text
shortcutList
factoryMap
```

执行时必须显式传入 `ShortcutItem`。

禁止继续依赖：

```text
先把地图节点同步选中到主界面 DataGrid，再读取 MainViewModel.SelectedShortcut 执行。
```

地图执行能力时：

```text
使用被右键点击节点自身的 ShortcutItem。
不得因为主窗口搜索、排序、选中状态或可见性改变执行对象。
```

## 11. PowerShell 能力模型

新增：

```text
VSLoader\Models\PowerShellCapabilityConfig.cs
VSLoader\Models\PowerShellCapabilityExecutionModes.cs
```

字段定义：

```csharp
public sealed class PowerShellCapabilityConfig
{
    public string Script { get; set; } = string.Empty;
    public string WorkingDirectoryMode { get; set; } = "target";
    public string ExecutionMode { get; set; } = "visible";
    public int TimeoutSeconds { get; set; } = 30;
}
```

执行模式：

```text
visible    -> 显示 PowerShell 窗口，不捕获输出，不等待完成。
background -> 后台异步运行，捕获退出码、标准输出和错误输出，并应用超时。
```

工作目录模式：

```text
target       -> 目标路径是目录时使用目标路径，否则使用目标父目录。
targetParent -> 始终使用目标父目录。
workspace    -> 当前工作区目录。
app          -> VSLoader 程序目录。
```

`targetParent` 边界：

```text
目标是普通文件时使用其父目录。
目标是普通目录时使用该目录的父目录。
目标已经是盘符根目录或 UNC 共享根目录、无法取得更上级目录时，使用目标目录自身。
最终结果为空或不存在时拒绝执行。
```

工作目录不存在时：

```text
不得启动 PowerShell。
返回包含能力名称、工作目录模式和最终路径的错误信息。
```

## 12. PowerShell 执行方式

新增：

```text
VSLoader\Models\Services\PowerShellCapabilityExecutor.cs
```

### 12.1 可执行文件

优先使用：

```text
%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe
```

若文件不存在，可通过系统 PATH 查找 `powershell.exe`。

仍无法找到时：

```text
返回“未找到 Windows PowerShell”错误。
不得自动切换到 cmd.exe。
```

### 12.2 启动参数

统一参数：

```text
-NoLogo
-NoProfile
-NonInteractive
-EncodedCommand
```

不得默认附加：

```text
-ExecutionPolicy Bypass
```

脚本必须按 PowerShell 要求使用 UTF-16LE 编码后转换为 Base64，再通过 `-EncodedCommand` 传入，避免中文、双引号、单引号和换行转义问题。

参数必须使用 `ProcessStartInfo.ArgumentList`，不得拼接完整命令行字符串。

### 12.3 进程配置

可见模式：

```text
UseShellExecute = false
CreateNoWindow = false
不重定向标准输出和错误输出
启动成功即返回
```

可见模式只表示显示终端窗口，仍然使用 `-NonInteractive`，不支持依赖 `Read-Host` 等交互式输入的脚本。需要人工交互的脚本不属于第一版支持范围。

后台模式：

```text
UseShellExecute = false
CreateNoWindow = true
RedirectStandardOutput = true
RedirectStandardError = true
异步读取 stdout 和 stderr
异步等待退出
应用超时和 CancellationToken
```

禁止：

```text
在 UI 线程同步 WaitForExit。
先 WaitForExit 再读取 stdout/stderr。
无限制缓存命令输出。
```

输出限制：

```text
stdout 和 stderr 各最多保留 64 KB。
超出部分丢弃并在结果中标记“输出已截断”。
```

超时后：

```text
尝试终止整个进程树。
终止失败只记录错误，不得让 VSLoader 崩溃。
返回能力名称、超时时间和已捕获输出。
```

## 13. PowerShell 环境变量

执行前设置以下进程级环境变量：

```text
VSL_TARGET_PATH
VSL_TARGET_PARENT
VSL_SHORTCUT_NAME
VSL_DESCRIPTION
VSL_SOURCE_MODULE_NAME
VSL_WORKSPACE_ID
VSL_WORKSPACE_PATH
VSL_APP_BASE_PATH
VSL_SOURCE_SURFACE
```

示例：

```powershell
code $env:VSL_TARGET_PATH
```

```powershell
Get-ChildItem -LiteralPath $env:VSL_TARGET_PATH
```

```powershell
Start-Process explorer.exe -ArgumentList $env:VSL_TARGET_PATH
```

必须保证：

```text
1. 环境变量只写入新启动的 PowerShell 子进程。
2. 不修改 VSLoader 进程或系统级环境变量。
3. null 值转换为空字符串。
4. TargetParent 使用 Path.GetDirectoryName 的安全结果。
5. 不传递 AdminUI 密码、受保护密码、剪贴板内容或其他敏感配置。
6. 不对环境变量内容再次执行字符串替换。
```

## 14. PowerShell 安全和信任

自定义 PowerShell 能力拥有当前 Windows 用户权限，必须明确处理信任边界。

### 14.1 本地创建

用户第一次保存 PowerShell 能力时显示警告：

```text
PowerShell 命令可以读取、修改或删除当前用户有权限访问的文件，并可启动其他程序。请只保存你理解并信任的命令。
```

用户确认保存后，将该能力当前内容加入“待写入信任”集合。只有当前工作区配置成功保存后，才把对应哈希写入本机信任文件。配置保存失败时不得留下孤立信任记录。

### 14.2 导入配置

全局配置包可以携带能力定义，但不得携带有效的本机信任状态。

导入后的 PowerShell 能力第一次执行时必须展示：

```text
能力名称
完整脚本
工作目录模式
执行模式
来源：导入的全局配置
```

用户确认后才能执行。

### 14.3 信任存储

新增本机级信任文件：

```text
%AppData%\VSLoader\context-menu-capability-trust.json
```

信任键必须至少包含：

```text
能力 ID
能力安全相关内容的 SHA-256 哈希
```

哈希输入包括：

```text
Kind
Script
WorkingDirectoryMode
ExecutionMode
RequiresExistingTargetPath
```

脚本或执行参数修改后哈希变化，必须重新确认。

信任文件：

```text
不得加入 AppConfig。
不得导出到全局配置包。
不得由导入文件写入。
损坏时按全部未信任处理。
```

### 14.4 执行前确认

`ConfirmBeforeExecute = true` 时，每次执行都显示确认弹窗。

它与首次信任确认是两种不同语义：

```text
信任确认 -> 判断是否允许该脚本在本机执行。
执行确认 -> 判断本次是否真的运行。
```

不得用一次确认同时跳过两层约束。

## 15. Web 能力模型

新增：

```text
VSLoader\Models\WebCapabilityConfig.cs
VSLoader\Models\Services\WebCapabilityExecutor.cs
VSLoader\Models\Services\ContextMenuUrlTemplateService.cs
```

模型定义：

```csharp
public sealed class WebCapabilityConfig
{
    public string UrlTemplate { get; set; } = string.Empty;
}
```

第一版始终使用系统默认浏览器：

```csharp
Process.Start(new ProcessStartInfo(url)
{
    UseShellExecute = true
});
```

不保存 Chrome、Edge 等浏览器绝对路径，避免全局配置导入到其他电脑后失效。

## 16. Web 模板变量

支持：

```text
{TargetPath}
{TargetParent}
{ShortcutName}
{Description}
{SourceModuleName}
{WorkspaceId}
{WorkspacePath}
```

示例：

```text
https://example.com/search?name={ShortcutName}
```

模板处理规则：

```text
1. 变量匹配区分明确名称，不允许任意反射读取对象属性。
2. 每个变量值使用 Uri.EscapeDataString 编码后替换。
3. 未知变量拒绝保存。
4. 缺少右花括号或模板格式错误时拒绝保存。
5. 替换完成后必须通过 Uri.TryCreate(..., UriKind.Absolute)。
6. 仅允许 http 和 https。
7. 生成 URL 长度超过 8192 字符时拒绝执行。
8. URL 为空时拒绝执行。
```

Web 能力不提供“跳过编码”的变量形式，避免路径中的 `&`、`?`、`#` 改变模板结构。

## 17. 统一执行调度

新增：

```text
VSLoader\Models\Services\ContextMenuCapabilityExecutionService.cs
VSLoader\Models\ContextMenuCapabilityExecutionResult.cs
```

职责：

```text
校验能力是否启用。
校验当前 Surface 是否允许展示和执行。
检查目标路径要求。
处理信任确认和每次执行确认。
按 Kind 分发给内建、PowerShell 或 Web 执行器。
捕获全部预期和非预期异常。
返回统一成功、失败、取消或已启动状态。
```

结果必须包含：

```csharp
public bool Success { get; init; }
public bool Cancelled { get; init; }
public bool Started { get; init; }
public int? ExitCode { get; init; }
public string Message { get; init; } = string.Empty;
public string StandardOutput { get; init; } = string.Empty;
public string StandardError { get; init; } = string.Empty;
public bool OutputTruncated { get; init; }
```

执行失败弹窗必须包含：

```text
能力名称
能力类型
目标快捷项名称
目标路径
失败阶段
退出码（如有）
错误输出（如有）
异常消息
```

不得显示：

```text
AdminUI 密码
受保护密码字段
与当前能力无关的配置内容
```

## 18. 内建能力调度

内建能力继续调用现有逻辑：

```text
builtin.open-vscode              -> VSCodeLauncherService
builtin.open-webui               -> WebUiService
builtin.open-adminui             -> AdminUiService 和现有自动登录协调逻辑
builtin.download-adminui-link    -> 现有单项 AdminUI 连接下载逻辑
```

禁止把现有能力改写成：

```text
code.exe 的 PowerShell 模板
浏览器 URL 的静态模板
AdminUI 启动命令字符串
```

原因：

```text
现有能力包含路径预检、properties 解析、网络错误处理、AdminUI 自动登录等业务逻辑，不能因菜单配置化而丢失。
```

## 19. 主界面菜单接入

修改：

```text
VSLoader\MainWindow.xaml
VSLoader\ViewModels\MainViewModel.cs
```

主界面 DataGrid 不再静态写死四个业务能力菜单项。

新增：

```text
ObservableCollection<ContextMenuCapabilityMenuItemViewModel> ShortcutContextMenuCapabilities
ExecuteContextMenuCapabilityCommand
```

菜单打开时必须：

```text
1. 使用右键命中的 DataGridRow 对应 ShortcutItem。
2. 将该行设置为 SelectedShortcut，保持现有视觉反馈。
3. 生成 Surface = shortcutList 的执行上下文。
4. 按能力集合顺序显示已启用能力。
5. 在业务能力后添加 Separator、编辑和删除。
```

不得只依赖旧的 `PlacementTarget.DataContext.SelectedShortcut`，否则右键未选中行时可能执行到旧选中项。

## 20. 地图节点菜单接入

修改：

```text
VSLoader\Views\FactoryMapWindow.xaml.cs
VSLoader\MainWindow.xaml.cs
```

浏览模式：

```text
CreateDeviceContextMenu 不再写死 VSCode、WebUI、AdminUI 和获取AdminUI连接。
从统一能力提供器读取 ShowInFactoryMap = true 的已启用能力。
点击后把 device.Shortcut 和 Surface = factoryMap 直接传给统一执行调度。
```

编辑和删除继续放在系统管理区域。

地图窗口不得为了执行能力而：

```text
激活主窗口。
把主窗口显示到前台。
依赖主窗口 DataGrid 当前选择。
改变地图窗口层级。
```

地图编辑模式：

```text
CreateTopologyDeviceContextMenu 保持拓扑语义。
仍只显示开始连接、断开连接等地图操作。
不混入 PowerShell、Web 或其他快捷项业务能力。
```

## 21. 设置界面

修改：

```text
VSLoader\Views\SettingsWindow.xaml
VSLoader\Views\SettingsWindow.xaml.cs
VSLoader\ViewModels\SettingsViewModel.cs
```

设置页新增独立区域：

```text
右键菜单能力集合
```

区域采用列表式布局，不使用卡片嵌套。

每行显示：

```text
启用开关
能力名称
类型：内建 / PowerShell / Web
展示位置：快捷项 / 地图
上移
下移
编辑
复制
删除
```

交互规则：

```text
1. 上移、下移使用图标按钮并提供 ToolTip。
2. 第一版使用明确的上移/下移，不依赖复杂拖放实现。
3. 内建能力的编辑入口只允许修改启用状态、顺序和展示位置。
4. 内建能力不显示删除按钮。
5. 自定义能力可以编辑、复制和删除。
6. 删除自定义能力前确认。
7. 列表底部提供“新增 PowerShell”“新增 Web”“恢复默认顺序”。
8. 恢复默认顺序只重排内建能力，不删除自定义能力。
9. 设置点击取消时，不修改当前工作区配置和运行时菜单。
10. 设置点击保存并通过校验后，一次写入配置并刷新两个菜单。
```

“恢复默认顺序”的确定性规则：

```text
1. 内建能力恢复为 VSCode、WebUI、AdminUI、获取AdminUI连接。
2. 自定义能力保持彼此之间的原相对顺序。
3. 自定义能力统一排列在内建能力之后。
4. 不改变任何能力的启用状态和展示位置。
```

### 21.1 PowerShell 编辑内容

```text
能力名称
显示位置
脚本多行编辑框
工作目录模式
执行模式
后台超时秒数
目标路径必须存在
每次执行前确认
可用环境变量只读列表
```

脚本编辑框必须支持：

```text
多行输入。
等宽字体。
垂直和水平滚动。
Tab 不直接切换焦点时，应提供可替代缩进方式或保留普通焦点导航。
不自动修改用户脚本文本。
```

### 21.2 Web 编辑内容

```text
能力名称
显示位置
URL 模板
目标路径必须存在
每次执行前确认
可用模板变量只读列表
```

保存前显示一条使用示例数据生成的 URL 预览，但预览不得自动打开浏览器。

## 22. 配置保存和实时刷新

`SettingsViewModel` 必须接收能力集合的克隆副本。

保存顺序：

```text
1. Trim 和规范化能力配置。
2. 校验全部能力。
3. 处理新增或修改 PowerShell 能力的安全警告，并暂存待信任哈希。
4. 现有设置项继续执行原校验。
5. SettingsWindow 返回成功。
6. MainViewModel 将克隆后的配置写回 _config。
7. ConfigService 保存当前工作区。
8. 工作区配置保存成功后写入暂存的本机信任哈希。
9. 主界面菜单刷新。
10. 已打开地图窗口刷新能力提供器或下一次打开菜单时读取最新快照。
```

若配置保存失败：

```text
不得只刷新内存菜单。
保留旧运行时配置。
显示配置文件路径和失败原因。
```

## 23. 全局配置导入导出

修改：

```text
VSLoader\Models\Services\GlobalConfigPackageService.cs
```

`CloneWorkspaceConfig` 必须包含：

```csharp
ContextMenuCapabilities = config.ContextMenuCapabilities.Clone()
```

导入时：

```text
1. 规范化能力配置。
2. 补齐缺失内建能力。
3. 保留支持的自定义 PowerShell 和 Web 能力。
4. 未知类型保留但禁用，并加入导入警告。
5. 清除或忽略任何试图携带的信任信息。
6. 提示导入包包含多少个 PowerShell 能力。
7. 第一次执行导入的 PowerShell 能力时要求本机信任确认。
```

全局配置包 `SchemaVersion` 本次保持当前值 `1`。

原因：

```text
ContextMenuCapabilities 是 WorkspaceConfig 中新增的可选字段。
新版本读取旧包时会自动补齐默认能力集合。
旧版本读取新包时会忽略未知字段，现有快捷项、设置和地图仍可导入。
旧版本不会保留自定义能力，属于向旧版本回退时的预期能力降级，不得影响其他配置内容。
```

## 24. 配置损坏和兼容策略

### 24.1 旧配置

缺少能力集合：

```text
使用默认能力集合。
现有四个业务能力和编辑、删除继续可用。
```

### 24.2 单项能力损坏

例如：

```text
Kind 未知。
PowerShell 脚本为空。
Web 模板无效。
ID 重复。
Order 重复。
```

处理：

```text
能规范化的自动规范化。
不能安全修复的能力禁用。
其他能力继续加载。
设置页显示具体警告。
```

### 24.3 整体能力集合损坏

反序列化失败或 SchemaVersion 不支持时：

```text
保留原 config.json 的现有损坏备份机制。
能力集合回退到默认值。
不得导致主程序启动失败。
```

## 25. 并发和性能

```text
1. 右键菜单打开时只读取和排序内存配置，不读磁盘。
2. 不扫描系统 PowerShell 进程。
3. 不在后台定时执行能力检查。
4. 每次点击只启动一个对应执行任务。
5. 同一能力对同一快捷项正在后台执行时，默认拒绝重复启动并提示“正在执行”。
6. 不同能力可以并行执行，但每个后台进程独立超时和取消。
7. PowerShell 输出使用有界缓存。
8. 关闭程序时取消仍在等待的后台任务，并尝试终止由 VSLoader 明确管理的进程树。
9. 可见终端模式属于独立外部进程，启动成功后不由 VSLoader 强制关闭。
10. 菜单生成不得阻塞地图渲染和主界面滚动。
```

## 26. 日志

能力执行日志统一写入一份文件：

```text
%AppData%\VSLoader\logs\context-menu-capability.log
```

沿用项目日志约束：

```text
最多保留最新 2000 条。
写日志失败不得影响能力执行。
```

记录：

```text
时间
能力 ID
能力名称
能力类型
Surface
快捷项名称
目标路径
执行阶段
启动结果
退出码
耗时
是否超时
错误摘要
```

不得记录：

```text
AdminUI 密码
受保护密码
剪贴板内容
完整 PowerShell 脚本正文
完整 stdout/stderr
```

脚本只记录 SHA-256 哈希，错误输出只记录截断后的摘要。

## 27. 新增文件

### 27.1 模型

```text
VSLoader\Models\ContextMenuCapabilityCollectionConfig.cs
VSLoader\Models\ContextMenuCapabilityDefinition.cs
VSLoader\Models\ContextMenuCapabilityKinds.cs
VSLoader\Models\ContextMenuBuiltInActionIds.cs
VSLoader\Models\ContextMenuCapabilityExecutionContext.cs
VSLoader\Models\ContextMenuCapabilityExecutionResult.cs
VSLoader\Models\ContextMenuCapabilitySurfaces.cs
VSLoader\Models\PowerShellCapabilityConfig.cs
VSLoader\Models\PowerShellCapabilityExecutionModes.cs
VSLoader\Models\WebCapabilityConfig.cs
```

### 27.2 服务

```text
VSLoader\Models\Services\ContextMenuCapabilityDefaults.cs
VSLoader\Models\Services\ContextMenuCapabilityConfigService.cs
VSLoader\Models\Services\ContextMenuCapabilityExecutionService.cs
VSLoader\Models\Services\PowerShellCapabilityExecutor.cs
VSLoader\Models\Services\WebCapabilityExecutor.cs
VSLoader\Models\Services\ContextMenuUrlTemplateService.cs
VSLoader\Models\Services\ContextMenuCapabilityTrustService.cs
VSLoader\Models\Services\ContextMenuCapabilityLogService.cs
```

### 27.3 ViewModel

```text
VSLoader\ViewModels\ContextMenuCapabilityListItemViewModel.cs
VSLoader\ViewModels\ContextMenuCapabilityEditorViewModel.cs
VSLoader\ViewModels\ContextMenuCapabilityMenuItemViewModel.cs
```

### 27.4 视图

复杂编辑内容使用独立弹窗，避免继续扩大 `SettingsWindow.xaml`：

```text
VSLoader\Views\ContextMenuCapabilityEditorWindow.xaml
VSLoader\Views\ContextMenuCapabilityEditorWindow.xaml.cs
```

## 28. 修改文件

```text
VSLoader\Models\AppConfig.cs
VSLoader\Models\Services\ConfigService.cs
VSLoader\Models\Services\GlobalConfigPackageService.cs
VSLoader\ViewModels\SettingsViewModel.cs
VSLoader\ViewModels\MainViewModel.cs
VSLoader\Views\SettingsWindow.xaml
VSLoader\Views\SettingsWindow.xaml.cs
VSLoader\MainWindow.xaml
VSLoader\MainWindow.xaml.cs
VSLoader\Views\FactoryMapWindow.xaml.cs
VSLoader\Styles\ModernTheme.xaml
```

`FactoryMapShortcutAction` 在全部调用方迁移后应删除，避免保留两套菜单动作模型。

删除前必须通过 `rg` 确认无调用者，不得留下无用兼容代码。

## 29. 测试文件

新增：

```text
VSLoader.Tests\ContextMenuCapabilityConfigServiceTests.cs
VSLoader.Tests\ContextMenuCapabilityExecutionServiceTests.cs
VSLoader.Tests\PowerShellCapabilityExecutorTests.cs
VSLoader.Tests\ContextMenuUrlTemplateServiceTests.cs
VSLoader.Tests\ContextMenuCapabilityTrustServiceTests.cs
VSLoader.Tests\ContextMenuCapabilityLogServiceTests.cs
VSLoader.Tests\ContextMenuCapabilityMenuIntegrationTests.cs
```

更新：

```text
VSLoader.Tests\GlobalConfigPackageServiceTests.cs
VSLoader.Tests\FactoryMapShortcutActionTests.cs
VSLoader.Tests\MainWindowProductVisualTests.cs
VSLoader.Tests\AllWindowsProductVisualTests.cs
```

若 `FactoryMapShortcutAction` 被删除，对应测试应迁移为统一能力菜单集成测试，不得简单删除覆盖。

## 30. 测试要求

### 30.1 默认和迁移

```text
旧 AppConfig 自动生成四个默认内建能力。
默认顺序与当前菜单一致。
缺少一个内建能力时只补齐缺失项。
重复内建能力被规范化。
自定义能力 ID 重复时安全修复。
未知 Kind 被禁用且其他能力继续可用。
设置取消不修改原配置。
```

### 30.2 PowerShell 配置

```text
空脚本拒绝保存。
超时小于 1 或大于 300 被拒绝或规范化。
工作目录模式未知时拒绝保存。
执行模式未知时拒绝保存。
环境变量不包含密码和敏感配置。
中文、空格、单引号和 UNC 路径通过环境变量保持原值。
EncodedCommand 可以还原为原始脚本。
```

### 30.3 PowerShell 执行

```text
后台成功执行返回 ExitCode = 0。
非零退出码返回失败和错误输出。
stdout/stderr 超过上限时被截断。
超时后尝试终止进程树。
取消后不弹出错误崩溃。
找不到 powershell.exe 时返回可追溯错误。
目标路径要求存在但路径缺失时不启动进程。
可见模式启动后不阻塞 UI。
同一能力同一快捷项重复点击被拒绝。
```

### 30.4 Web 模板

```text
全部支持变量可以正确替换。
变量值执行 URL 编码。
未知变量拒绝保存。
http 和 https 可以执行。
javascript、data、file 和自定义协议被拒绝。
相对 URL 被拒绝。
生成 URL 超过 8192 字符被拒绝。
系统默认浏览器启动失败时返回错误。
```

### 30.5 信任

```text
本地创建并确认后的当前哈希可以执行。
修改脚本后旧信任失效。
导入能力第一次执行必须确认。
导入包中的伪造信任字段无效。
信任文件损坏时按未信任处理。
取消信任确认时不启动进程。
ConfirmBeforeExecute 仍单独生效。
```

### 30.6 菜单一致性

```text
主界面和地图浏览模式按同一顺序显示业务能力。
ShowInShortcutList = false 时主界面不显示。
ShowInFactoryMap = false 时地图不显示。
禁用能力不显示。
编辑和删除始终位于业务能力之后。
删除继续使用危险样式。
地图编辑模式不显示自定义能力。
地图节点执行时使用被右键节点，而不是主窗口旧选中项。
地图执行能力不会显示或激活主窗口。
```

### 30.7 全局配置

```text
导出包含完整能力集合。
导入恢复顺序、启用状态、展示位置和模板。
导入不恢复本机信任。
旧全局配置缺少能力集合时自动使用默认值。
能力配置无效时只产生警告，不破坏快捷项和地图导入。
```

## 31. 验证命令

执行目录：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader
```

目标测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ContextMenuCapability|FullyQualifiedName~PowerShellCapability|FullyQualifiedName~GlobalConfigPackage"
```

全量测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore
```

Debug 构建：

```powershell
dotnet build .\VSLoader.sln -c Debug --no-restore
```

要求：

```text
测试全部通过。
构建 0 错误。
不得新增编译警告。
```

## 32. 人工验收

```text
1. 打开设置，确认存在独立“右键菜单能力集合”区域。
2. 调整 VSCode、WebUI、AdminUI 顺序并保存。
3. 主界面右键快捷项，确认顺序立即变化。
4. 地图浏览模式右键节点，确认顺序与主界面一致。
5. 禁用 WebUI，确认两个菜单都按展示位置配置隐藏。
6. 新增 PowerShell 能力，脚本读取 $env:VSL_TARGET_PATH 并打开目标目录。
7. 使用包含中文、空格和网络路径的快捷项执行，确认路径无截断和转义错误。
8. 新增后台 PowerShell 能力，确认界面不冻结且能显示非零退出码错误。
9. 新增 Web 能力，确认中文名称和路径被正确 URL 编码。
10. 配置 javascript: URL，确认保存被拒绝。
11. 在地图节点执行能力，确认主窗口不会突然显示到前台。
12. 切换地图编辑模式，确认节点菜单仍是拓扑操作，不显示自定义能力。
13. 导出全局配置并导入另一工作区，确认能力定义被恢复。
14. 首次执行导入的 PowerShell 能力，确认显示脚本和信任警告。
15. 修改已信任脚本，确认再次要求信任。
16. 删除或损坏一项能力配置，确认其他菜单能力仍可使用。
17. 切换到另一个工作区，确认不会继承当前工作区能力集合。
18. 点击编辑和删除，确认继续沿用原业务逻辑和确认弹窗。
```

## 33. 验收标准

全部满足才算完成：

```text
1. 主界面与地图浏览模式共享一套业务能力集合。
2. 能力集合属于工作区，不污染其他工作区。
3. 现有四个内建能力行为无回归。
4. 用户可以调整顺序、启用状态和展示位置。
5. 用户可以新增 PowerShell 和 Web 能力。
6. PowerShell 使用系统 powershell.exe 和环境变量安全传值。
7. PowerShell 执行不阻塞 UI，并具备超时和有界输出。
8. Web 模板只允许 http/https 并正确编码变量。
9. 导入的命令不能携带本机信任状态。
10. 编辑、删除继续保持受保护系统语义。
11. 地图拓扑菜单不受本次改造影响。
12. 地图节点执行能力不依赖主窗口选择和层级状态。
13. 配置损坏或单项执行失败不会导致程序崩溃。
14. 全局配置导入导出完整支持能力集合。
15. 不保留 FactoryMapShortcutAction 等无用旧分发代码。
16. 目标测试、全量测试和 Debug 构建全部通过。
```

本次改造的核心原则：

```text
右键菜单负责展示能力；执行上下文负责提供目标数据；执行器负责完成动作；配置和信任边界负责保证可控。
```
