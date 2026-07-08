# v132 全局配置包更新检测替代 rules 和 map 检测编码规格说明

## 1. 背景

当前 VSLoader 已经具备“导出全局配置 / 导入全局配置”能力。

一份全局配置包已经包含当前工作区可共享的主要配置：

```text
快捷项列表
AdminUI 配置
WebUI 配置
批量新增相关路径
更新检测相关路径
地图布局
主窗口快捷键
地图快捷键
软件更新 manifest 路径
```

因此继续分别提示：

```text
rules 批量规则文件需要更新
map 地图配置文件需要更新
```

会让用户困惑，也会让更新检测逻辑过碎。

本次需求是：

```text
停用 rules/map 文件级更新检测。
新增全局配置包更新检测。
软件版本更新检测保持不变。
```

最终用户只需要关心两类更新：

```text
1. 全局配置
2. 软件版本
```

## 2. 当前代码现状

### 2.1 当前设置项

文件：

```text
VSLoader/Models/UpdateCheckConfig.cs
```

当前字段：

```csharp
public sealed class UpdateCheckConfig
{
    public string RulesFilePath { get; set; } = string.Empty;
    public string MapFilePath { get; set; } = string.Empty;
    public string SoftwareVersionFilePath { get; set; } = string.Empty;
}
```

其中 `SoftwareVersionFilePath` 已经是旧字段，当前软件版本检测实际使用程序级：

```text
AppSettings.SoftwareUpdateManifestPath
```

### 2.2 当前基线状态

文件：

```text
VSLoader/Models/UpdateTimeState.cs
```

当前字段：

```csharp
public sealed class UpdateTimeState
{
    public UpdateFileState Rules { get; set; } = new();
    public UpdateFileState Map { get; set; } = new();
    public UpdateSoftwareState Software { get; set; } = new();
}
```

当前 rules/map 使用文件修改时间做基线：

```text
LastUsedWriteTimeUtc
```

### 2.3 当前检测逻辑

文件：

```text
VSLoader/Models/Services/UpdateCheckService.cs
```

当前 `Check(...)` 会检测：

```text
config.RulesFilePath -> 批量规则文件
config.MapFilePath -> 地图配置文件
softwareUpdateManifestPath -> 软件版本
```

当前黄色横幅可能显示：

```text
检测到更新：批量规则文件
检测到更新：地图配置文件
检测到更新：软件版本
```

本次需要改成：

```text
检测到更新：全局配置
检测到更新：软件版本
```

## 3. 总体目标

### 3.1 用户目标

用户不再看到 rules/map 这类底层文件更新提示。

用户只看到：

```text
检测到更新：全局配置
检测到更新：软件版本
检测到更新：全局配置、软件版本
```

如果全局配置包路径不可访问，显示红色失败横幅：

```text
更新检测失败：全局配置包不可访问：xxx
```

软件版本更新提示和更新软件按钮行为保持现状。

### 3.2 技术目标

1. `UpdateCheckConfig` 新增全局配置包路径。
2. 设置页“更新检测”区域移除 rules/map 两个输入框，改为全局配置包路径输入框。
3. 后台更新检测不再检测 rules/map。
4. 后台更新检测新增全局配置包检测。
5. 全局配置包检测采用“文件修改时间快筛 + ExportedAt 精准判断”的混合策略。
6. `updateTime.json` 新增全局配置包基线。
7. 旧的 Rules/Map 基线字段保留兼容，但不再参与检测和提示。
8. 导入全局配置成功后，更新全局配置包基线。
9. 点击黄色横幅“我知道了”后，写入当前检测到的全局配置包基线。
10. 软件版本更新检测保持不变。

## 4. 新数据模型

### 4.1 UpdateCheckConfig

文件：

```text
VSLoader/Models/UpdateCheckConfig.cs
```

新增：

```csharp
public string GlobalConfigPackagePath { get; set; } = string.Empty;
```

保留旧字段：

```csharp
public string RulesFilePath { get; set; } = string.Empty;
public string MapFilePath { get; set; } = string.Empty;
public string SoftwareVersionFilePath { get; set; } = string.Empty;
```

但旧字段变成兼容字段：

```text
不在设置页展示。
不参与后台更新检测。
不参与更新提醒。
不因为不存在产生失败提醒。
```

`Clone()` 必须带上 `GlobalConfigPackagePath`。

### 4.2 UpdateTimeState

文件：

```text
VSLoader/Models/UpdateTimeState.cs
```

新增：

```csharp
public UpdateGlobalConfigState GlobalConfig { get; set; } = new();
```

新增状态类：

```csharp
public sealed class UpdateGlobalConfigState
{
    public DateTime? LastSeenWriteTimeUtc { get; set; }

    public DateTimeOffset? LastUsedExportedAt { get; set; }
}
```

字段含义：

```text
LastSeenWriteTimeUtc:
用于文件系统快筛，避免每 10 分钟都读取并解析 JSON。

LastUsedExportedAt:
用于判断用户是否已经知道或使用过这份全局配置包。
```

保留旧字段：

```csharp
public UpdateFileState Rules { get; set; } = new();
public UpdateFileState Map { get; set; } = new();
```

旧字段不删除，避免旧 `updateTime.json` 反序列化、回写时出现兼容风险。

## 5. 全局配置包检测算法

### 5.1 基本输入

输入：

```text
GlobalConfigPackagePath
updateTime.json
```

输出：

```text
UpdatedItems 添加 “全局配置”
Failures 添加 “全局配置包不可访问：xxx”
DetectedGlobalConfigExportedAt
DetectedGlobalConfigWriteTimeUtc
```

### 5.2 快筛逻辑

先执行路径预检：

```text
PathAccessPreflightService.CheckFileAsync(GlobalConfigPackagePath)
```

如果失败：

```text
Failures.Add("全局配置包不可访问：xxx")
不再继续读取 JSON。
```

如果成功：

```text
var writeTimeUtc = File.GetLastWriteTimeUtc(GlobalConfigPackagePath)
```

如果：

```text
state.GlobalConfig.LastSeenWriteTimeUtc == writeTimeUtc
```

则不读取 JSON，直接认为本次没有全局配置更新。

### 5.3 精准判断逻辑

只有当文件修改时间变化，或者没有历史 `LastSeenWriteTimeUtc` 时，才读取 JSON。

读取并解析：

```text
GlobalConfigPackage
```

必须校验：

```text
AppName == "VSLoader"
SchemaVersion 支持
ExportedAt 非空且可解析为 DateTimeOffset
```

如果校验失败：

```text
Failures.Add("全局配置包格式无效：xxx")
```

如果可解析：

```text
exportedAt = DateTimeOffset.Parse(package.ExportedAt)
```

判断：

```text
如果 LastUsedExportedAt 为空：
    初始化 LastUsedExportedAt = exportedAt
    初始化 LastSeenWriteTimeUtc = writeTimeUtc
    不提示更新

如果 exportedAt > LastUsedExportedAt:
    UpdatedItems.Add("全局配置")
    DetectedGlobalConfigExportedAt = exportedAt
    DetectedGlobalConfigWriteTimeUtc = writeTimeUtc

如果 exportedAt <= LastUsedExportedAt:
    更新 LastSeenWriteTimeUtc = writeTimeUtc
    不提示更新
```

### 5.4 为什么不用纯文件修改时间

纯文件修改时间性能最好，但容易误报：

```text
文件复制到共享目录。
服务器同步。
备份恢复。
文件被重新保存但内容没有变化。
```

这些情况可能导致 LastWriteTime 变化，但全局配置实际并没有新版本。

### 5.5 为什么不用每次直接读 ExportedAt

直接读 `ExportedAt` 语义最准确，但每次都要：

```text
打开网络文件
读取 JSON
解析 JSON
```

虽然当前 40KB 左右的配置包性能压力很小，但网络共享路径可能存在延迟。

因此采用混合策略：

```text
文件修改时间没变 -> 不读 JSON
文件修改时间变了 -> 读取 ExportedAt 做最终判断
```

## 6. UpdateCheckResult 扩展

文件：

```text
VSLoader/Models/Services/UpdateCheckResult.cs
```

新增：

```csharp
public DateTimeOffset? DetectedGlobalConfigExportedAt { get; set; }

public DateTime? DetectedGlobalConfigWriteTimeUtc { get; set; }
```

保留旧字段：

```csharp
public DateTime? DetectedRulesWriteTimeUtc { get; set; }
public DateTime? DetectedMapWriteTimeUtc { get; set; }
```

旧字段暂时保留，避免测试和历史逻辑一次性大拆。

后续可以在确认稳定后再清理。

## 7. AcknowledgeDetectedUpdates 逻辑

文件：

```text
VSLoader/Models/Services/UpdateCheckService.cs
```

当前关闭黄色横幅会写入：

```text
Rules.LastUsedWriteTimeUtc
Map.LastUsedWriteTimeUtc
Software.LastUsedVersion
```

本次改为支持：

```text
GlobalConfig.LastUsedExportedAt
GlobalConfig.LastSeenWriteTimeUtc
Software.LastUsedVersion
```

规则：

```text
如果 result.DetectedGlobalConfigExportedAt 不为空：
    state.GlobalConfig.LastUsedExportedAt = result.DetectedGlobalConfigExportedAt

如果 result.DetectedGlobalConfigWriteTimeUtc 不为空：
    state.GlobalConfig.LastSeenWriteTimeUtc = result.DetectedGlobalConfigWriteTimeUtc

如果 result.DetectedSoftwareVersion 不为空：
    state.Software.LastUsedVersion = result.DetectedSoftwareVersion
```

旧 rules/map detected 字段可以保留兼容，但新检测不再产生它们。

## 8. 导入全局配置成功后的基线更新

文件：

```text
VSLoader/ViewModels/MainViewModel.cs
VSLoader/Models/Services/UpdateCheckService.cs
```

当前导入全局配置成功后：

```text
写入当前工作区 config.json
写入当前工作区 factory-map.layout.json
保存 AppSettings
LoadConfig()
TryRegisterImportedHotkey(...)
显示导入结果
```

本次需要增加：

```text
导入成功后，将被导入的全局配置包标记为已使用。
```

建议新增方法：

```csharp
public SaveResult MarkGlobalConfigUsed(string packagePath, string updateTimePath)
```

行为：

```text
1. 检查 packagePath 存在。
2. 读取文件 LastWriteTimeUtc。
3. 读取并解析 GlobalConfigPackage.ExportedAt。
4. 写入 updateTime.json:
   - GlobalConfig.LastSeenWriteTimeUtc
   - GlobalConfig.LastUsedExportedAt
5. 如果失败，显示红色更新失败横幅：
   更新检测失败：全局配置基线更新失败：xxx
```

这样用户实际导入后，就不会继续被提示同一份全局配置有更新。

## 9. 设置页调整

文件：

```text
VSLoader/Views/SettingsWindow.xaml
VSLoader/ViewModels/SettingsViewModel.cs
```

### 9.1 软件更新区域

保持不变：

```text
软件更新
- manifest 路径
```

### 9.2 更新检测区域

当前：

```text
更新检测
- rules 批量配置文件
- map 地图配置文件
```

改为：

```text
更新检测
- 全局配置包路径
```

绑定：

```text
UpdateCheck.GlobalConfigPackagePath
```

按钮：

```text
浏览
```

文件过滤：

```text
JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*
```

### 9.3 ViewModel

`SettingsViewModel` 需要：

```text
1. 保存时 Trim GlobalConfigPackagePath。
2. 浏览按钮设置 GlobalConfigPackagePath。
3. 旧 RulesFilePath / MapFilePath 不再由 UI 修改。
```

## 10. 主界面提醒文案

黄色横幅：

```text
检测到更新：全局配置
检测到更新：软件版本
检测到更新：全局配置、软件版本
```

红色横幅：

```text
更新检测失败：全局配置包不可访问：xxx
更新检测失败：全局配置包格式无效：xxx
更新检测失败：软件更新 manifest 不可访问：xxx
```

主动点击“检测更新”：

```text
没有更新且没有失败 -> 状态栏临时提示：已完成检测，当前没有发现更新。
有全局配置更新 -> 黄色横幅提示。
有软件版本更新 -> 黄色横幅提示。
有失败 -> 红色横幅提示。
```

## 11. 兼容策略

### 11.1 旧 config.json

旧配置没有 `GlobalConfigPackagePath` 时：

```text
默认为空。
不检测全局配置。
不报错。
```

### 11.2 旧 updateTime.json

旧文件没有 `GlobalConfig` 时：

```text
NormalizeState 自动补 new UpdateGlobalConfigState()
```

旧的 `Rules` 和 `Map` 字段继续保留。

### 11.3 旧全局配置导入导出

全局配置导入导出继续导出/导入 `UpdateCheckConfig`。

导出包中如果包含旧的：

```text
RulesFilePath
MapFilePath
```

可以保留，但不参与后续更新检测。

导出包中应包含新的：

```text
GlobalConfigPackagePath
```

## 12. 不做的事情

本次不做：

```text
1. 不删除 RulesFilePath 字段。
2. 不删除 MapFilePath 字段。
3. 不删除 UpdateTimeState.Rules。
4. 不删除 UpdateTimeState.Map。
5. 不改变软件更新按钮逻辑。
6. 不改变软件版本 manifest version 判断逻辑。
7. 不自动帮用户导入全局配置。
8. 不把全局配置包改成 zip。
9. 不在后台读取完整配置内容做差异比较。
```

## 13. 测试要求

### 13.1 UpdateCheckServiceTests

新增或调整：

```text
1. Missing_updateTime_initializes_global_config_without_update_notice
2. Global_config_package_new_exported_at_shows_update_notice
3. AcknowledgeDetectedUpdates_updates_global_config_baseline
4. MarkGlobalConfigUsed_updates_global_config_baseline
5. Global_config_unchanged_write_time_does_not_read_json
6. Global_config_write_time_changed_but_exported_at_same_does_not_show_update
7. Global_config_invalid_package_returns_failure
8. CheckAsync_does_not_check_rules_or_map_paths
9. Software_version_update_detection_still_works
```

旧 rules/map 更新提醒相关测试需要改造或删除：

```text
RulesFilePath 不再产生 “批量规则文件” 更新项。
MapFilePath 不再产生 “地图配置文件” 更新项。
RulesFilePath / MapFilePath 不存在不再产生失败提醒。
```

### 13.2 SettingsViewModelWebUiTests 或新增 SettingsViewModelUpdateCheckTests

新增：

```text
1. Save_trims_global_config_package_path
2. BrowseGlobalConfigPackage_sets_path
3. Save_keeps_legacy_rules_and_map_fields_without_ui_dependency
```

### 13.3 MainViewModelUpdateNoticeTests

调整：

```text
1. ApplyUpdateCheckResult_shows_global_config_and_software_update_notice
2. CloseUpdateNotice_acknowledges_global_config_baseline
3. ManualCheckUpdates_shows_no_update_status_when_global_config_and_software_have_no_updates
```

### 13.4 GlobalConfigPackageServiceTests

确认：

```text
1. Export includes UpdateCheck.GlobalConfigPackagePath.
2. Import preserves UpdateCheck.GlobalConfigPackagePath.
```

## 14. 验证命令

定向测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "UpdateCheckServiceTests|MainViewModelUpdateNoticeTests|SettingsViewModelWebUiTests|GlobalConfigPackageServiceTests" -p:BaseOutputPath=.\artifacts\test-output\ -p:UseSharedCompilation=false
```

全量测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore -p:BaseOutputPath=.\artifacts\test-output\ -p:UseSharedCompilation=false
```

Debug 构建：

```powershell
dotnet build .\VSLoader.sln -c Debug --no-restore -p:UseSharedCompilation=false
```

## 15. 验收标准

完成后必须满足：

```text
1. 设置页不再显示 rules/map 更新检测路径。
2. 设置页显示全局配置包路径。
3. 后台不再检测 rules/map 文件更新。
4. 后台不再因为 rules/map 文件不存在显示失败提醒。
5. 后台能检测全局配置包 ExportedAt 更新。
6. 文件修改时间没变时，不读取全局配置 JSON。
7. 文件修改时间变了但 ExportedAt 没变时，不显示更新。
8. ExportedAt 变新时，黄色横幅显示“检测到更新：全局配置”。
9. 点击“我知道了”后，同一份全局配置不再重复提示。
10. 成功导入全局配置后，同一份全局配置不再重复提示。
11. 软件版本更新检测和更新软件按钮行为不变。
12. 旧 config.json / updateTime.json 可正常加载。
13. 定向测试、全量测试、Debug 构建通过。
```

## 16. 实现顺序建议

```text
1. 扩展 UpdateCheckConfig / UpdateTimeState / UpdateCheckResult。
2. 给 UpdateCheckService 写全局配置包检测测试。
3. 实现全局配置包检测和基线写入。
4. 停用 rules/map 检测和失败提示。
5. 修改设置页 UI 和 SettingsViewModel。
6. 导入全局配置成功后调用 MarkGlobalConfigUsed。
7. 调整主界面提醒测试。
8. 跑定向测试。
9. 跑全量测试。
10. 构建 Debug。
```

