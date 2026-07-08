# VSLoader 项目需求与技术方案

## 1. 项目背景

在 Windows 系统中，可以通过命令行直接使用 VSCode 打开指定文件夹或文件：

```bat
"C:\Users\用户名\AppData\Local\Programs\Microsoft VS Code\Code.exe" "C:\目标文件夹或文件路径"
```

目前可以把这类命令写入 `.bat` 文件，并通过给 `.bat` 文件命名来区分不同项目或目录。例如，一个名为 `3365_TRSM005.bat` 的文件可以用于打开某个固定项目目录。

这种方式简单有效，但存在以下问题：

- 每个快捷入口都需要单独维护一个 `.bat` 文件。
- VSCode 的安装路径在不同电脑上可能不同，命令前半部分不能固定。
- 快捷入口数量变多后，不方便搜索、修改、删除和分类。
- 网络路径较长时，用户很难直接从路径判断要打开的内容。

VSLoader 的目标是把这些分散的 `.bat` 文件能力集中到一个 Windows 桌面应用中，通过可视化界面统一管理 VSCode 路径和常用打开项。

## 2. 项目目标

VSLoader 是一个 Windows 桌面应用，用于管理并快速打开 VSCode 项目路径。

核心目标：

- 用户可以配置本机 VSCode 可执行文件路径。
- 用户可以创建多个快捷打开项，并为每个路径设置易理解的名称。
- 用户可以通过双击或点击按钮，用 VSCode 打开指定文件夹或文件。
- 应用可以保存配置和快捷项，关闭后再次打开仍然保留数据。
- 应用应兼容本地路径和网络共享路径。

## 3. 功能需求分析

### 3.1 配置中心

配置中心用于维护全局配置。

必须支持：

- 设置 VSCode 程序路径，即 `Code.exe` 的完整路径。
- 通过文件选择器选择 `Code.exe`。
- 保存配置到本地 JSON 文件。
- 应用启动时自动读取配置。
- 当 VSCode 路径为空或无效时，提示用户重新配置。

示例：

```text
C:\Users\shee_\AppData\Local\Programs\Microsoft VS Code\Code.exe
```

### 3.2 快捷项管理

快捷项代表一个可被 VSCode 打开的文件夹或文件。

必须支持：

- 新增快捷项。
- 编辑快捷项。
- 删除快捷项。
- 列表展示快捷项。
- 双击快捷项打开目标路径。
- 支持本地路径和网络路径。

快捷项字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `Name` | `string` | 是 | 快捷项显示名称，用于替代 `.bat` 文件名 |
| `TargetPath` | `string` | 是 | 要用 VSCode 打开的文件夹或文件路径 |
| `Description` | `string` | 否 | 备注说明 |
| `CreatedAt` | `DateTime` | 是 | 创建时间 |
| `UpdatedAt` | `DateTime` | 是 | 最后修改时间 |

网络路径示例：

```text
\\192.168.15.69\instances\3365_TRSM005
```

### 3.3 打开路径

用户触发快捷项后，应用执行以下逻辑：

1. 读取配置中的 VSCode 路径。
2. 校验 `Code.exe` 是否存在。
3. 读取快捷项中的目标路径。
4. 校验目标路径是否存在。
5. 使用 `System.Diagnostics.Process` 启动 VSCode。
6. 将目标路径作为参数传给 VSCode。

等价命令：

```bat
"配置的 Code.exe 路径" "快捷项目标路径"
```

### 3.4 基础搜索

最小可用版本可以先不做复杂分类，但建议支持名称搜索。

搜索范围：

- 快捷项名称。
- 目标路径。
- 备注说明。

### 3.5 数据持久化

应用数据保存到本地 JSON 文件。

建议保存位置：

```text
%AppData%\VSLoader\config.json
```

保存内容：

- 全局配置。
- 快捷项列表。

## 4. 非功能需求

- 应用应只面向 Windows 系统。
- 应用界面应简单直接，重点是快速找到并打开目标路径。
- 路径中包含空格、中文或网络共享地址时，应正常工作。
- 配置文件损坏时，应给出友好提示，而不是直接崩溃。
- 打开失败时，应提示具体原因，例如 VSCode 路径不存在、目标路径不存在、网络路径不可访问。

## 5. 项目技术栈明细

| 模块 | 技术选择 | 说明 |
| --- | --- | --- |
| 桌面框架 | WPF | 成熟稳定，适合 Windows 本地桌面工具 |
| 开发语言 | C# | 与 WPF 和 .NET 生态天然匹配 |
| 运行时 | .NET 8 | 当前推荐的长期可用 .NET 版本之一 |
| 架构模式 | MVVM | 分离界面、状态和业务逻辑 |
| 配置格式 | JSON | 简单可读，方便后续导入导出 |
| JSON 序列化 | `System.Text.Json` | .NET 内置，无需额外依赖 |
| 启动外部程序 | `System.Diagnostics.Process` | 用于调用 VSCode |
| 文件选择 | WPF `OpenFileDialog` | 用于选择 `Code.exe` |
| 文件夹选择 | `FolderBrowserDialog` 或自定义选择逻辑 | 用于选择目标文件夹 |

## 6. 推荐项目结构

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
│  │  └─ SettingsViewModel.cs
│  ├─ Views
│  │  ├─ SettingsWindow.xaml
│  │  └─ ShortcutEditWindow.xaml
│  ├─ Services
│  │  ├─ ConfigService.cs
│  │  ├─ VSCodeLauncherService.cs
│  │  └─ DialogService.cs
│  └─ Config
│     └─ config.example.json
└─ VSLoader_项目需求与技术方案.md
```

## 7. 核心数据模型

### 7.1 AppConfig

```csharp
public sealed class AppConfig
{
    public string VSCodePath { get; set; } = string.Empty;

    public List<ShortcutItem> Shortcuts { get; set; } = new();
}
```

### 7.2 ShortcutItem

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

### 7.3 配置文件示例

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

## 8. 关键技术细节

### 8.1 配置文件路径

配置文件建议放在当前用户的 AppData 目录：

```csharp
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var configDir = Path.Combine(appData, "VSLoader");
var configPath = Path.Combine(configDir, "config.json");
```

如果目录不存在，应用启动时自动创建。

### 8.2 JSON 读写

使用 `System.Text.Json` 进行序列化和反序列化。

建议开启缩进，方便用户排查配置：

```csharp
var options = new JsonSerializerOptions
{
    WriteIndented = true
};
```

### 8.3 启动 VSCode

推荐使用 `ProcessStartInfo`。

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = config.VSCodePath,
    UseShellExecute = false
};

startInfo.ArgumentList.Add(shortcut.TargetPath);

Process.Start(startInfo);
```

使用 `ArgumentList` 可以避免手动拼接引号，能够更好地兼容路径中的空格。

### 8.4 路径校验

启动前需要校验：

```csharp
if (!File.Exists(config.VSCodePath))
{
    // 提示 VSCode 路径无效
}

if (!Directory.Exists(shortcut.TargetPath) && !File.Exists(shortcut.TargetPath))
{
    // 提示目标路径不存在或不可访问
}
```

网络路径可能因为网络不可达、权限不足、共享目录关闭等原因暂时不可访问，需要给出明确错误提示。

### 8.5 UI 页面规划

主窗口建议包含：

- 顶部工具栏：新增、编辑、删除、设置。
- 搜索框：按名称、路径、备注过滤。
- 快捷项列表：显示名称、目标路径、备注、更新时间。
- 操作按钮：打开、编辑、删除。

设置窗口建议包含：

- VSCode 路径输入框。
- 浏览按钮。
- 保存按钮。
- 路径有效性提示。

快捷项编辑窗口建议包含：

- 名称输入框。
- 目标路径输入框。
- 浏览文件夹按钮。
- 浏览文件按钮。
- 备注输入框。
- 保存和取消按钮。

## 9. 异常处理策略

| 场景 | 处理方式 |
| --- | --- |
| 未配置 VSCode 路径 | 弹出设置窗口或提示用户进入配置中心 |
| VSCode 路径不存在 | 提示用户重新选择 `Code.exe` |
| 目标路径不存在 | 提示目标路径不存在或网络不可达 |
| JSON 配置文件不存在 | 自动创建默认配置 |
| JSON 配置文件损坏 | 提示配置损坏，并允许重新初始化 |
| 启动 VSCode 失败 | 捕获异常并显示失败原因 |

## 10. 已实现步骤

当前已完成：

- 明确项目路径：`C:\Users\shee_\OneDrive\Desktop\VSLoader`
- 明确项目定位：VSCode 快捷启动器。
- 明确桌面框架：WPF。
- 明确运行时：.NET 8。
- 明确配置存储方式：本地 JSON 文件。
- 明确 VSCode 启动方式：`System.Diagnostics.Process`。
- 完成项目需求与技术方案文档。

## 11. 后续开发实现步骤

### 第一步：创建 WPF 项目

在项目目录下创建解决方案和 WPF 项目：

```powershell
dotnet new sln -n VSLoader
dotnet new wpf -n VSLoader -f net8.0-windows
dotnet sln VSLoader.sln add .\VSLoader\VSLoader.csproj
```

### 第二步：创建基础目录

在 WPF 项目中创建：

- `Models`
- `ViewModels`
- `Views`
- `Services`
- `Config`

### 第三步：实现数据模型

创建：

- `AppConfig`
- `ShortcutItem`

### 第四步：实现配置服务

创建 `ConfigService`，负责：

- 获取配置文件路径。
- 创建默认配置。
- 读取 JSON 配置。
- 保存 JSON 配置。
- 处理配置文件不存在或损坏的情况。

### 第五步：实现 VSCode 启动服务

创建 `VSCodeLauncherService`，负责：

- 校验 VSCode 路径。
- 校验目标路径。
- 调用 `Process.Start` 启动 VSCode。
- 返回启动成功或失败信息。

### 第六步：实现主界面

主界面需要支持：

- 展示快捷项列表。
- 搜索快捷项。
- 打开快捷项。
- 新增快捷项。
- 编辑快捷项。
- 删除快捷项。
- 进入配置中心。

### 第七步：实现设置窗口

设置窗口需要支持：

- 输入 VSCode 路径。
- 浏览选择 `Code.exe`。
- 保存配置。
- 校验路径是否有效。

### 第八步：实现快捷项编辑窗口

快捷项编辑窗口需要支持：

- 输入快捷项名称。
- 输入目标路径。
- 选择文件夹。
- 选择文件。
- 输入备注。
- 保存快捷项。

### 第九步：联调与异常处理

需要验证：

- 首次启动时配置为空的处理。
- 配置保存后能再次读取。
- 快捷项增删改查正常。
- 打开本地文件夹正常。
- 打开网络共享路径正常。
- VSCode 路径错误时提示正确。
- 目标路径不存在时提示正确。

### 第十步：打包发布

可以先使用 `dotnet publish` 发布为本地可运行程序：

```powershell
dotnet publish .\VSLoader\VSLoader.csproj -c Release -r win-x64 --self-contained false
```

后续如果需要给非开发人员使用，可以考虑：

- 生成自包含版本。
- 使用安装包工具打包。
- 添加桌面快捷方式。

## 12. 验收测试场景

| 编号 | 测试场景 | 预期结果 |
| --- | --- | --- |
| 1 | 首次启动且没有配置 VSCode 路径 | 应提示用户配置 VSCode 路径 |
| 2 | 配置正确的 `Code.exe` 路径 | 配置可以保存并再次读取 |
| 3 | 新增本地文件夹快捷项 | 列表中显示该快捷项 |
| 4 | 新增网络路径快捷项 | 列表中显示该快捷项 |
| 5 | 双击快捷项 | VSCode 打开对应路径 |
| 6 | VSCode 路径不存在 | 应提示 VSCode 路径无效 |
| 7 | 目标路径不存在 | 应提示目标路径不存在或不可访问 |
| 8 | 删除快捷项 | 快捷项从列表和配置文件中移除 |
| 9 | 修改快捷项名称 | 列表显示修改后的名称 |
| 10 | 重启应用 | 配置和快捷项仍然存在 |

## 13. 后续扩展方向

后续可以根据使用情况增加：

- 快捷项分组。
- 置顶常用项。
- 最近打开记录。
- 从现有 `.bat` 文件导入快捷项。
- 导出快捷项配置。
- 自动检测 VSCode 安装路径。
- 支持 Cursor、Visual Studio、Windows Terminal 等其他应用启动器。
- 支持为快捷项生成桌面快捷方式。

