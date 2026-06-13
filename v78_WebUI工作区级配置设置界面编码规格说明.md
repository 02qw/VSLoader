# v78 WebUI工作区级配置设置界面编码规格说明

## 1. 文档目的

本文件用于指导编程 Agent 将 WebUI 链接拼接参数暴露到“设置”窗口中，并明确这些参数属于：

```text
工作区级配置
```

也就是说，WebUI 配置必须保存到当前工作区的：

```text
%AppData%\VSLoader\Workspaces\<当前工作区>\config.json
```

不能保存到程序区：

```text
%AppData%\VSLoader\app-settings.json
```

## 2. 背景说明

当前 VSLoader 已经支持多工作区。

程序区配置：

```text
%AppData%\VSLoader\app-settings.json
```

用于保存：

```text
1. 工作区列表
2. LastWorkspaceId
3. 每个工作区的 Id、Name、Path
4. 是否打开上次工作区等程序级状态
```

工作区配置：

```text
%AppData%\VSLoader\Workspaces\<WorkspaceFolder>\config.json
```

用于保存：

```text
1. 当前工作区快捷项
2. 当前工作区 VSCode 路径
3. 当前工作区 AdminUI 配置
4. 当前工作区 WebUI 配置
5. 当前工作区批量新增配置
6. 当前工作区快捷键配置
```

WebUI 的服务器地址、properties 文件名、实例名 key、端口 key 都与产线/工作区有关，因此必须属于工作区配置。

## 3. 当前已有代码基础

当前已经存在 WebUI 配置模型：

```text
VSLoader\Models\WebUiConfig.cs
```

当前默认值：

```csharp
public string BaseUrl { get; set; } = "https://192.168.15.69";

public string InstancePropertiesName { get; set; } = "INSTANCE.properties";

public string InstanceNameKey { get; set; } = "zam.instance.name";

public string SslPortKey { get; set; } = "GUI.WebServer.SSLPort";
```

当前工作区配置模型：

```text
VSLoader\Models\AppConfig.cs
```

已经包含：

```csharp
public WebUiConfig WebUi { get; set; } = new();
```

当前 WebUI 打开逻辑：

```text
VSLoader\Models\Services\WebUiService.cs
```

当前打开时已经使用：

```csharp
_webUiService.OpenWebUi(SelectedShortcut, _config.WebUi);
```

也就是说，底层数据结构和打开逻辑已经基本具备工作区级 WebUI 配置能力。  
本次重点是：

```text
把 WebUiConfig 暴露到设置窗口中，让用户可以编辑并保存。
```

## 4. 需求目标

完成后用户应该可以：

```text
1. 打开某个工作区。
2. 点击主界面“设置”。
3. 在设置窗口中看到 WebUI 配置区域。
4. 修改 WebUI BaseUrl。
5. 修改 INSTANCE.properties 文件名。
6. 修改实例名 Key。
7. 修改 SSL 端口 Key。
8. 点击保存后，配置写入当前工作区的 config.json。
9. 切换到其他工作区后，读取另一个工作区自己的 WebUI 配置。
10. 点击快捷项 WebUI 时，使用当前工作区的 WebUI 配置拼接链接。
```

## 5. 非目标范围

本阶段不实现：

```text
1. 不修改 WebUI URL 拼接算法。
2. 不修改 WebUiService.OpenWebUi 的核心流程。
3. 不把 WebUI 配置保存到 app-settings.json。
4. 不新增程序级 WebUI 默认配置中心。
5. 不新增 WebUI 网络连通性测试按钮。
6. 不新增 WebUI URL 预览按钮。
7. 不新增导入/导出 WebUI 配置。
8. 不修改 AdminUI 逻辑。
9. 不修改工作区切换逻辑。
10. 不修改快捷项数据结构。
```

## 6. 配置归属规则

### 6.1 程序区 app-settings.json

不能新增 WebUI 字段。

`app-settings.json` 只继续负责：

```text
1. Workspaces
2. LastWorkspaceId
3. OpenLastWorkspaceOnStartup
```

禁止把以下字段保存到 `app-settings.json`：

```text
WebUi.BaseUrl
WebUi.InstancePropertiesName
WebUi.InstanceNameKey
WebUi.SslPortKey
```

### 6.2 工作区 config.json

WebUI 配置必须保存到：

```text
%AppData%\VSLoader\Workspaces\<WorkspaceFolder>\config.json
```

推荐结构：

```json
{
  "WebUi": {
    "BaseUrl": "https://192.168.15.69",
    "InstancePropertiesName": "INSTANCE.properties",
    "InstanceNameKey": "zam.instance.name",
    "SslPortKey": "GUI.WebServer.SSLPort"
  }
}
```

注意：

```text
config.json 中还会有 VSCodePath、Shortcuts、AdminUi、Hotkey、BatchImport 等字段。
不要覆盖或丢失这些已有字段。
```

## 7. 设置窗口 UI 改造

### 7.1 目标文件

主要修改：

```text
VSLoader\Views\SettingsWindow.xaml
```

可能修改：

```text
VSLoader\ViewModels\SettingsViewModel.cs
```

### 7.2 新增 WebUI 配置区域

设置窗口中应新增一个分组，标题建议：

```text
WebUI 配置
```

字段：

```text
BaseUrl
INSTANCE.properties 文件名
实例名 Key
SSL 端口 Key
```

建议 UI 排列参考现有 AdminUI 区域，保持同样的表单风格。

示例：

```xml
<GroupBox Header="WebUI 配置">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="150" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>

        <TextBlock Grid.Row="0" Grid.Column="0" VerticalAlignment="Center" Text="BaseUrl" />
        <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding WebUi.BaseUrl, UpdateSourceTrigger=PropertyChanged}" />

        <TextBlock Grid.Row="1" Grid.Column="0" VerticalAlignment="Center" Text="properties 文件名" />
        <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding WebUi.InstancePropertiesName, UpdateSourceTrigger=PropertyChanged}" />

        <TextBlock Grid.Row="2" Grid.Column="0" VerticalAlignment="Center" Text="实例名 Key" />
        <TextBox Grid.Row="2" Grid.Column="1" Text="{Binding WebUi.InstanceNameKey, UpdateSourceTrigger=PropertyChanged}" />

        <TextBlock Grid.Row="3" Grid.Column="0" VerticalAlignment="Center" Text="SSL 端口 Key" />
        <TextBox Grid.Row="3" Grid.Column="1" Text="{Binding WebUi.SslPortKey, UpdateSourceTrigger=PropertyChanged}" />
    </Grid>
</GroupBox>
```

实际 XAML 应根据现有 `SettingsWindow.xaml` 的布局微调，不要引入完全不同的视觉风格。

## 8. SettingsViewModel 改造

### 8.1 暴露 WebUi 属性

修改文件：

```text
VSLoader\ViewModels\SettingsViewModel.cs
```

如果当前只暴露了 `AdminUi`，需要新增：

```csharp
public WebUiConfig WebUi { get; }
```

初始化时从当前工作区 `AppConfig` 克隆：

```csharp
WebUi = config.WebUi.Clone();
```

保存时写回：

```csharp
config.WebUi = WebUi.Clone();
```

注意：

```text
不能直接让设置窗口编辑全局 appSettings。
WebUi 必须来自当前工作区 ConfigService 加载的 AppConfig。
```

### 8.2 保存前 Trim

保存前应对 WebUI 字段做 Trim：

```csharp
WebUi.BaseUrl = WebUi.BaseUrl.Trim();
WebUi.InstancePropertiesName = WebUi.InstancePropertiesName.Trim();
WebUi.InstanceNameKey = WebUi.InstanceNameKey.Trim();
WebUi.SslPortKey = WebUi.SslPortKey.Trim();
```

### 8.3 校验规则

保存时建议校验：

```text
1. BaseUrl 不能为空。
2. BaseUrl 必须以 http:// 或 https:// 开头。
3. InstancePropertiesName 不能为空。
4. InstanceNameKey 不能为空。
5. SslPortKey 不能为空。
```

错误提示建议：

```text
请输入有效的 WebUI BaseUrl。
WebUI BaseUrl 必须以 http:// 或 https:// 开头。
请输入 WebUI properties 文件名。
请输入 WebUI 实例名 Key。
请输入 WebUI SSL 端口 Key。
```

说明：

```text
不在设置保存阶段校验 INSTANCE.properties 是否真实存在，因为该文件属于具体快捷项目标路径。
```

## 9. ConfigService 兼容性

检查文件：

```text
VSLoader\Models\Services\ConfigService.cs
```

必须确保旧 config.json 没有 WebUi 字段时：

```csharp
config.WebUi ??= new WebUiConfig();
```

当前项目中大概率已经存在该兼容逻辑。  
如果已经存在，不需要重复修改。

要求：

```text
旧工作区配置加载后，WebUi 不为 null，并带默认值。
```

## 10. WebUiService 逻辑保持不变

文件：

```text
VSLoader\Models\Services\WebUiService.cs
```

本需求不要求修改。

当前策略继续保持：

```text
WebUI URL =
WebUi.BaseUrl.TrimEnd('/')
+ ":"
+ INSTANCE.properties 中的 WebUi.SslPortKey 值
+ "/"
+ INSTANCE.properties 中的 WebUi.InstanceNameKey 值
   或目标文件夹最后一个 "_" 后面的内容
+ "/ui"
```

示例：

```text
BaseUrl = https://192.168.15.67
GUI.WebServer.SSLPort = 10024
zam.instance.name = TATP012

最终 URL：
https://192.168.15.67:10024/TATP012/ui
```

## 11. 测试要求

### 11.1 SettingsViewModel 测试

如果当前已有 SettingsViewModel 测试，则新增：

```text
1. SaveSettings_writes_webui_config_to_workspace_config
2. SaveSettings_rejects_empty_webui_base_url
3. SaveSettings_rejects_webui_base_url_without_http_scheme
4. SaveSettings_rejects_empty_webui_properties_name
5. SaveSettings_rejects_empty_webui_instance_name_key
6. SaveSettings_rejects_empty_webui_ssl_port_key
```

如果当前没有 SettingsViewModel 测试，可以新增测试文件：

```text
VSLoader.Tests\SettingsViewModelWebUiTests.cs
```

核心断言：

```text
保存后当前工作区 config.json 中 WebUi 字段更新。
app-settings.json 不出现 WebUi 字段。
```

### 11.2 ConfigService 兼容测试

如果已有 ConfigService 测试，确认或新增：

```text
Load_returns_default_webui_config_when_missing_from_json
```

### 11.3 WebUiService 回归测试

如果已有 WebUiService 测试，确认：

```text
OpenWebUi / Build URL 使用传入的 config.BaseUrl，而不是硬编码默认地址。
```

如果没有，不强制本次新增，因为本需求重点是设置界面与保存。

## 12. 手工验收

### 场景一：当前工作区修改 WebUI 配置

操作：

```text
1. 打开工作区 A。
2. 点击设置。
3. 修改 WebUI BaseUrl 为 https://192.168.15.67。
4. 保存。
5. 查看工作区 A 的 config.json。
```

预期：

```text
config.json 中 WebUi.BaseUrl 为 https://192.168.15.67。
```

### 场景二：切换工作区配置互不影响

操作：

```text
1. 工作区 A 设置 WebUI BaseUrl = https://192.168.15.67。
2. 工作区 B 设置 WebUI BaseUrl = https://192.168.15.69。
3. 分别切换打开两个工作区并进入设置。
```

预期：

```text
工作区 A 显示自己的 BaseUrl。
工作区 B 显示自己的 BaseUrl。
两者互不覆盖。
```

### 场景三：程序区不保存 WebUI

操作：

```text
1. 修改 WebUI 配置并保存。
2. 打开 %AppData%\VSLoader\app-settings.json。
```

预期：

```text
app-settings.json 中没有 WebUi 字段。
```

### 场景四：点击 WebUI 使用新配置

操作：

```text
1. 当前工作区设置 WebUI BaseUrl。
2. 选择一个快捷项。
3. 点击 WebUI。
```

预期：

```text
打开的浏览器 URL 使用当前工作区配置的 BaseUrl。
```

## 13. 风险点

### 13.1 错把 WebUI 保存到 app-settings.json

风险：

```text
WebUI 地址变成程序全局配置，切换工作区后会串线。
```

规避：

```text
只修改当前工作区 config.json。
不要修改 AppSettings 模型。
不要在 AppSettingsService 中加入 WebUi。
```

### 13.2 保存设置时覆盖快捷项

风险：

```text
保存 WebUI 配置时，如果重新创建 AppConfig 可能丢失 Shortcuts。
```

规避：

```text
必须基于当前加载的 AppConfig 修改 config.WebUi 后整体保存。
不要 new AppConfig 后只写 WebUi。
```

### 13.3 旧工作区没有 WebUi 字段

风险：

```text
旧 config.json 加载后 WebUi 为 null，设置窗口绑定崩溃。
```

规避：

```text
ConfigService.Normalize 或等价逻辑必须 config.WebUi ??= new WebUiConfig();
```

## 14. 验收标准

本需求完成必须满足：

```text
1. 设置窗口出现 WebUI 配置区域。
2. 可以编辑 BaseUrl。
3. 可以编辑 properties 文件名。
4. 可以编辑实例名 Key。
5. 可以编辑 SSL 端口 Key。
6. 保存后写入当前工作区 config.json。
7. 不写入 app-settings.json。
8. 切换工作区后 WebUI 配置互不影响。
9. WebUI 打开时使用当前工作区配置。
10. dotnet build 通过。
11. dotnet test 通过。
```

## 15. 推荐执行命令

实现前停止运行中的程序：

```powershell
Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force
```

构建：

```powershell
dotnet build C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader.sln -p:UseSharedCompilation=false
```

测试：

```powershell
dotnet test C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader.sln -p:UseSharedCompilation=false
```

## 16. 推荐提交信息

```text
feat: expose workspace webui settings
```
