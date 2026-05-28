# VSLoader 打包分享操作指南

## 1. 目标

把当前 VSLoader 项目打包成一个别人可以直接体验的 Windows 程序。

推荐使用：

```text
自包含发布版本 + zip 压缩包
```

这样对方电脑通常不需要提前安装 .NET 8，解压后双击 `VSLoader.exe` 就能运行。

## 2. 打包前确认

项目根目录是：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader
```

确认项目可以正常构建：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader
dotnet build .\VSLoader.sln
```

如果看到类似：

```text
已成功生成。
0 个警告
0 个错误
```

说明项目可以正常打包。

## 3. 生成可分享版本

在 PowerShell 中执行：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader
dotnet publish .\VSLoader\VSLoader.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

命令含义：

| 参数 | 说明 |
| --- | --- |
| `-c Release` | 使用正式发布模式 |
| `-r win-x64` | 生成 64 位 Windows 程序 |
| `--self-contained true` | 把 .NET 运行时一起打进去 |
| `PublishSingleFile=true` | 尽量发布成单个主程序文件 |
| `IncludeNativeLibrariesForSelfExtract=true` | 让自包含单文件程序更稳定 |

## 4. 找到发布目录

发布完成后，打开这个目录：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader\bin\Release\net8.0-windows\win-x64\publish
```

里面应该可以看到：

```text
VSLoader.exe
batch-rules.example.csv
```

`VSLoader.exe` 是要发给别人的程序。

`batch-rules.example.csv` 是批量新增识别功能的 CSV 示例文件。

## 5. 压缩发送给别人

不要只单独发送 `VSLoader.exe`。

推荐做法：

1. 打开发布目录：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader\bin\Release\net8.0-windows\win-x64\publish
```

2. 选中整个 `publish` 文件夹。
3. 右键压缩成：

```text
VSLoader.zip
```

4. 把 `VSLoader.zip` 发给别人。

## 6. 对方怎么使用

对方收到 zip 后：

1. 解压 `VSLoader.zip`。
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

## 7. 批量新增识别 CSV 示例

发布目录中会带一个示例文件：

```text
batch-rules.example.csv
```

内容类似：

```csv
MatchType,Pattern,DisplayName,NameTemplate
Contains,TSSM,xxx,{DisplayName}_{FolderName}
Contains,TSSP,yyy,{DisplayName}_{FolderName}
Contains,TRSM,zzz,{DisplayName}_{FolderName}
Contains,TPSM,aaa,{DisplayName}_{FolderName}
```

例如文件夹：

```text
8812_TSSM001
```

命中规则：

```text
Contains,TSSM,xxx,{DisplayName}_{FolderName}
```

生成快捷项名称：

```text
xxx_8812_TSSM001
```

## 8. 对方的数据保存在哪里

每个用户自己的配置保存在自己电脑上：

```text
%AppData%\VSLoader\config.json
```

例如：

```text
C:\Users\对方用户名\AppData\Roaming\VSLoader\config.json
```

所以：

- 你发出去的程序不包含你的快捷项配置。
- 对方第一次运行需要自己配置 VSCode 路径。
- 对方新增的快捷项只保存在对方电脑上。

## 9. 常见问题

### 9.1 双击没反应

可以让对方在 PowerShell 中运行：

```powershell
.\VSLoader.exe
```

这样如果有错误，PowerShell 里通常能看到提示。

### 9.2 Windows 提示未知发布者

这是正常的。

因为当前程序没有做代码签名证书。

对方可以选择：

```text
更多信息 -> 仍要运行
```

正式给更多人使用时，可以考虑购买代码签名证书。

### 9.3 程序打不开网络路径

可能原因：

- 对方电脑访问不了该网络地址。
- 对方没有共享目录权限。
- 对方没有连接公司网络或 VPN。
- 目标路径本身不存在。

可以先让对方在 Windows 文件资源管理器中打开对应路径测试。

### 9.4 找不到 VSCode 路径

常见路径：

```text
C:\Users\用户名\AppData\Local\Programs\Microsoft VS Code\Code.exe
```

也可以右键 VSCode 桌面快捷方式，查看“目标”路径。

## 10. 后续如果要做正式安装包

当前 zip 分享方式适合测试和小范围体验。

如果后续要正式发布，可以考虑：

- Inno Setup 安装包。
- MSI 安装包。
- MSIX 安装包。
- 添加桌面快捷方式。
- 添加开始菜单入口。
- 添加代码签名证书。

