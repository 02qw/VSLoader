# v90 软件更新检测统一使用 manifest 版本编码规格说明

## 1. 背景

当前 VSLoader 有两套“远端软件版本”来源：

1. 软件自动更新功能使用程序级配置：

```text
AppSettings.SoftwareUpdateManifestPath
```

对应设置页面中的：

```text
manifest 路径
```

该 manifest 文件中包含：

```json
{
  "version": "2.2.0",
  "packageFile": "VSLoader_2.2.0_win-x64.zip",
  "sha256": "...",
  "releaseNotes": "..."
}
```

2. 自动检测更新提醒功能使用工作区级配置：

```text
UpdateCheckConfig.SoftwareVersionFilePath
```

对应设置页面中的：

```text
软件版本 txt
```

该 txt 文件第一行只维护版本号。

这两套配置都表达“远端最新软件版本”，存在明显冗余：

1. 用户需要维护两个文件。
2. 两个文件版本不一致时，提醒和实际更新会割裂。
3. v89 全局配置导入导出已经会携带 `SoftwareUpdateManifestPath`，继续导出/校验软件版本 txt 会增加困惑。

因此 v90 要将软件版本检测统一到 manifest：

```text
自动检测软件版本更新
  ↓
读取 SoftwareUpdateManifestPath 指向的 manifest.json
  ↓
取 version 字段
  ↓
与当前程序版本比较
```

## 2. 总体目标

### 2.1 用户目标

用户只需要在设置中维护：

```text
manifest 路径
```

不再需要维护：

```text
软件版本 txt
```

软件版本提醒和软件自动更新按钮使用同一个 manifest 文件。

### 2.2 技术目标

1. 删除设置页面中的“软件版本 txt”UI。
2. 自动检测更新时，从 `SoftwareUpdateManifestPath` 读取 manifest 的 `version`。
3. 保留 v88 的“关闭提醒即确认该版本”逻辑。
4. 不再使用 `UpdateCheckConfig.SoftwareVersionFilePath` 参与检测。
5. v89 全局配置导入导出不再校验 `SoftwareVersionFilePath`。
6. 暂时保留 `UpdateCheckConfig.SoftwareVersionFilePath` 字段作为历史兼容字段。

## 3. 边界说明

### 3.1 删除 UI 不等于删除模型字段

本次删除：

```text
设置窗口中的“软件版本 txt”输入框和选择按钮
```

本次暂不删除：

```csharp
UpdateCheckConfig.SoftwareVersionFilePath
```

原因：

1. 旧用户的 `config.json` 里可能已经有这个字段。
2. v89 导出的旧全局配置包也可能包含这个字段。
3. 直接删除模型字段意义不大，反而增加兼容风险。

字段保留但不再使用：

```text
不展示
不检测
不校验
不提示
```

### 3.2 manifest 路径属于程序级配置

`SoftwareUpdateManifestPath` 位于：

```text
%AppData%\VSLoader\app-settings.json
```

它是程序级配置，影响所有工作区。

这是合理的，因为：

```text
软件更新是 VSLoader 程序本体能力，不属于某一个工作区。
```

工作区级配置仍然只负责：

```text
rules 文件检测路径
map 文件检测路径
```

### 3.3 不影响其他工作区数据

本次改动不会写其他工作区的 `config.json`。

变化只是：

```text
软件版本检测统一读取程序级 manifest。
```

其他工作区的快捷项、地图、rules/map 检测路径不受影响。

## 4. 现有代码问题点

### 4.1 UpdateCheckService

当前入口：

```csharp
public UpdateCheckResult Check(UpdateCheckConfig config, string updateTimePath, Version currentVersion)
```

内部软件检测：

```csharp
changed |= CheckSoftware(config.SoftwareVersionFilePath, state.Software, currentVersion, result);
```

当前 `CheckSoftware` 读取 txt 第一行：

```csharp
var versionText = File.ReadLines(filePath).FirstOrDefault()?.Trim();
```

v90 后不应再这样读取。

### 4.2 MainViewModel

当前后台检测：

```csharp
var updateCheckConfig = _config.UpdateCheck.Clone();
return _updateCheckService.Check(updateCheckConfig, updateTimePath, currentVersion);
```

v90 后需要同时传入：

```text
_appSettings.SoftwareUpdateManifestPath
```

### 4.3 SettingsViewModel / SettingsWindow

当前设置窗口仍有：

```text
软件版本 txt
```

并绑定：

```text
UpdateCheck.SoftwareVersionFilePath
```

v90 后需要删除该 UI。

### 4.4 GlobalConfigPackageService

v89 中导入预检会校验：

```csharp
config.UpdateCheck.SoftwareVersionFilePath
```

v90 后应取消这条校验。

全局配置包仍然携带：

```text
ProgramSettings.SoftwareUpdateManifestPath
```

这是新的软件版本检测来源。

## 5. UpdateCheckService 修改规格

### 5.1 Check 方法签名

建议将签名改为：

```csharp
public UpdateCheckResult Check(
    UpdateCheckConfig config,
    string updateTimePath,
    Version currentVersion,
    string softwareUpdateManifestPath)
```

调用逻辑：

```csharp
changed |= CheckFile(... rules ...);
changed |= CheckFile(... map ...);
changed |= CheckSoftwareManifest(softwareUpdateManifestPath, state.Software, currentVersion, result);
```

### 5.2 CheckSoftwareManifest 方法

新增私有方法：

```csharp
private bool CheckSoftwareManifest(
    string manifestPath,
    UpdateSoftwareState state,
    Version currentVersion,
    UpdateCheckResult result)
```

职责：

1. manifest 路径为空时，不提示更新，不报错。
2. manifest 路径非空但文件不存在时，加入失败：

```text
软件更新 manifest 文件不存在
```

3. manifest 无法读取/JSON 损坏时，加入失败：

```text
manifest 读取失败：xxx
```

4. `version` 为空时，加入失败：

```text
manifest version 为空
```

5. `version` 格式无效时，加入失败：

```text
manifest version 格式无效
```

6. version 大于当前程序版本时，提示：

```text
软件版本
```

7. version 等于当前程序版本时，更新 `Software.LastUsedVersion` 为当前版本。
8. version 小于当前程序版本时，不提示。

### 5.3 继续沿用 v88 已确认版本逻辑

如果：

```text
manifest.version > currentVersion
manifest.version <= updateTime.Software.LastUsedVersion
```

则不重复提醒。

伪代码：

```csharp
var comparison = latestVersion.CompareTo(currentVersion);
if (comparison > 0)
{
    if (Version.TryParse(state.LastUsedVersion, out var acknowledgedVersion)
        && latestVersion.CompareTo(acknowledgedVersion) <= 0)
    {
        return false;
    }

    result.UpdatedItems.Add("软件版本");
    result.DetectedSoftwareVersion = FormatVersion(latestVersion);
    return false;
}
```

### 5.4 MarkSoftwareCurrent 修改

当前：

```csharp
public SaveResult MarkSoftwareCurrent(UpdateCheckConfig config, string updateTimePath, Version currentVersion)
```

内部会判断：

```csharp
config.SoftwareVersionFilePath
```

v90 后建议改为：

```csharp
public SaveResult MarkSoftwareCurrent(string updateTimePath, Version currentVersion)
```

或者暂时保留旧签名但不再检查 `SoftwareVersionFilePath`。

推荐最小修改：

```csharp
public SaveResult MarkSoftwareCurrent(string updateTimePath, Version currentVersion)
```

如果没有调用点，也可以暂时保留旧方法但改为直接写当前版本。

## 6. MainViewModel 修改规格

### 6.1 CheckUpdatesOnceAsync

当前调用：

```csharp
return _updateCheckService.Check(updateCheckConfig, updateTimePath, currentVersion);
```

修改为：

```csharp
var manifestPath = _appSettings.SoftwareUpdateManifestPath?.Trim() ?? string.Empty;
return _updateCheckService.Check(updateCheckConfig, updateTimePath, currentVersion, manifestPath);
```

注意：

1. manifest 路径来自程序级 `AppSettings`。
2. 不再从 `_config.UpdateCheck.SoftwareVersionFilePath` 读取。
3. manifest 路径为空时不显示软件版本检测失败，避免未配置更新功能的用户被打扰。

## 7. 设置界面修改规格

### 7.1 删除“软件版本 txt”UI

从 `SettingsWindow.xaml` 删除或隐藏：

```text
软件版本 txt
对应 TextBox
对应 浏览按钮
```

只保留更新检测区域中的：

```text
rules 文件
map 文件
```

### 7.2 SettingsViewModel

移除或停用：

```csharp
BrowseSoftwareVersionFileCommand
```

`TrimUpdateCheckConfig()` 中不再需要处理：

```csharp
UpdateCheck.SoftwareVersionFilePath
```

但如果保留 trim 也没有问题。

### 7.3 文案调整

设置页中：

```text
manifest 路径
```

继续保留。

不需要在 UI 上额外解释“软件版本检测也使用 manifest”，避免增加说明文字。

## 8. UpdateCheckConfig 兼容策略

### 8.1 暂时保留字段

保留：

```csharp
public string SoftwareVersionFilePath { get; set; } = string.Empty;
```

保留 `Clone()` 中复制该字段也可以。

原因：

1. 兼容旧 config。
2. 兼容旧全局配置包。
3. 减少 JSON 反序列化变化。

### 8.2 逻辑不再使用

以下地方不再使用该字段：

```text
UpdateCheckService.Check
SettingsWindow UI
GlobalConfigPackageService 预检
导入结果 warning
```

## 9. v89 全局配置导入导出影响

### 9.1 导出

全局配置继续导出：

```text
ProgramSettings.SoftwareUpdateManifestPath
```

这是软件版本检测和软件自动更新的共同来源。

对于：

```text
WorkspaceConfig.UpdateCheck.SoftwareVersionFilePath
```

可选策略：

1. 保留原样导出，作为历史字段。
2. 导出前清空该字段。

推荐最小修改：

```text
保留原样导出，但不再使用。
```

这样不会意外改变用户已有配置文件结构。

### 9.2 导入

导入配置包时：

1. 读取旧包里的 `SoftwareVersionFilePath` 不报错。
2. 不校验该路径是否存在。
3. 不产生“UpdateCheck 软件版本文件不存在”警告。
4. 使用 `ProgramSettings.SoftwareUpdateManifestPath` 作为软件版本检测来源。

### 9.3 导入结果报告

如果 manifest 路径不存在，仍然提示：

```text
软件更新路径不存在：xxx
```

不再提示：

```text
UpdateCheck 软件版本文件不存在：xxx
```

## 10. 错误提示调整

旧提示：

```text
软件版本文件不存在
软件版本文件为空
软件版本号格式无效
软件版本文件读取失败：xxx
```

新提示：

```text
软件更新 manifest 文件不存在
manifest version 为空
manifest version 格式无效
manifest 读取失败：xxx
```

manifest 路径为空：

```text
不提示错误
```

原因：

```text
不是所有用户都一定配置自动更新功能。
路径为空时静默跳过软件版本检测更友好。
```

## 11. 测试要求

### 11.1 UpdateCheckServiceTests

新增/修改：

1. `Software_manifest_version_greater_than_current_returns_software_update`
   - manifest version 大于当前程序版本。
   - 期望提示“软件版本”。

2. `Software_manifest_version_equal_current_updates_software_baseline`
   - manifest version 等于当前程序版本。
   - 期望更新 `Software.LastUsedVersion`。

3. `Acknowledged_manifest_version_does_not_repeat_notice`
   - 当前版本 `2.1.0`
   - manifest version `2.2.0`
   - `LastUsedVersion = 2.2.0`
   - 期望不重复提示。

4. `Newer_manifest_version_than_acknowledged_returns_notice`
   - `LastUsedVersion = 2.2.0`
   - manifest version `2.3.0`
   - 期望重新提示。

5. `Missing_manifest_returns_failure`
   - manifest 路径非空但文件不存在。
   - 期望 failure 包含“软件更新 manifest 文件不存在”。

6. `Empty_manifest_path_skips_software_check_without_failure`
   - manifest 路径为空。
   - 期望没有软件版本相关 failure。

7. `Invalid_manifest_version_returns_failure`
   - manifest version 格式无效。
   - 期望 failure 包含“manifest version 格式无效”。

8. `Legacy_software_version_txt_is_ignored`
   - `UpdateCheck.SoftwareVersionFilePath` 指向不存在文件。
   - manifest 路径为空。
   - 期望不产生“软件版本文件不存在”。

### 11.2 SettingsViewModel / SettingsWindow 测试

如有相关测试，更新：

1. 不再期望 `BrowseSoftwareVersionFileCommand`。
2. 不再测试软件版本 txt 输入。

### 11.3 GlobalConfigPackageServiceTests

更新：

1. 导入旧配置包包含 `UpdateCheck.SoftwareVersionFilePath` 时不报错。
2. 不再因为该字段路径不存在产生 warning。
3. 继续校验 `ProgramSettings.SoftwareUpdateManifestPath`。

## 12. 手工验证

### 12.1 设置界面

1. 打开设置。
2. 确认仍有：

```text
manifest 路径
```

3. 确认更新检测区域不再显示：

```text
软件版本 txt
```

### 12.2 软件版本提醒

1. 配置 manifest 路径。
2. manifest 中写：

```json
{ "version": "9.9.9", "packageFile": "x.zip", "sha256": "abc" }
```

3. 等待自动检测。
4. 主界面应提示：

```text
检测到更新：软件版本
```

### 12.3 关闭提醒确认

1. 点击更新提醒 `×`。
2. 检查 `updateTime.json`：

```json
"Software": {
  "LastUsedVersion": "9.9.9"
}
```

3. 下一轮检测不再重复提示 `9.9.9`。

### 12.4 manifest 升级后重新提醒

1. 将 manifest version 改成 `10.0.0`。
2. 下一轮检测重新提示软件版本。

### 12.5 旧 txt 字段兼容

1. 在 config.json 中保留旧的 `SoftwareVersionFilePath`。
2. 路径可以不存在。
3. 程序启动和检测不能报“软件版本文件不存在”。

## 13. 风险与注意事项

### 13.1 manifest 路径是程序级

软件版本检测改用 manifest 后，软件版本提醒跟随程序级配置。

这意味着：

```text
同一个 VSLoader 程序下，所有工作区共用同一个软件版本提醒来源。
```

这是正确边界，因为软件更新是程序本体能力。

### 13.2 不要误删 manifest 路径 UI

要删除的是：

```text
软件版本 txt
```

不是：

```text
manifest 路径
```

manifest 路径必须保留。

### 13.3 不要破坏 v89 全局配置包

v89 的全局配置包仍需要导出：

```text
ProgramSettings.SoftwareUpdateManifestPath
```

导入时也必须继续处理该字段。

### 13.4 旧字段不要立刻删除

`SoftwareVersionFilePath` 暂时保留，避免旧配置和旧配置包带来的兼容问题。

后续如果要删除，应另起版本规格。

## 14. 验收标准

满足以下条件视为完成：

1. 设置窗口不再显示“软件版本 txt”。
2. manifest 路径 UI 仍然保留。
3. 自动检测软件版本更新从 manifest 的 `version` 字段读取。
4. `UpdateCheckConfig.SoftwareVersionFilePath` 不再参与检测。
5. manifest 路径为空时不报错。
6. manifest 路径不存在时显示更新检测失败。
7. manifest version 无效时显示更新检测失败。
8. v88 的关闭提醒确认逻辑继续有效。
9. v89 全局配置导入导出继续导出 `SoftwareUpdateManifestPath`。
10. v89 全局配置导入不再校验 `SoftwareVersionFilePath`。
11. 旧配置中残留 `SoftwareVersionFilePath` 不影响程序启动和检测。
12. `dotnet test .\VSLoader.sln` 通过。
13. `dotnet build .\VSLoader.sln -c Debug` 通过。
14. `.\build-release.ps1` 通过。

