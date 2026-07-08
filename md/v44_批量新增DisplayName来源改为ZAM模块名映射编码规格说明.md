# v44 批量新增 DisplayName 来源改为 ZAM 模块名映射编码规格说明

## 1. 需求背景

当前 VSLoader 的“批量新增识别”会根据目标文件夹名称匹配 CSV 规则，并使用名称模板生成快捷项名称。

当前典型名称模板为：

```text
{DisplayName}_{No}
```

其中：

```text
DisplayName = CSV 规则中的 DisplayName
No = 从文件夹名正则捕获组中提取的编号
```

例如：

```text
文件夹名：12190_TAOI007
CSV DisplayName：3D-AOI
Regex 捕获 No：007
NameTemplate：{DisplayName}_{No}
生成名称：3D-AOI_007
```

现在需要调整名称生成算法，但采用最小修改策略：

```text
只替换 {DisplayName} 的来源。
{No} 仍然沿用原来的文件夹名正则捕获算法。
```

新的 `DisplayName` 来源为目标文件夹下：

```text
META-INF\ZAM-DEPLOY.xml
```

中的模块名。

## 2. 需求目标

在批量新增识别时，对每一个扫描到的目标子文件夹：

1. 仍然使用文件夹名匹配原有 CSV 规则。
2. 仍然从文件夹名正则捕获组中提取 `{No}`。
3. 新增读取：

```text
目标文件夹\META-INF\ZAM-DEPLOY.xml
```

4. 从 XML 中提取模块名，例如：

```xml
description="Application for eap-sic-Jutze-3D-AOI"
```

提取得到：

```text
eap-sic-Jutze-3D-AOI
```

5. 使用新的 CSV 正则规则，将模块名映射为新的 `DisplayName`。
6. 最终仍然使用原有 `NameTemplate`：

```text
{DisplayName}_{No}
```

生成快捷项名称。

最终算法变为：

```text
文件夹名 -> 原规则匹配 -> 提取 No
目标文件夹\META-INF\ZAM-DEPLOY.xml -> 提取模块名 -> CSV 正则映射 DisplayName
DisplayName + No -> NameTemplate -> 快捷项名称
```

## 3. 非目标范围

本次不实现：

1. 不改变批量新增识别入口。
2. 不新增按钮。
3. 不改变快捷项保存结构。
4. 不改变目标路径 `TargetPath`。
5. 不改变备注 `Description = 批量新增：{FolderName}`。
6. 不改变 `{No}` 的提取方式。
7. 不改变路径去重、可更新、可清理等既有逻辑。
8. 不实现复杂 XML 编辑器或规则编辑器。

## 4. 当前代码现状

核心代码位置：

```text
VSLoader/Models/Services/BatchImportService.cs
```

当前流程：

```csharp
var matchResult = FindMatchingRule(rules, folderName);
var generatedName = GenerateName(matchResult.Rule, folderName, matchResult.RegexMatch, out var nameError).Trim();
var sortNo = TryGetRegexNo(matchResult.RegexMatch);
```

当前 `GenerateName` 中直接使用：

```csharp
.Replace("{DisplayName}", rule.DisplayName, StringComparison.Ordinal)
```

当前 `No` 来源：

```csharp
RegexMatch.Groups["No"]
```

本次改造重点：

```text
GenerateName 中替换 {DisplayName} 时，优先使用从 ZAM-DEPLOY.xml 模块名映射得到的 DisplayName。
```

## 5. CSV 规则设计

为了最小改动，推荐仍然使用一份 CSV 文件，沿用当前表头：

```csv
MatchType,Pattern,DisplayName,NameTemplate
```

但需要对规则含义做扩展：

```text
Pattern 同时承担两个作用：
1. 继续匹配文件夹名，用于提取 No。
2. 如果当前规则匹配文件夹名成功，再尝试用同一条规则或同规则组匹配 ZAM 模块名。
```

不过从可维护性和清晰性考虑，更推荐引入新表头字段：

```csv
MatchType,Pattern,ModulePattern,DisplayName,NameTemplate
```

字段含义：

| 字段 | 含义 |
| --- | --- |
| MatchType | 文件夹名匹配类型，继续支持 Contains / Regex |
| Pattern | 文件夹名匹配规则，用于匹配文件夹名和提取 No |
| ModulePattern | ZAM 模块名匹配规则，支持正则表达式 |
| DisplayName | 当 ModulePattern 匹配成功后使用的显示名 |
| NameTemplate | 名称模板，例如 `{DisplayName}_{No}` |

本次推荐采用：

```text
新增 ModulePattern 字段。
```

原因：

1. `Pattern` 继续负责文件夹名和 `{No}`，不破坏原逻辑。
2. `ModulePattern` 专门负责匹配 XML 中的模块名。
3. 同一行规则可以同时描述“这类文件夹如何取编号”和“这个模块名映射成什么显示名”。
4. 用户只需要维护一份 CSV。

## 6. 新 CSV 示例

示例 CSV：

```csv
MatchType,Pattern,ModulePattern,DisplayName,NameTemplate
Regex,^(?<Code>\d+)_(?<Type>TAOI)(?<No>\d+)$,^eap-sic-Jutze-3D-AOI$,3D-AOI,{DisplayName}_{No}
Regex,^(?<Code>\d+)_(?<Type>TSSM)(?<No>\d+)$,^eap-sic-.*HotPress.*$,热贴机,{DisplayName}_{No}
Regex,^(?<Code>\d+)_(?<Type>TVF)(?<No>\d+)$,^eap-sic-.*Vertical.*Furnace.*$,垂直炉,{DisplayName}_{No}
```

如果目标文件夹为：

```text
12190_TAOI007
```

且：

```text
META-INF\ZAM-DEPLOY.xml
```

中存在：

```xml
description="Application for eap-sic-Jutze-3D-AOI"
```

则：

```text
Pattern 提取 No = 007
ModulePattern 匹配 eap-sic-Jutze-3D-AOI
DisplayName = 3D-AOI
NameTemplate = {DisplayName}_{No}
最终名称 = 3D-AOI_007
```

## 7. 向后兼容策略

为了避免旧 CSV 立刻全部失效，建议支持两种 CSV：

### 7.1 新格式

包含：

```text
MatchType,Pattern,ModulePattern,DisplayName,NameTemplate
```

行为：

```text
文件夹名必须匹配 Pattern。
模块名必须匹配 ModulePattern。
DisplayName 使用当前行 DisplayName。
```

### 7.2 旧格式

包含：

```text
MatchType,Pattern,DisplayName,NameTemplate
```

行为：

```text
沿用旧算法。
DisplayName 仍然直接使用 CSV 中的 DisplayName。
```

这样可以保证用户还没有准备新 CSV 时，旧规则仍然可用。

## 8. 模型改造

修改：

```text
VSLoader/Models/BatchImportRule.cs
```

新增字段：

```csharp
public string ModulePattern { get; set; } = string.Empty;
```

说明：

```text
ModulePattern 为空时表示旧格式规则。
ModulePattern 非空时表示启用 ZAM 模块名匹配。
```

## 9. CSV 读取与校验

修改：

```text
BatchImportService.LoadRules
BatchImportService.ValidateRule
```

### 9.1 表头校验

当前必需表头：

```text
MatchType,Pattern,DisplayName,NameTemplate
```

调整为：

```text
MatchType,Pattern,DisplayName,NameTemplate 必需
ModulePattern 可选
```

如果存在 `ModulePattern` 表头，则读取该字段。

### 9.2 ModulePattern 校验

如果 `ModulePattern` 不为空：

1. 必须是合法 Regex。
2. 使用 `RegexOptions.IgnoreCase`。
3. 正则错误时提示：

```text
第 X 行规则错误：ModulePattern Regex 语法无效：{错误信息}
```

### 9.3 DisplayName 校验

保持不变：

```text
DisplayName 不能为空。
```

## 10. ZAM-DEPLOY.xml 读取规则

新增辅助方法：

```csharp
private static string? TryReadZamModuleName(string targetDirectory, out string? errorMessage)
```

读取路径：

```csharp
Path.Combine(targetDirectory, "META-INF", "ZAM-DEPLOY.xml")
```

提取规则：

1. 优先使用 XML 解析方式读取根节点或任意节点上的 `description` 属性。
2. 找到包含：

```text
Application for 
```

的 description。
3. 提取 `Application for ` 后面的文本并 Trim。

示例：

```text
Application for eap-sic-Jutze-3D-AOI
```

提取：

```text
eap-sic-Jutze-3D-AOI
```

如果 XML 解析失败，可以作为规则错误返回，不建议用字符串硬切作为首选逻辑。

## 11. 模块名匹配规则

当前 `FindMatchingRule` 只接收：

```csharp
rules, folderName
```

建议扩展为：

```csharp
private static RuleMatchResult FindMatchingRule(
    IEnumerable<BatchImportRule> rules,
    string folderName,
    string? moduleName,
    out string? errorMessage)
```

匹配逻辑：

1. 先按原逻辑匹配文件夹名，得到候选规则。
2. 如果候选规则 `ModulePattern` 为空：
   - 认为是旧规则，直接匹配成功。
3. 如果候选规则 `ModulePattern` 非空：
   - 必须成功读取 `moduleName`。
   - `Regex.IsMatch(moduleName, rule.ModulePattern, RegexOptions.IgnoreCase)` 成功才匹配。
4. 如果文件夹名匹配成功，但模块名没有匹配任何规则：
   - 该文件夹应显示为 `已跳过` 或 `规则错误`。

推荐策略：

```text
ZAM 文件缺失、XML 无 description、模块名没有匹配 ModulePattern：显示 已跳过。
ModulePattern 正则语法错误：在 LoadRules 阶段作为 规则错误。
XML 文件存在但格式无法解析：显示 规则错误。
```

原因：

1. 文件缺失可能代表该目录不是目标应用目录，不应阻断整批导入。
2. XML 格式损坏属于数据异常，应明确提示。

## 12. GenerateName 改造

当前：

```csharp
private static string GenerateName(BatchImportRule rule, string folderName, Match? regexMatch, out string? errorMessage)
```

建议保持签名最小变化：

```csharp
private static string GenerateName(
    BatchImportRule rule,
    string folderName,
    Match? regexMatch,
    string? displayNameOverride,
    out string? errorMessage)
```

内部：

```csharp
var displayName = string.IsNullOrWhiteSpace(displayNameOverride)
    ? rule.DisplayName
    : displayNameOverride;
```

然后：

```csharp
.Replace("{DisplayName}", displayName, StringComparison.Ordinal)
```

对于新格式规则：

```text
displayNameOverride = rule.DisplayName
```

但这个 `rule.DisplayName` 是在 `ModulePattern` 成功匹配后才允许使用。

说明：

从代码层面看，可以不一定真的传 override，因为匹配成功的规则本身已经带有正确 `DisplayName`。

但建议保留这个扩展点，便于未来支持从 XML 捕获组动态生成 `DisplayName`。

## 13. 预览项显示建议

不新增主列表字段。

预览窗口中现有字段如果能显示 `MatchedPattern`，可以继续显示文件夹名 `Pattern`。

如果后续需要排查，可以考虑把模块名放入 `Message`：

```text
匹配模块：eap-sic-Jutze-3D-AOI
```

本次最小策略：

```text
不改预览 UI 字段结构。
只在错误/跳过信息中说明 ZAM 模块名相关原因。
```

## 14. 错误与跳过策略

### 14.1 ZAM 文件不存在

如果规则是旧格式：

```text
继续按旧算法执行。
```

如果规则需要 `ModulePattern`：

```text
已跳过：未找到 META-INF\ZAM-DEPLOY.xml，无法读取模块名。
```

### 14.2 description 不存在

```text
已跳过：ZAM-DEPLOY.xml 中未找到 Application for 模块描述。
```

### 14.3 模块名没有匹配规则

```text
已跳过：模块名未匹配任何规则：{ModuleName}
```

### 14.4 XML 格式错误

```text
规则错误：ZAM-DEPLOY.xml 解析失败：{错误信息}
```

### 14.5 ModulePattern 正则错误

在 CSV 加载阶段报错：

```text
第 X 行规则错误：ModulePattern Regex 语法无效：{错误信息}
```

## 15. 与现有功能的关系

### 15.1 No 提取

保持不变：

```text
Regex 捕获组 No 仍然来自文件夹名 Pattern。
```

### 15.2 排序

当前排序使用：

```csharp
SortRuleIndex
SortNo
SortName
```

保持不变。

`SortNo` 仍然来自文件夹名正则捕获组。

### 15.3 路径去重/更新/清理

保持不变。

只要新算法生成的名称和旧记录不同，既有 v40/v41/v42 逻辑会显示：

```text
可更新
可清理
已跳过
```

### 15.4 备注

保持不变：

```text
批量新增：{FolderName}
```

## 16. 测试要求

新增或扩展：

```text
VSLoader.Tests/BatchImportServicePathUpdateTests.cs
```

或新增：

```text
VSLoader.Tests/BatchImportServiceZamModuleTests.cs
```

推荐新增独立测试文件。

### 16.1 新格式规则根据 ZAM 模块名生成 DisplayName

目录：

```text
12190_TAOI007\META-INF\ZAM-DEPLOY.xml
```

XML：

```xml
<application description="Application for eap-sic-Jutze-3D-AOI" />
```

CSV 规则对象：

```text
Pattern = ^(?<Code>\d+)_(?<Type>TAOI)(?<No>\d+)$
ModulePattern = ^eap-sic-Jutze-3D-AOI$
DisplayName = 3D-AOI
NameTemplate = {DisplayName}_{No}
```

预期：

```text
GeneratedName = 3D-AOI_007
```

### 16.2 No 仍来自文件夹名

文件夹名：

```text
12190_TAOI007
```

模块名：

```text
eap-sic-Jutze-3D-AOI
```

预期：

```text
No = 007
GeneratedName = 3D-AOI_007
```

证明 `007` 不是从 XML 读取。

### 16.3 ZAM 文件缺失时新格式规则跳过

规则包含 `ModulePattern`，但目录没有：

```text
META-INF\ZAM-DEPLOY.xml
```

预期：

```text
Status = 已跳过
Message 包含 未找到 META-INF\ZAM-DEPLOY.xml
```

### 16.4 XML 格式错误时显示规则错误

ZAM 文件内容不是合法 XML。

预期：

```text
Status = 规则错误
Message 包含 ZAM-DEPLOY.xml 解析失败
```

### 16.5 旧格式规则仍可用

规则没有 `ModulePattern`。

预期：

```text
仍按原文件夹名规则生成名称。
```

## 17. 示例 CSV 文件建议

如果项目安装目录或 Assets 中已有规则示例文件，建议新增一份示例：

```text
Config\batch-rules-zam-example.csv
```

内容示例：

```csv
MatchType,Pattern,ModulePattern,DisplayName,NameTemplate
Regex,^(?<Code>\d+)_(?<Type>TAOI)(?<No>\d+)$,^eap-sic-Jutze-3D-AOI$,3D-AOI,{DisplayName}_{No}
Regex,^(?<Code>\d+)_(?<Type>TSSM)(?<No>\d+)$,^eap-sic-.*TSSM.*$,热贴机,{DisplayName}_{No}
```

本次如果只做代码功能，可以先不新增示例文件。

## 18. 预期改动文件

预计改动：

- `VSLoader/Models/BatchImportRule.cs`
- `VSLoader/Models/Services/BatchImportService.cs`
- `VSLoader.Tests/BatchImportServiceZamModuleTests.cs`

可能改动：

- `VSLoader/Config/*.csv`
- `VSLoader_安装包打包使用指南.md`

本次最小实现优先改前三个文件。

## 19. 构建与测试验证

实现完成后执行：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader
dotnet test .\VSLoader.sln -p:UseSharedCompilation=false
dotnet build .\VSLoader.sln -p:UseSharedCompilation=false
```

如果 `VSLoader.exe` 被占用，先关闭：

```powershell
Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force
```

最终要求：

- 测试全部通过。
- 构建 `0 个错误`。
- 不引入新的无关警告。

## 20. 最终效果

批量新增识别时，快捷项名称仍然保持：

```text
{DisplayName}_{No}
```

但：

```text
No 继续来自文件夹名正则捕获组。
DisplayName 改为根据目标文件夹中 ZAM-DEPLOY.xml 的模块名，通过 CSV 正则规则映射得到。
```

这样用户可以预先维护模块名到显示名的映射关系，例如：

```text
eap-sic-Jutze-3D-AOI -> 3D-AOI
```

最终生成：

```text
3D-AOI_007
```

同时保留现有批量新增、更新、路径去重、重复清理、排序和预览确认流程。
