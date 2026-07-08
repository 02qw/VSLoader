# VSLoader 安装包打包使用指南

## 1. 目标

本项目现在支持一键生成 Windows 安装包。

以后你只需要运行：

```powershell
.\build-installer.ps1 -Version 1.0.1
```

即可自动完成：

1. 清理旧发布目录。
2. 使用 `dotnet publish` 生成 Release 程序文件。
3. 检查规则 CSV 文件是否进入发布目录。
4. 使用 Inno Setup 生成安装包。
5. 输出 `VSLoader_Setup_版本号.exe`。

## 2. 首次准备

### 2.1 安装 Inno Setup

打安装包需要先安装 Inno Setup 6。

安装完成后，脚本会自动尝试寻找：

```text
C:\Program Files (x86)\Inno Setup 6\ISCC.exe
C:\Program Files\Inno Setup 6\ISCC.exe
```

如果你安装到了其他目录，可以打包时手动指定：

```powershell
.\build-installer.ps1 -Version 1.0.1 -IsccPath "D:\Tools\Inno Setup 6\ISCC.exe"
```

### 2.2 确认项目目录

进入项目根目录：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader
```

## 3. 一键打包命令

最常用命令：

```powershell
.\build-installer.ps1 -Version 1.0.0
```

生成的安装包位置：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader\installer\Output\VSLoader_Setup_1.0.0.exe
```

以后发布新版时，只需要改版本号：

```powershell
.\build-installer.ps1 -Version 1.0.1
```

输出：

```text
installer\Output\VSLoader_Setup_1.0.1.exe
```

## 4. 版本号怎么配置

版本号通过命令参数传入：

```powershell
.\build-installer.ps1 -Version 1.2.3
```

这个版本号会同时用于：

1. 安装包文件名。
2. Inno Setup 安装包版本。
3. `VSLoader.exe` 文件版本。
4. 程序程序集版本。

建议版本号格式：

```text
主版本.次版本.修订号
```

例如：

```text
1.0.0
1.0.1
1.1.0
2.0.0
```

## 5. 安装包里包含哪些内容

脚本会先生成：

```text
publish\
```

然后 Inno Setup 会把整个 `publish` 目录打进安装包。

安装后目录类似：

```text
C:\Program Files\VSLoader\
  VSLoader.exe
  VSLoader.dll
  ...
  Config\
    batch-rules.example.csv
    batch-rules.regex.example.csv
```

重点：规则 CSV 文件会安装到程序目录的 `Config` 文件夹中。

## 6. CSV 规则文件的定位

安装目录中的 CSV 文件是模板文件：

```text
Config\batch-rules.example.csv
Config\batch-rules.regex.example.csv
```

建议用户使用方式：

1. 打开安装目录中的 `Config` 文件夹。
2. 复制一份 CSV 到桌面、文档或其他可写目录。
3. 修改复制出来的 CSV。
4. 在 VSLoader 的批量新增识别页面中选择该 CSV。

不建议用户直接修改安装目录里的 CSV。

原因：

如果程序安装在：

```text
C:\Program Files\VSLoader
```

普通用户可能没有写入权限。

而且以后安装新版时，安装目录里的模板 CSV 可能会被新版覆盖。

## 7. 更新安装包如何覆盖旧版本

假如用户已经安装：

```text
VSLoader_Setup_1.0.0.exe
```

你发布新版：

```text
VSLoader_Setup_1.0.1.exe
```

用户直接双击新版安装包安装即可。

安装器会使用同一个 `AppId`，因此会识别为同一个软件，并覆盖旧的程序文件。

用户配置不会丢失，因为运行配置保存在：

```text
%AppData%\VSLoader\config.json
```

AdminUI 下载文件保存在：

```text
%AppData%\VSLoader\UIdownload
```

这些都不在安装目录里，升级安装不会删除。

## 8. 程序运行中时怎么更新

VSLoader 有托盘常驻功能。

如果用户安装新版时 VSLoader 正在运行，安装器会尝试关闭正在运行的程序。

建议实际发版时提醒用户：

```text
安装新版前，请先从托盘右键退出 VSLoader。
```

这样最稳，避免程序文件被占用导致覆盖失败。

## 9. 常用命令

### 9.1 标准自包含安装包

推荐：

```powershell
.\build-installer.ps1 -Version 1.0.0
```

特点：

- 别人电脑不需要单独安装 .NET 运行时。
- 安装包体积会更大。
- 分享体验最省心。

### 9.2 依赖系统 .NET 运行时的安装包

不推荐给普通用户，但可以用于内部测试：

```powershell
.\build-installer.ps1 -Version 1.0.0 -FrameworkDependent
```

特点：

- 安装包更小。
- 用户电脑需要已有对应 .NET Desktop Runtime。

### 9.3 指定 Inno Setup 路径

```powershell
.\build-installer.ps1 -Version 1.0.0 -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

## 10. 常见问题

### 10.1 提示找不到 ISCC.exe

说明没有安装 Inno Setup，或者安装路径不在默认位置。

解决：

1. 安装 Inno Setup 6。
2. 或使用 `-IsccPath` 指定路径。

### 10.2 提示缺少 CSV 文件

脚本会检查：

```text
publish\Config\batch-rules.example.csv
publish\Config\batch-rules.regex.example.csv
```

如果缺少，说明项目发布时没有正确复制 Config 文件。

需要检查：

```text
VSLoader\VSLoader.csproj
```

其中应包含：

```xml
<None Update="Config\batch-rules.example.csv">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

### 10.3 安装新版后用户配置还在吗

还在。

用户配置保存在：

```text
%AppData%\VSLoader
```

安装包只覆盖安装目录里的程序文件，不会覆盖 `%AppData%` 中的用户配置。

## 11. 推荐发版流程

每次准备发给别人时，按这个顺序：

1. 修改代码并确认功能正常。
2. 运行测试：

```powershell
dotnet test .\VSLoader.sln -p:UseSharedCompilation=false
```

3. 生成安装包：

```powershell
.\build-installer.ps1 -Version 1.0.1
```

4. 找到安装包：

```text
installer\Output\VSLoader_Setup_1.0.1.exe
```

5. 把这个 `.exe` 发给别人。

