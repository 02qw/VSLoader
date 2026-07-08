# VSLoader MVP 编码规格说明书

## 1. 文档目的

本文档用于指导编程 agent 或开发人员实现 VSLoader 的 MVP 版本。

VSLoader 是一个 Windows 桌面应用，用于集中管理常用 VSCode 打开入口，替代多个手写 `.bat` 文件。用户可以为每个目标文件夹或文件配置一个易理解的名称，并通过界面快速用 VSCode 打开。

本规格以可编码、可验收为目标，未写入本文档的功能不属于 MVP 范围。

## 2. MVP 范围

MVP 必须实现：

- WPF 桌面应用主界面。
- 配置 VSCode 可执行文件路径。
- 新增、编辑、删除快捷项。
- 搜索快捷项。
- 双击或点击按钮打开快捷项。
- 本地 JSON 持久化配置。
- 基础异常提示。

MVP 不实现：

- 用户登录。
- 云同步。
- 分组管理。
- 最近打开记录。
- 从 `.bat` 文件导入。
- 导出配置。
- 自动检测 VSCode 安装路径。
- 支持 VSCode 以外的编辑器。
- 托盘常驻。
- 自动更新。

## 3. 技术栈

| 项目 | 选择 |
| --- | --- |
| 桌面框架 | WPF |
| 开发语言 | C# |
| 运行时 | .NET 8 |
| 架构模式 | MVVM |
| MVVM 工具包 | `CommunityToolkit.Mvvm` |
| 配置格式 | JSON |
| JSON 序列化 | `System.Text.Json` |
| 启动外部程序 | `System.Diagnostics.Process` |
| VSCode 启动参数策略 | 不添加额外参数 |

必须创建的项目类型：

```powershell
dotnet new wpf -n VSLoader -f net8.0-windows
```

必须添加的 NuGet 包：

```powershell
dotnet add .\VSLoader\VSLoader.csproj package CommunityToolkit.Mvvm
```

## 4. 项目目录结构

推荐结构如下：

```text
VSLoader
├─ VSLoader.sln
├─ VSLoader
│  ├─ App.xaml
│  ├─ App.xaml.cs
│  ├─ MainWindow.xaml
│  ├─ MainWindow.xaml.cs
│  ├─ Models
│  │  ├─ AppConfig.cs
│  │  └─ ShortcutItem.cs
│  ├─ ViewModels
│  │  ├─ MainViewModel.cs
│  │  ├─ SettingsViewModel.cs
│  │  └─ ShortcutEditViewModel.cs
│  ├─ Views
│  │  ├─ SettingsWindow.xaml
│  │  └─ ShortcutEditWindow.xaml
│  ├─ Services
│  │  ├─ ConfigService.cs
│  │  ├─ VSCodeLauncherService.cs
│  │  └─ DialogService.cs
│  └─ Config
│     └─ config.example.json
├─ VSLoader_项目需求与技术方案.md
└─ VSLoader_MVP_编码规格说明书.md
```

## 5. 数据模型

### 5.1 AppConfig

```csharp
public sealed class AppConfig
{
    public string VSCodePath { get; set; } = string.Empty;

    public List<ShortcutItem> Shortcuts { get; set; } = new();
}
```

### 5.2 ShortcutItem

```csharp
public sealed class ShortcutItem
{
    public string Name { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
```

MVP 不增加 `LastOpenedAt` 字段。

## 6. 配置文件规格

### 6.1 配置文件位置

配置文件固定保存到：

```text
%AppData%\VSLoader\config.json
```

C# 获取方式：

```csharp
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var configDir = Path.Combine(appData, "VSLoader");
var configPath = Path.Combine(configDir, "config.json");
```

### 6.2 配置文件示例

```json
{
  "VSCodePath": "C:\\Users\\shee_\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe",
  "Shortcuts": [
    {
      "Name": "3365_TRSM005",
      "TargetPath": "\\\\192.168.15.69\\instances\\3365_TRSM005",
      "Description": "网络实例目录",
      "CreatedAt": "2026-05-26T10:00:00",
      "UpdatedAt": "2026-05-26T10:00:00"
    }
  ]
}
```

### 6.3 配置读取规则

- 应用启动时读取 `config.json`。
- 如果 `%AppData%\VSLoader` 目录不存在，自动创建目录。
- 如果 `config.json` 不存在，自动创建默认空配置。
- 如果 `config.json` 存在但 JSON 损坏或无法反序列化，只弹窗提示用户配置文件损坏，不自动覆盖、不自动备份、不自动重建。
- 配置损坏时，主界面仍应尽量打开，但快捷项列表为空，且顶部显示配置异常提示。

### 6.4 配置保存规则

- 用户保存设置、添加快捷项、编辑快捷项、删除快捷项后，应立即写入 JSON 文件。
- JSON 输出必须使用缩进格式。
- 保存失败时弹窗提示错误原因。

## 7. UI 规格

### 7.1 主窗口

主窗口标题：

```text
VSLoader
```

主窗口至少包含：

- 顶部状态提示区域。
- 搜索输入框。
- 快捷项列表。
- 新增按钮。
- 编辑按钮。
- 删除按钮。
- 打开按钮。
- 设置按钮。

### 7.2 顶部状态提示区域

首次启动或 VSCode 路径无效时，不自动弹出设置窗口。

主界面顶部显示醒目提示：

```text
尚未配置有效的 VSCode 路径，请进入设置。
```

当配置文件损坏时显示：

```text
配置文件读取失败，请检查 %AppData%\VSLoader\config.json。
```

### 7.3 快捷项列表

列表至少显示以下列：

- 名称。
- 目标路径。
- 备注。
- 更新时间。

列表交互：

- 单击选中快捷项。
- 双击快捷项执行打开。
- 选中快捷项后，编辑、删除、打开按钮可用。
- 未选中快捷项时，编辑、删除、打开按钮不可用。

### 7.4 搜索

搜索框实时过滤快捷项。

搜索范围：

- `Name`
- `TargetPath`
- `Description`

搜索规则：

- 忽略大小写。
- 输入为空时显示全部快捷项。
- 不修改原始数据，只影响列表显示。

### 7.5 设置窗口

设置窗口标题：

```text
设置
```

字段和按钮：

- VSCode 路径输入框。
- 浏览按钮。
- 保存按钮。
- 取消按钮。

规则：

- 用户可以手动输入 VSCode 路径。
- 浏览按钮只选择 `.exe` 文件。
- 保存时必须校验路径存在。
- 保存时必须校验文件扩展名为 `.exe`。
- 保存成功后关闭设置窗口。
- 取消时不保存任何修改。

### 7.6 快捷项编辑窗口

新增和编辑共用同一个窗口。

新增窗口标题：

```text
新增快捷项
```

编辑窗口标题：

```text
编辑快捷项
```

字段和按钮：

- 名称输入框。
- 目标路径输入框。
- 浏览文件夹按钮。
- 浏览文件按钮。
- 备注输入框。
- 保存按钮。
- 取消按钮。

规则：

- 名称必填。
- 目标路径必填。
- 保存时自动去除名称、目标路径、备注前后的空格。
- 快捷项名称不允许重复。
- 编辑当前快捷项时，原名称不计入重复判断。
- 保存成功后关闭窗口。
- 取消时不保存任何修改。

## 8. 路径校验规则

### 8.1 VSCode 路径

VSCode 路径保存时必须满足：

- 非空。
- 文件存在。
- 扩展名为 `.exe`。

不要求文件名必须是 `Code.exe`，但界面提示用户选择 VSCode 的可执行文件。

### 8.2 目标路径

快捷项目标路径支持：

- 本地文件夹。
- 本地文件。
- 网络共享路径。

保存快捷项时：

- 如果目标路径是本地路径，必须存在。
- 如果目标路径是网络路径，允许保存，即使当前不可访问。

网络路径判断：

- 以 `\\` 开头的路径视为网络路径。

打开快捷项时：

- 无论本地路径还是网络路径，都必须再次校验目标路径是否存在。
- 如果不存在或不可访问，弹窗提示：

```text
目标路径不存在或当前不可访问。
```

## 9. 快捷项重复规则

快捷项名称不允许重复。

比较规则：

- 去除前后空格后比较。
- 忽略大小写。

示例：

- `3365_TRSM005`
- ` 3365_TRSM005 `
- `3365_trsm005`

以上三个名称视为重复。

目标路径允许重复。

## 10. 打开 VSCode 规则

打开快捷项时，使用：

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = config.VSCodePath,
    UseShellExecute = false
};

startInfo.ArgumentList.Add(shortcut.TargetPath);
Process.Start(startInfo);
```

MVP 不添加 `-r`、`-n` 或其他 VSCode 参数。

打开前校验：

- VSCode 路径必须有效。
- 目标路径必须存在或可访问。

打开失败时弹窗提示异常信息。

打开成功后：

- 不记录最近打开时间。
- 不修改 `UpdatedAt`。
- 不关闭主窗口。

## 11. 时间字段规则

新增快捷项时：

- `CreatedAt` 设置为当前本地时间。
- `UpdatedAt` 设置为当前本地时间。

编辑快捷项时：

- 保持原 `CreatedAt` 不变。
- `UpdatedAt` 设置为当前本地时间。

打开快捷项时：

- 不修改任何时间字段。

## 12. 删除规则

删除快捷项前必须弹出确认框。

确认文案：

```text
确定要删除该快捷项吗？
```

用户确认后删除并立即保存配置。

用户取消时不做任何修改。

## 13. 服务职责

### 13.1 ConfigService

职责：

- 获取配置文件路径。
- 创建配置目录。
- 读取配置。
- 写入配置。
- 在配置文件不存在时创建默认配置。
- 在配置文件损坏时返回失败结果，不自动修改损坏文件。

### 13.2 VSCodeLauncherService

职责：

- 校验 VSCode 路径。
- 校验目标路径。
- 调用 `Process.Start`。
- 返回成功或失败结果。

### 13.3 DialogService

职责：

- 弹出普通提示。
- 弹出错误提示。
- 弹出确认框。
- 选择 `.exe` 文件。
- 选择目标文件。
- 选择目标文件夹。

## 14. ViewModel 职责

### 14.1 MainViewModel

职责：

- 持有当前配置。
- 持有快捷项集合。
- 持有搜索文本。
- 维护过滤后的快捷项列表。
- 维护当前选中的快捷项。
- 提供新增、编辑、删除、打开、设置命令。
- 根据配置状态生成顶部提示文本。

### 14.2 SettingsViewModel

职责：

- 编辑 VSCode 路径。
- 调用文件选择器选择 `.exe`。
- 保存设置。
- 取消设置。

### 14.3 ShortcutEditViewModel

职责：

- 新增或编辑快捷项。
- 校验名称和目标路径。
- 调用文件夹选择器。
- 调用文件选择器。
- 保存或取消编辑。

## 15. 验收标准

完成后必须满足：

- 可以直接运行 WPF 应用。
- 首次启动时没有 VSCode 路径，主界面显示未配置提示。
- 可以进入设置窗口并保存有效 `.exe` 路径。
- 保存 VSCode 路径后，重启应用仍能读取配置。
- 可以新增本地文件夹快捷项。
- 可以新增网络路径快捷项，例如：

```text
\\192.168.15.69\instances\3365_TRSM005
```

- 本地路径不存在时，保存快捷项失败并提示。
- 网络路径即使当前不可访问，也允许保存。
- 快捷项名称重复时，保存失败并提示。
- 双击快捷项或点击打开按钮可以启动 VSCode。
- 删除快捷项前必须弹出确认框。
- 删除确认后，快捷项从列表和 JSON 配置中移除。
- 搜索框可以按名称、路径、备注过滤。
- 配置文件损坏时，应用提示配置读取失败，不覆盖原文件。

## 16. 推荐开发步骤

1. 创建解决方案和 WPF 项目。
2. 添加 `CommunityToolkit.Mvvm`。
3. 创建目录结构。
4. 实现 `AppConfig` 和 `ShortcutItem`。
5. 实现 `ConfigService`。
6. 实现 `DialogService`。
7. 实现 `VSCodeLauncherService`。
8. 实现 `MainViewModel`。
9. 实现主窗口 UI。
10. 实现设置窗口和 `SettingsViewModel`。
11. 实现快捷项编辑窗口和 `ShortcutEditViewModel`。
12. 联调配置读写。
13. 联调快捷项增删改查。
14. 联调 VSCode 启动。
15. 按验收标准逐项测试。

