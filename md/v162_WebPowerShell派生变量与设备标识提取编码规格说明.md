# v162 Web/PowerShell 派生变量与设备标识提取编码规格说明

## 1. 背景

v161 已实现工作区级右键菜单能力集合，并为 Web 和 PowerShell 能力提供基础变量。

当前 Web 模板支持：

```text
{TargetPath}
{TargetParent}
{ShortcutName}
{Description}
{SourceModuleName}
{WorkspaceId}
{WorkspacePath}
```

但这些变量只能整体替换，不能从目标路径中取得具有业务含义的内部字段。

例如目标路径：

```text
\\192.168.15.69\instances\3134_TSSP001
```

用户实际可能需要：

```text
目标末级名称：3134_TSSP001
实例编号：3134
设备 ID：TSSP001
设备类型：TSSP
设备序号：001
```

目前用户只能把完整 `{TargetPath}` 传入 URL，无法直接构造：

```text
https://example.com/device?id=TSSP001
```

项目中已经存在 `FactoryMapDeviceCodeParser`，地图节点会从目标路径末级名称中提取 `TSSP001`。如果 Web、PowerShell 和地图分别实现字符串拆分，会产生规则不一致、异常边界不同和后续维护困难的问题。

## 2. 核心结论

本次必须采用“统一目标标识解析器 + 受控派生变量”方案：

```text
1. 新增通用目标标识解析器，统一解析目标末级名称、实例编号和设备 ID。
2. 地图现有设备代码解析改为委托给统一解析器，不保留第二套规则。
3. Web 模板新增 5 个派生变量。
4. PowerShell 同步新增对应的 5 个环境变量。
5. 设备 ID 固定按“英文字母 + 数字”解析，例如 TSSP001。
6. 设备序号保持原始字符串，不得把 001 转成 1。
7. 解析仅处理字符串，不访问磁盘或网络路径，不受网络连通性影响。
8. Web 引用了无法提取的派生变量时必须拒绝打开，并返回可追溯错误。
9. PowerShell 即使派生失败也允许执行，对应环境变量传入空字符串。
10. 本次不开放任意正则、脚本表达式或通用字符串过滤器。
11. 旧 Web 模板、旧 PowerShell 脚本和全局配置必须保持兼容。
12. 任意非法、空白或异常路径不得导致 VSLoader 崩溃。
```

## 3. 目标

本次必须实现：

```text
1. 用户可以在 Web URL 模板中直接使用 {DeviceCode} 得到 TSSP001。
2. 用户可以分别使用实例编号、设备类型和设备序号。
3. 用户可以取得目标路径最后一个目录或文件名称。
4. Web 和 PowerShell 使用完全一致的派生结果。
5. 地图节点显示的设备代码继续使用同一套解析结果。
6. Web 编辑器帮助区展示全部新增变量、中文说明和可复制案例。
7. PowerShell 编辑器帮助区展示对应环境变量和案例。
8. Web 实时预览使用符合业务格式的示例目标路径。
9. 派生失败时错误信息必须指出变量名、目标末级名称和格式要求。
10. 不修改工作区配置 Schema，不要求迁移已有配置文件。
```

## 4. 非目标

本次不做：

```text
1. 不支持用户在模板中编写正则表达式。
2. 不支持 {TargetName|AfterLast:_} 等过滤器或管道语法。
3. 不支持任意字符串截取、替换、大小写转换和条件表达式。
4. 不新增 JavaScript、C# 表达式或其他脚本执行能力。
5. 不把 URL 模板变成完整编程语言。
6. 不读取目标目录中的 XML、JSON 或其他文件来生成派生变量。
7. 不检查 UNC 路径是否在线，不因服务器离线阻止纯字符串解析。
8. 不修改快捷项 `TargetPath`、`Name`、`Description` 等原始数据。
9. 不把派生结果持久化到 `config.json`，避免产生冗余和过期数据。
10. 不改变现有 URL 自动编码、协议白名单和 8192 字符限制。
```

## 5. 新增变量

### 5.1 Web 模板变量

新增：

```text
{TargetName}
{InstanceId}
{DeviceCode}
{DeviceType}
{DeviceNumber}
```

示例输入：

```text
TargetPath = \\192.168.15.69\instances\3134_TSSP001
```

解析结果：

```text
{TargetName}   -> 3134_TSSP001
{InstanceId}   -> 3134
{DeviceCode}   -> TSSP001
{DeviceType}   -> TSSP
{DeviceNumber} -> 001
```

### 5.2 PowerShell 环境变量

同步新增：

```text
VSL_TARGET_NAME
VSL_INSTANCE_ID
VSL_DEVICE_CODE
VSL_DEVICE_TYPE
VSL_DEVICE_NUMBER
```

使用示例：

```powershell
Write-Output $env:VSL_DEVICE_CODE
```

```powershell
Start-Process "https://example.com/device?id=$env:VSL_DEVICE_CODE"
```

### 5.3 命名约束

必须使用上述固定名称，不增加同义别名，例如：

```text
禁止同时提供 {DeviceId} 和 {DeviceCode}。
禁止同时提供 {No} 和 {DeviceNumber}。
```

统一语义：

```text
DeviceCode 表示完整设备 ID，例如 TSSP001。
DeviceType 表示设备 ID 中的字母部分，例如 TSSP。
DeviceNumber 表示设备 ID 中的数字部分，例如 001。
InstanceId 表示目标末级名称最后一个下划线之前的纯数字实例编号。
TargetName 表示目标路径最后一个目录或文件名称。
```

## 6. 统一目标标识模型

新增：

```text
VSLoader\Models\ShortcutTargetIdentity.cs
```

建议模型：

```csharp
public sealed class ShortcutTargetIdentity
{
    public string TargetName { get; init; } = string.Empty;
    public string InstanceId { get; init; } = string.Empty;
    public string DeviceCode { get; init; } = string.Empty;
    public string DeviceType { get; init; } = string.Empty;
    public string DeviceNumber { get; init; } = string.Empty;
}
```

约束：

```text
1. 所有属性始终为非 null 字符串。
2. 模型只表达解析结果，不持有 ShortcutItem 引用。
3. 模型不负责 URL 编码和环境变量命名。
4. 解析失败使用空字符串表达，不通过异常表达普通格式不匹配。
```

## 7. 统一目标标识解析器

新增：

```text
VSLoader\Models\Services\ShortcutTargetIdentityParser.cs
```

建议接口：

```csharp
public static class ShortcutTargetIdentityParser
{
    public static ShortcutTargetIdentity Parse(string? targetPath);
}
```

### 7.1 解析步骤

必须按以下顺序执行：

```text
1. null 转为空字符串并执行 Trim。
2. 移除末尾的 \ 和 /，但不得修改路径内部字符。
3. 使用 Path.GetFileName 取得目标末级名称。
4. TargetName 保存完整末级名称。
5. 查找 TargetName 中最后一个下划线。
6. 存在下划线时，下划线之后作为设备 ID 候选值，下划线之前作为实例编号候选值。
7. 不存在下划线时，完整 TargetName 作为设备 ID 候选值，实例编号候选值为空。
8. 设备 ID 必须匹配 ^(?<Type>[A-Za-z]+)(?<Number>[0-9]+)$。
9. 匹配成功后写入 DeviceCode、DeviceType 和 DeviceNumber。
10. 实例编号候选值仅在完全匹配 ^[0-9]+$ 时写入 InstanceId。
11. 任意 Path API 异常必须捕获并返回空结果，不得向 UI 抛出。
```

### 7.2 大小写和前导零

解析器必须保留输入原值：

```text
3134_TSSP001 -> DeviceType = TSSP, DeviceNumber = 001
3134_tssp001 -> DeviceType = tssp, DeviceNumber = 001
```

不得强制转换大小写，因为 URL 查询参数可能区分大小写。

不得把 `DeviceNumber` 转成整数，否则会丢失前导零。

### 7.3 多下划线边界

设备代码始终取最后一个下划线之后的内容：

```text
line_a_TSSP001 -> DeviceCode = TSSP001
```

但 `InstanceId` 仅允许纯数字，因此：

```text
line_a_TSSP001 -> InstanceId = 空字符串
```

这样既保持现有地图设备代码提取能力，又不把任意前缀错误标记为实例编号。

### 7.4 不依赖路径存在性

解析器禁止调用：

```text
File.Exists
Directory.Exists
Directory.GetFiles
网络访问 API
```

原因：

```text
1. Web 模板预览不应因为网络路径离线而卡顿。
2. UNC 路径即使暂时不可访问，仍然可以从字符串中提取设备 ID。
3. 右键菜单打开和能力编辑不得引入额外 I/O。
```

## 8. 解析结果边界表

| 输入 | TargetName | InstanceId | DeviceCode | DeviceType | DeviceNumber |
|---|---|---|---|---|---|
| `\\server\instances\3134_TSSP001` | `3134_TSSP001` | `3134` | `TSSP001` | `TSSP` | `001` |
| `C:\instances\5924_TSSP002\` | `5924_TSSP002` | `5924` | `TSSP002` | `TSSP` | `002` |
| `C:\instances\3134_tssp001` | `3134_tssp001` | `3134` | `tssp001` | `tssp` | `001` |
| `C:\instances\line_a_TSSP001` | `line_a_TSSP001` | 空 | `TSSP001` | `TSSP` | `001` |
| `C:\instances\3134_TSSP` | `3134_TSSP` | `3134` | 空 | 空 | 空 |
| `C:\instances\3134_001` | `3134_001` | `3134` | 空 | 空 | 空 |
| `C:\instances\TSSP001` | `TSSP001` | 空 | `TSSP001` | `TSSP` | `001` |
| 空字符串或 null | 空 | 空 | 空 | 空 | 空 |

对于不带下划线但自身符合设备代码格式的末级名称，例如 `TSSP001`：

```text
DeviceCode、DeviceType 和 DeviceNumber 允许正常解析。
InstanceId 保持为空。
```

这与现有 `FactoryMapDeviceCodeParser` 的行为保持兼容。

## 9. 地图解析兼容

当前文件：

```text
VSLoader\Models\Services\FactoryMapDeviceCodeParser.cs
```

不得继续保留独立正则和字符串拆分逻辑。

修改为兼容包装：

```csharp
public static string Parse(string? targetPath)
{
    return ShortcutTargetIdentityParser.Parse(targetPath).DeviceCode;
}
```

要求：

```text
1. 现有地图节点设备代码展示结果不得改变。
2. 现有调用方不需要立即修改方法签名。
3. 后续可以逐步让新代码直接使用 ShortcutTargetIdentityParser。
4. FactoryMapDeviceCodeParser 中不得残留第二份 DeviceCodeRegex。
```

## 10. 变量服务改造

修改：

```text
VSLoader\Models\Services\ContextMenuCapabilityVariableService.cs
```

`BuildTemplateVariables` 必须只调用一次解析器：

```csharp
var identity = ShortcutTargetIdentityParser.Parse(shortcut.TargetPath);
```

然后追加：

```csharp
["TargetName"] = identity.TargetName,
["InstanceId"] = identity.InstanceId,
["DeviceCode"] = identity.DeviceCode,
["DeviceType"] = identity.DeviceType,
["DeviceNumber"] = identity.DeviceNumber
```

`BuildEnvironmentVariables` 追加：

```csharp
["VSL_TARGET_NAME"] = values["TargetName"],
["VSL_INSTANCE_ID"] = values["InstanceId"],
["VSL_DEVICE_CODE"] = values["DeviceCode"],
["VSL_DEVICE_TYPE"] = values["DeviceType"],
["VSL_DEVICE_NUMBER"] = values["DeviceNumber"]
```

约束：

```text
1. Web 和 PowerShell 必须从同一个 identity 实例生成结果。
2. 不得在 BuildEnvironmentVariables 中再次解析路径。
3. 原有变量名称和值不得变化。
4. 不得向环境变量加入 AdminUI 密码或其他敏感数据。
```

## 11. Web 模板失败策略

修改：

```text
VSLoader\Models\Services\ContextMenuUrlTemplateService.cs
```

### 11.1 必须阻止的情况

模板实际引用以下变量之一，但解析结果为空时，必须返回失败：

```text
{TargetName}
{InstanceId}
{DeviceCode}
{DeviceType}
{DeviceNumber}
```

原因：Web 能力通常会把这些字段作为服务端查询主键。静默替换为空字符串可能打开错误设备、错误页面或无意义查询结果。

### 11.2 错误信息

例如模板使用 `{DeviceCode}`，但路径是：

```text
C:\instances\3134_TSSP
```

返回：

```text
无法从目标末级名称“3134_TSSP”提取变量 {DeviceCode}。设备 ID 应为英文字母加数字，例如 TSSP001。
```

模板使用 `{InstanceId}` 但前缀不是纯数字时：

```text
无法从目标末级名称“line_a_TSSP001”提取变量 {InstanceId}。实例编号必须是最后一个下划线之前的纯数字内容。
```

要求：

```text
1. 错误必须包含无法生成的变量名。
2. 错误必须包含安全截断后的 TargetName，不输出超长路径全文。
3. 错误必须说明期望格式并给出一个示例。
4. 不得启动默认浏览器。
5. 不得因为一个能力执行失败而关闭主窗口或地图窗口。
```

### 11.3 现有可空变量

以下现有字段继续允许替换为空字符串：

```text
{Description}
{SourceModuleName}
```

不得把新派生字段的严格规则扩散到所有旧变量，避免破坏旧模板。

## 12. PowerShell 失败策略

PowerShell 环境变量属于原始执行上下文，不对脚本用途作假设。

因此派生失败时：

```text
VSL_TARGET_NAME      -> 空字符串
VSL_INSTANCE_ID      -> 空字符串
VSL_DEVICE_CODE      -> 空字符串
VSL_DEVICE_TYPE      -> 空字符串
VSL_DEVICE_NUMBER    -> 空字符串
```

PowerShell 能力仍然允许启动。

用户可以自行判断：

```powershell
if ([string]::IsNullOrWhiteSpace($env:VSL_DEVICE_CODE)) {
    throw "当前快捷项无法提取设备 ID。"
}
```

不得在 PowerShell 执行器中增加另一套解析或阻断规则。

## 13. Web 编辑器帮助区

修改：

```text
VSLoader\Views\ContextMenuCapabilityEditorWindow.xaml
```

在现有 Web 变量说明中追加：

```text
{TargetName}   当前目标路径最后一个目录或文件名称。
{InstanceId}   目标末级名称最后一个下划线之前的纯数字实例编号。
{DeviceCode}   设备完整 ID，例如 TSSP001。
{DeviceType}   设备 ID 的英文字母部分，例如 TSSP。
{DeviceNumber} 设备 ID 的数字部分，例如 001；保留前导零。
```

新增可复制案例：

```text
# 使用完整设备 ID 打开页面
https://example.com/device?id={DeviceCode}

# 分别传递设备类型和设备序号
https://example.com/device?type={DeviceType}&number={DeviceNumber}

# 同时传递实例编号和设备 ID
https://example.com/device?instance={InstanceId}&device={DeviceCode}

# 使用目标末级名称进行查询
https://example.com/search?target={TargetName}
```

帮助区必须明确说明：

```text
1. DeviceCode 格式为英文字母加数字。
2. DeviceNumber 保留前导零。
3. 派生变量无法提取时，Web 能力不会打开错误页面，而是显示具体原因。
4. 变量值继续自动进行 URL 编码。
```

## 14. PowerShell 编辑器帮助区

修改同一窗口的 PowerShell 变量说明，追加：

```text
$env:VSL_TARGET_NAME
$env:VSL_INSTANCE_ID
$env:VSL_DEVICE_CODE
$env:VSL_DEVICE_TYPE
$env:VSL_DEVICE_NUMBER
```

增加案例：

```powershell
# 使用设备 ID 调用已有脚本
& "C:\Scripts\ProcessDevice.ps1" `
    -InstanceId $env:VSL_INSTANCE_ID `
    -DeviceCode $env:VSL_DEVICE_CODE
```

PowerShell 帮助区必须注明：派生失败时环境变量为空，脚本可按自身需求判断或回退。

## 15. 实时预览示例

修改：

```text
VSLoader\ViewModels\ContextMenuCapabilityEditorViewModel.cs
```

当前预览示例路径应改为符合真实业务格式的字符串：

```csharp
TargetPath = @"\\192.168.15.69\instances\3134_TSSP001"
```

推荐同时设置：

```csharp
Name = "示例设备_001"
Description = "示例设备 3134_TSSP001"
SourceModuleName = "eap-sic-Example"
```

这样用户输入：

```text
https://example.com/device?id={DeviceCode}
```

预览必须显示：

```text
https://example.com/device?id=TSSP001
```

预览失败时继续展示 `ContextMenuUrlTemplateService` 返回的中文错误，不弹出阻断性对话框。

## 16. URL 编码和安全边界

新增派生变量继续使用现有规则：

```text
1. 每个变量值单独使用 Uri.EscapeDataString 编码。
2. 固定 URL 结构不参与变量编码。
3. 不提供 Raw、NoEncode 或跳过编码语法。
4. 仅允许 http 和 https。
5. 替换后的 URL 必须是绝对地址。
6. URL 最长 8192 个字符。
7. 未知变量、括号错误和不支持协议继续拒绝执行。
```

解析器不得解释 URL，也不得把路径内容直接拼接到 URL；URL 编码职责仍然只属于 `ContextMenuUrlTemplateService`。

## 17. 配置和兼容性

本次不新增配置字段。

现有 Web 配置仍然只是：

```json
{
  "UrlTemplate": "https://example.com/device?id={DeviceCode}"
}
```

兼容规则：

```text
1. 旧模板只使用旧变量时行为完全不变。
2. 旧 PowerShell 脚本不需要修改。
3. 新环境变量只是追加，不覆盖现有环境变量。
4. 全局配置导入导出无需修改 Schema。
5. 新模板导入旧版本 VSLoader 时会被旧版本识别为未知变量，这是版本能力差异，不做向下兼容改写。
6. 当前版本加载新模板后必须正常预览、校验和执行。
7. 不把解析结果写入 config.json 或全局配置包。
```

## 18. 性能要求

```text
1. 单次解析时间复杂度为 O(n)，n 为目标路径字符串长度。
2. 每次构建变量只解析一次目标路径。
3. 不访问文件系统和网络。
4. 不引入后台线程、定时器或缓存。
5. 不在右键菜单打开时提前批量解析全部快捷项。
6. 仅在生成执行上下文变量或实时预览时按需解析当前快捷项。
7. 异常路径处理不得写高频日志。
```

派生变量计算量只涉及路径末级名称和一次正则匹配，不会对地图渲染、菜单打开或主界面滚动造成可感知影响。

## 19. 错误处理

必须覆盖：

```text
空 TargetPath。
只有盘符或 UNC 根路径。
目标路径以斜杠结尾。
目标末级名称没有下划线。
下划线后为空。
设备代码只有字母或只有数字。
设备代码包含空格、横线、中文或特殊字符。
实例编号不是纯数字。
路径包含 Path.GetFileName 无法处理的非法字符。
模板引用无法提取的派生变量。
```

处理原则：

```text
解析器返回空字段，不抛异常。
Web 模板引用空派生字段时返回中文失败结果。
PowerShell 传入空环境变量并继续执行。
错误不得触发默认浏览器。
错误不得导致设置窗口、主窗口或地图窗口退出。
```

## 20. 新增文件

```text
VSLoader\Models\ShortcutTargetIdentity.cs
VSLoader\Models\Services\ShortcutTargetIdentityParser.cs
VSLoader.Tests\ShortcutTargetIdentityParserTests.cs
```

## 21. 修改文件

```text
VSLoader\Models\Services\FactoryMapDeviceCodeParser.cs
VSLoader\Models\Services\ContextMenuCapabilityVariableService.cs
VSLoader\Models\Services\ContextMenuUrlTemplateService.cs
VSLoader\ViewModels\ContextMenuCapabilityEditorViewModel.cs
VSLoader\Views\ContextMenuCapabilityEditorWindow.xaml
VSLoader.Tests\ContextMenuUrlTemplateServiceTests.cs
VSLoader.Tests\PowerShellCapabilityExecutorTests.cs
VSLoader.Tests\ContextMenuCapabilityEditorViewModelTests.cs
```

如果现有地图解析测试直接覆盖 `FactoryMapDeviceCodeParser`，必须保留并更新为验证委托后的兼容行为，不得简单删除。

## 22. 测试要求

### 22.1 统一解析器

必须测试：

```text
标准 UNC 路径正确解析 3134、TSSP001、TSSP、001。
本地路径与 UNC 路径结果一致。
末尾斜杠不影响结果。
小写设备代码保留原始大小写。
设备序号保留前导零。
多个下划线时 DeviceCode 使用最后一段，InstanceId 为空。
不带下划线的 TSSP001 仍能解析 DeviceCode。
只有字母、只有数字、中文和特殊字符返回空设备字段。
空字符串和 null 返回全空结果。
非法路径不抛异常。
解析过程不依赖目标路径真实存在。
```

### 22.2 地图兼容

必须测试：

```text
FactoryMapDeviceCodeParser.Parse 标准路径仍返回 TSSP001。
地图节点设备代码显示结果不变。
FactoryMapDeviceCodeParser 不再保存独立正则实现。
```

### 22.3 Web 变量

必须测试：

```text
全部 5 个新增变量正确替换。
多个新旧变量可以在同一 URL 中组合。
新变量值继续执行 URL 编码。
{DeviceCode} 生成 TSSP001。
{DeviceNumber} 生成 001 而不是 1。
模板引用无法提取的 {DeviceCode} 时返回格式错误且不打开浏览器。
模板引用无法提取的 {InstanceId} 时返回实例编号错误。
未引用空派生变量时不影响其他模板执行。
Description 和 SourceModuleName 为空时仍保持旧行为。
未知变量和不支持协议的现有测试继续通过。
```

### 22.4 PowerShell 变量

必须测试：

```text
5 个新增 VSL_ 环境变量值正确。
设备序号前导零保持不变。
派生失败时对应环境变量为空字符串。
派生失败不阻止 PowerShell 能力启动。
旧环境变量值不变。
环境变量只写入子进程。
```

### 22.5 编辑器帮助和预览

必须测试：

```text
Web 帮助区包含 5 个新增变量和中文说明。
Web 帮助区包含 DeviceCode、DeviceType、DeviceNumber 示例。
PowerShell 帮助区包含 5 个新增环境变量。
预览示例路径使用 3134_TSSP001。
输入 {DeviceCode} 后预览包含 TSSP001。
界面帮助区域仍可滚动，按钮不被内容挤出窗口。
```

## 23. 推荐 TDD 实施顺序

```text
1. 先为 ShortcutTargetIdentityParser 编写失败测试。
2. 实现最小解析模型和解析器，使解析测试通过。
3. 为 FactoryMapDeviceCodeParser 兼容委托编写测试并改造。
4. 为 ContextMenuCapabilityVariableService 新增变量编写失败测试。
5. 实现 Web 和 PowerShell 变量扩充。
6. 为 Web 空派生变量失败策略编写测试。
7. 实现 ContextMenuUrlTemplateService 的派生变量校验。
8. 为编辑器帮助和预览编写 XAML/ViewModel 测试。
9. 更新编辑器帮助内容和预览示例。
10. 运行目标测试、全量测试和 Debug 构建。
```

不得先修改生产代码再补测试。

## 24. 验证命令

执行目录：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader
```

目标测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ShortcutTargetIdentityParser|FullyQualifiedName~ContextMenuUrlTemplate|FullyQualifiedName~PowerShellCapability|FullyQualifiedName~ContextMenuCapabilityEditor"
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
目标测试全部通过。
全量测试全部通过。
Debug 构建 0 错误、0 新增警告。
```

## 25. 人工验收

```text
1. 新增 Web 能力，模板填写 https://example.com/device?id={DeviceCode}。
2. 对目标路径为 \\192.168.15.69\instances\3134_TSSP001 的快捷项执行。
3. 确认浏览器打开的 URL 参数为 id=TSSP001。
4. 改为 type={DeviceType}&number={DeviceNumber}，确认得到 TSSP 和 001。
5. 使用 instance={InstanceId}，确认得到 3134。
6. 使用 target={TargetName}，确认得到经过 URL 编码的 3134_TSSP001。
7. 对不符合格式的目标使用 {DeviceCode}，确认不启动浏览器并显示具体原因。
8. 新增 PowerShell 能力输出 $env:VSL_DEVICE_CODE，确认值为 TSSP001。
9. 对不符合格式的目标运行 PowerShell，确认环境变量为空但程序不崩溃。
10. 打开工厂地图，确认节点设备代码展示与改造前一致。
11. 导出并导入全局配置，确认包含新变量的 URL 模板保持原文。
12. 断开网络后编辑和预览 UNC 路径模板，确认界面不因路径解析卡顿。
```

## 26. 验收标准

全部满足才算完成：

```text
1. Web 能力可以直接使用 {DeviceCode} 取得 TSSP001。
2. TargetName、InstanceId、DeviceType 和 DeviceNumber 均按规格生成。
3. Web 和 PowerShell 使用同一套解析结果。
4. 地图设备代码解析不再维护第二套规则。
5. 设备序号前导零不会丢失。
6. 路径解析不访问磁盘和网络。
7. Web 引用无法提取的派生变量时不会打开错误页面。
8. PowerShell 派生失败时使用空环境变量且不被强制阻断。
9. 旧 Web 模板、旧 PowerShell 脚本和旧配置行为不变。
10. 编辑器帮助和实时预览清楚展示新增能力。
11. 任意异常路径不会导致程序崩溃。
12. 不引入任意正则或模板表达式语言。
13. 不持久化派生结果，不增加配置 Schema。
14. 目标测试、全量测试和 Debug 构建全部通过。
```

本次设计的核心原则：

```text
路径只解析一次，业务字段由统一解析器产生；Web 负责安全替换，PowerShell 负责自由使用，地图继续复用同一设备语义。
```
