# VSLoader 打包分享操作指南

## 1. 最简单用法

以后只需要运行这个脚本：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader

.\build-release.ps1
```

默认版本号写在脚本开头：

```powershell
[string]$Version = "2.0.1"
```

发新版时，改这个版本号，然后重新运行脚本即可。

也可以不改脚本，直接这样指定版本：

```powershell
.\build-release.ps1 -Version "2.0.2"
```

## 2. 脚本会自动做什么

`build-release.ps1` 会自动完成：

```text
1. 关闭正在运行的 VSLoader。
2. 调用 build-installer.ps1 生成 publish 目录。
3. 确认 publish 里有 VSLoader.exe。
4. 确认 publish 里有 VSLoader.Updater.exe。
5. 把 publish 压缩成 VSLoader_版本号_win-x64.zip。
6. 自动计算 zip 的 SHA256。
7. 自动生成 manifest.json。
8. 同时保留安装包输出。
```

## 3. 生成结果在哪里

运行完成后，重点看这个目录：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader\release-update
```

里面会有：

```text
manifest.json
VSLoader_版本号_win-x64.zip
```

这两个文件就是自动更新要放到服务器上的文件。

安装包在：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader\installer\Output
```

## 4. 放到服务器

把 `release-update` 里的两个文件复制到你的更新目录，例如：

```text
\\服务器\VSLoaderUpdate\
    manifest.json
    VSLoader_2.0.2_win-x64.zip
```

用户软件里配置：

```text
设置 -> 软件更新 -> manifest 路径
```

填写：

```text
\\服务器\VSLoaderUpdate\manifest.json
```

以后用户点击：

```text
更新软件
```

就会自动更新。

## 5. 需要注意

自动更新用的是：

```text
release-update\VSLoader_版本号_win-x64.zip
release-update\manifest.json
```

不是安装包 exe。

安装包 exe 只用于手动安装。

如果更新失败，优先检查：

```text
1. 服务器上有没有 manifest.json。
2. 服务器上有没有 zip。
3. manifest.json 里的 packageFile 是否等于 zip 文件名。
4. zip 里面是否包含 VSLoader.exe 和 VSLoader.Updater.exe。
```

更新器错误日志在：

```text
%LocalAppData%\VSLoader\errorLog
```
