# VSLoader 免安装版打包分享操作指南

## 1. 目标

把当前 VSLoader 项目打包成一个别人可以直接体验的 Windows 免安装程序。

推荐使用：

```text
自包含发布版本 + zip 压缩包
```

这样对方电脑通常不需要提前安装 .NET 8，解压后双击 `VSLoader.exe` 就能运行。

本指南重点说明：

```text
如何设置版本号，并让程序标题和压缩包文件名都带上这个版本号。
```

## 2. 打包前确认

项目根目录是：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader
```

先关闭正在运行的 VSLoader，否则编译时可能出现 `VSLoader.exe` 被占用。

可以在 PowerShell 中执行：

```powershell
Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force
```

然后确认项目可以正常构建：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader
dotnet build .\VSLoader.sln -p:UseSharedCompilation=false
```

如果看到类似：

```text
已成功生成。
0 个错误
```

说明项目可以正常打包。

## 3. 设置版本号

免安装版打包时，推荐先在 PowerShell 中设置一个版本号变量：

```powershell
$Version = "1.9.2"
```

版本号建议使用这种格式：

```text
主版本.次版本.修订版本
```

例如：

```text
1.9.2
1.9.3
1.10.0
```

这个版本号会影响：

```text
1. 程序标题栏显示的版本号，例如 VSLoader v1.9.2。
2. 程序文件版本信息。
3. 最后生成的 zip 文件名，例如 VSLoader_1.9.2_win-x64.zip。
```

注意：

```text
修改 $Version 后，不需要手动改 C# 代码。
```

## 4. 生成免安装发布目录

在 PowerShell 中执行：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader

$Version = "1.9.2"
$Runtime = "win-x64"
$PublishDir = ".\publish\VSLoader_$Version`_$Runtime"

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

dotnet publish .\VSLoader\VSLoader.csproj `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -o $PublishDir
```

命令含义：

| 参数 | 说明 |
| --- | --- |
| `$Version = "1.9.2"` | 本次发布的软件版本号 |
| `$Runtime = "win-x64"` | 生成 64 位 Windows 程序 |
| `$PublishDir` | 本次免安装版输出目录 |
| `-c Release` | 使用正式发布模式 |
| `--self-contained true` | 把 .NET 运行时一起打进去 |
| `PublishSingleFile=false` | 使用多文件发布，更适合带配置示例文件一起分享 |
| `Version` | 程序产品版本 |
| `AssemblyVersion` | 程序程序集版本，标题栏会读取这个版本 |
| `FileVersion` | exe 文件版本 |

## 5. 找到发布目录

发布完成后，打开：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader\publish\VSLoader_1.9.2_win-x64
```

其中 `1.9.2` 会根据你设置的 `$Version` 变化。

目录里应该能看到：

```text
VSLoader.exe
Config\
```

`VSLoader.exe` 是程序主入口。

`Config` 文件夹里会包含规则示例文件，例如：

```text
Config\batch-rules.example.csv
Config\batch-rules.regex.example.csv
Config\batch-rules.v3.zam.example.csv
Config\batch-rules.v4.module-map.example.csv
Config\factory-map.example.json
```

这些文件可以给用户作为批量新增识别、模块映射、地图配置的参考。

## 6. 压缩成可分享 zip

继续在 PowerShell 中执行：

```powershell
$ZipPath = ".\publish\VSLoader_$Version`_$Runtime.zip"

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath
```

生成结果类似：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader\publish\VSLoader_1.9.2_win-x64.zip
```

把这个 zip 文件发给别人即可。

## 7. 一整套可直接复制的命令

如果你只想快速打一个免安装包，可以直接复制下面这一整段：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader

Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force

$Version = "1.9.2"
$Runtime = "win-x64"
$PublishDir = ".\publish\VSLoader_$Version`_$Runtime"
$ZipPath = ".\publish\VSLoader_$Version`_$Runtime.zip"

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

dotnet publish .\VSLoader\VSLoader.csproj `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -o $PublishDir

Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath

Write-Host "免安装版已生成：$ZipPath"
```

每次更新版本时，只需要改这一行：

```powershell
$Version = "1.9.2"
```

例如下一版改成：

```powershell
$Version = "1.9.3"
```

## 8. 对方怎么使用

对方收到 zip 后：

1. 解压 `VSLoader_版本号_win-x64.zip`。
2. 进入解压后的文件夹。
3. 双击：

```text
VSLoader.exe
```

4. 第一次打开后，点击“设置”。
5. 选择对方自己电脑上的 VSCode 程序路径，例如：

```text
C:\Users\对方用户名\AppData\Local\Programs\Microsoft VS Code\Code.exe
```

6. 保存后，就可以新增快捷项或使用“批量新增识别”。

## 9. 对方的数据保存在哪里

每个用户自己的配置保存在自己电脑上：

```text
%AppData%\VSLoader\config.json
```

例如：

```text
C:\Users\对方用户名\AppData\Roaming\VSLoader\config.json
```

窗口位置配置保存在：

```text
%AppData%\VSLoader\window-layout.json
```

AdminUI 下载文件默认保存在：

```text
%AppData%\VSLoader\UIdownload
```

所以：

```text
1. 你发出去的免安装程序不包含你的个人快捷项配置。
2. 对方第一次运行需要自己配置 VSCode 路径。
3. 对方新增的快捷项只保存在对方电脑上。
4. 更新免安装 zip 后，覆盖程序文件通常不会删除对方 AppData 里的配置。
```

## 10. 更新免安装版怎么发

当你改完代码，要发新版时：

1. 修改 `$Version`。
2. 重新执行第 7 节的一整套命令。
3. 把新生成的 zip 发给对方。
4. 对方关闭旧版 VSLoader。
5. 解压新版 zip。
6. 用新版文件夹中的 `VSLoader.exe` 启动。

如果对方想覆盖旧文件夹：

```text
先完全退出旧版 VSLoader，再把新版解压内容覆盖旧文件夹。
```

注意：

```text
如果旧版程序还在托盘区运行，覆盖文件可能失败。
需要先在托盘区右键退出 VSLoader。
```

## 11. 常见问题

### 11.1 双击没反应

可以让对方在 PowerShell 中运行：

```powershell
.\VSLoader.exe
```

这样如果有错误，PowerShell 里通常能看到提示。

### 11.2 Windows 提示未知发布者

这是正常的。

因为当前程序没有做代码签名证书。

对方可以选择：

```text
更多信息 -> 仍要运行
```

正式给更多人使用时，可以考虑购买代码签名证书。

### 11.3 程序打不开网络路径

可能原因：

```text
1. 对方电脑访问不了该网络地址。
2. 对方没有共享目录权限。
3. 对方没有连接公司网络或 VPN。
4. 目标路径本身不存在。
```

可以先让对方在 Windows 文件资源管理器中打开对应路径测试。

### 11.4 找不到 VSCode 路径

常见路径：

```text
C:\Users\用户名\AppData\Local\Programs\Microsoft VS Code\Code.exe
```

也可以右键 VSCode 桌面快捷方式，查看“目标”路径。

### 11.5 标题栏版本号没有变化

优先检查打包命令里有没有设置：

```powershell
-p:Version=$Version
-p:AssemblyVersion=$Version
-p:FileVersion=$Version
```

如果没有这三项，程序标题栏可能还是旧版本。

另外确认你运行的是新发布目录里的：

```text
publish\VSLoader_版本号_win-x64\VSLoader.exe
```

不要误运行旧的 Debug 目录：

```text
VSLoader\bin\Debug\net8.0-windows\VSLoader.exe
```

## 12. 免安装版和安装包版的区别

免安装 zip：

```text
1. 适合内部测试、小范围快速分享。
2. 对方解压即可运行。
3. 不自动创建开始菜单和桌面快捷方式。
4. 更新时需要手动覆盖或换文件夹。
```

安装包：

```text
1. 适合正式发给更多人。
2. 可以创建开始菜单和桌面快捷方式。
3. 后续新版本安装包可以覆盖旧版本程序文件。
4. 需要 Inno Setup。
```

如果要生成安装包，请看：

```text
VSLoader_安装包打包使用指南.md
```
