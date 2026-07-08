# v8 AdminUI 密码剪贴板编码规格说明

## 1. 文档目的

本文档用于指导编程 agent 或开发人员为 VSLoader 增加 AdminUI 密码剪贴板功能。

目标是在用户点击 `AdminUI` 打开对应 `.jnlp` 文件后，自动将用户配置的 AdminUI 密码写入系统剪贴板，方便用户在 SwingUI 登录窗口中直接粘贴密码。

## 2. 需求背景

当前用户点击 `AdminUI` 后，可以打开对应的 `.jnlp` 文件，并弹出 Java Swing 登录窗口。

登录窗口中用户名已显示，但密码需要用户手动输入。该流程较繁琐。

本次需求采用简化方案：

```text
点击 AdminUI -> 打开 .jnlp -> 立即将配置好的密码写入系统剪贴板
```

不检测 SwingUI 是否已经加载完成。

## 3. 用户确认的实现选择

| 项目 | 选择 |
| --- | --- |
| 自动填充方式 | 不直接控制 SwingUI 登录框 |
| 密码写入时机 | 点击 `AdminUI` 并成功打开 `.jnlp` 后立即写入剪贴板 |
| 是否检测 SwingUI 窗口 | 不检测 |
| 密码配置入口 | 主界面“设置”窗口 |
| 设置分类名称 | `AdminUI 密码` |
| 密码保存方式 | 使用 Windows DPAPI 加密保存 |
| 剪贴板写入方式 | `Clipboard.SetText(password)` |

## 4. 目标效果

用户流程：

1. 打开 VSLoader。
2. 点击主界面 `设置`。
3. 在 `AdminUI 密码` 分类中输入密码。
4. 保存设置。
5. 选中一个快捷项。
6. 点击 `AdminUI`。
7. 程序打开该快捷项对应的 `.jnlp`。
8. 如果已配置 AdminUI 密码，程序立即把密码写入系统剪贴板。
9. 用户在 SwingUI 密码框中按 `Ctrl + V` 粘贴密码。

## 5. 配置模型修改

修改：

```text
Models\AdminUiConfig.cs
```

新增字段：

```csharp
public string ProtectedPassword { get; set; } = string.Empty;
```

说明：

- 该字段保存加密后的密码。
- 不保存明文密码。
- 如果没有配置密码，该字段为空字符串。

`Clone()` 方法需要复制该字段。

## 6. 新增密码保护服务

新增：

```text
Services\PasswordProtectionService.cs
```

职责：

- 将明文密码加密为字符串。
- 将加密字符串解密为明文密码。
- 解密失败时返回失败结果或空字符串。

推荐使用：

```csharp
System.Security.Cryptography.ProtectedData
DataProtectionScope.CurrentUser
```

### 6.1 加密方法

建议方法：

```csharp
public string Protect(string plainText)
```

行为：

- 如果 `plainText` 为空，返回空字符串。
- 使用 UTF-8 转字节。
- 使用 `ProtectedData.Protect` 加密。
- 使用 `Convert.ToBase64String` 转成字符串保存。

### 6.2 解密方法

建议方法：

```csharp
public string Unprotect(string protectedText)
```

行为：

- 如果 `protectedText` 为空，返回空字符串。
- 使用 `Convert.FromBase64String` 转字节。
- 使用 `ProtectedData.Unprotect` 解密。
- 使用 UTF-8 转回字符串。
- 如果解密失败，返回空字符串。

说明：

- 加密范围使用 `CurrentUser`。
- 该密码只能由当前 Windows 用户解密。

## 7. SettingsViewModel 修改

修改：

```text
ViewModels\SettingsViewModel.cs
```

新增依赖：

```csharp
PasswordProtectionService
```

新增属性：

```csharp
[ObservableProperty]
private string adminUiPassword = string.Empty;
```

构造函数中：

- 接收 `AdminUiConfig`。
- 克隆现有 AdminUI 配置。
- 尝试解密 `AdminUi.ProtectedPassword`。
- 将明文密码赋值给 `AdminUiPassword`。

保存时：

```csharp
AdminUi.ProtectedPassword = _passwordProtectionService.Protect(AdminUiPassword);
```

注意：

- `AdminUiPassword` 是设置窗口中的临时明文字段。
- 不要把明文密码写入 `AppConfig`。
- 如果用户清空密码，保存后 `ProtectedPassword` 应为空字符串。

## 8. 设置窗口 UI 修改

修改：

```text
Views\SettingsWindow.xaml
```

在当前 AdminUI 配置区域下方新增一个分类：

```text
AdminUI 密码
```

推荐使用 `GroupBox`：

```xml
<GroupBox Header="AdminUI 密码">
    ...
</GroupBox>
```

字段：

```text
打开 AdminUI 时复制到剪贴板的密码
```

使用 WPF `PasswordBox`。

注意：

- `PasswordBox.Password` 不能直接普通绑定。
- 可以使用 code-behind 辅助同步，或使用附加属性。
- 为了实现简单，可以在 `SettingsWindow.xaml.cs` 中处理 `PasswordChanged`。

### 8.1 推荐实现方式

在 `SettingsWindow.xaml` 中：

```xml
<PasswordBox x:Name="AdminUiPasswordBox"
             PasswordChanged="AdminUiPasswordBox_PasswordChanged" />
```

在 `SettingsWindow.xaml.cs` 中：

```csharp
AdminUiPasswordBox.Password = viewModel.AdminUiPassword;
```

事件：

```csharp
private void AdminUiPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
{
    if (DataContext is SettingsViewModel viewModel)
    {
        viewModel.AdminUiPassword = AdminUiPasswordBox.Password;
    }
}
```

说明：

- 设置窗口打开时显示已保存密码。
- 保存时加密。
- 如果用户不修改密码，也应保留原密码。

## 9. AdminUiService 修改

修改：

```text
Services\AdminUiService.cs
```

当前 `OpenAdminUi` 负责打开 `.jnlp`。

新增逻辑：

- 成功打开 `.jnlp` 后，如果配置了密码，则写入系统剪贴板。

由于密码解密依赖 `PasswordProtectionService`，推荐不让 `AdminUiService` 直接处理密码。

更推荐在 `MainViewModel.OpenAdminUi()` 中处理剪贴板写入。

因此 `AdminUiService.OpenAdminUi()` 可以保持原职责：

```text
只负责打开 .jnlp
```

## 10. MainViewModel 修改

修改：

```text
ViewModels\MainViewModel.cs
```

新增依赖：

```csharp
PasswordProtectionService
```

在 `OpenAdminUi()` 中：

1. 调用 `_adminUiService.OpenAdminUi(...)`。
2. 如果打开失败，显示原有错误提示，直接返回。
3. 如果打开成功：
   - 解密 `_config.AdminUi.ProtectedPassword`。
   - 如果密码非空，调用 `Clipboard.SetText(password)`。
   - 提示：

```text
AdminUI 已打开，密码已复制到剪贴板。
```

4. 如果密码为空，提示：

```text
AdminUI 已打开，但未配置 AdminUI 密码。
```

### 10.1 剪贴板写入

使用：

```csharp
System.Windows.Clipboard.SetText(password);
```

注意：

- 需要在 UI 线程调用。
- 当前 `OpenAdminUi()` 是从 WPF 命令触发，默认在 UI 线程，可以直接调用。
- 剪贴板写入失败时，应弹窗提示。

## 11. 安全说明

虽然配置文件中保存的是 DPAPI 加密后的密码，但点击 `AdminUI` 后，密码会被写入系统剪贴板。

这意味着：

- 剪贴板内容可能被其他程序读取。
- 用户后续复制其他内容会覆盖该密码。
- 程序不自动清空剪贴板。

v8 不实现自动清空剪贴板。

后续版本如果需要，可以增加：

```text
N 秒后自动清空剪贴板
```

## 12. 错误处理

| 场景 | 处理方式 |
| --- | --- |
| 未配置密码 | 打开 AdminUI 后提示未配置密码 |
| 密码解密失败 | 视为未配置密码 |
| 剪贴板写入失败 | 弹窗提示写入剪贴板失败 |
| `.jnlp` 不存在 | 保持现有提示：请先点击“自动获取连接” |
| `.jnlp` 打开失败 | 保持现有错误提示 |
| 用户清空密码并保存 | `ProtectedPassword` 保存为空 |

## 13. 验收标准

- 设置窗口出现 `AdminUI 密码` 分类。
- 用户可以输入 AdminUI 密码。
- 保存后重启程序，密码仍可读取。
- 配置文件中不出现明文密码。
- 配置文件中保存的是加密后的 `ProtectedPassword`。
- 点击 `AdminUI` 成功打开 `.jnlp` 后，密码写入系统剪贴板。
- 用户可以在 SwingUI 密码框中 `Ctrl + V` 粘贴密码。
- 未配置密码时，点击 `AdminUI` 只打开 `.jnlp` 并提示未配置密码。
- `.jnlp` 不存在时，不写入剪贴板。
- 剪贴板写入失败时弹窗提示。
- `dotnet build .\VSLoader.sln` 必须通过。

