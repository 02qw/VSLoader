param(
    [string]$Version = "3.3.5",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$UpdateOutputDir = ".\release-update",
    [string]$ReleaseNotes = 
"
更新日志:
-修复节点边缘连线超出
-自动登录加强版
-小范围UI重构
"
#History
# -自动登录AdminUI
# -独立地图和主程序窗口
# -优化更新逻辑
# -优化主界面列排布&调整显示顺序
# -log日志写入设置2000条上限
# -修改AdminUI密码为明文配置
# -优化更新按钮提示样式
# -修复地图关闭后无法恢复状态
# -修复地图全屏模式和窗口模式的最下角状态不一致现象
# -优化工作区界面逻辑
# -优化地图背景颜色
# -重构UI设计
# -地图增添自动获取连接功能
# -修复了地图自动获取连接窗口位置显示错误
# -添加了自动更新功能
# -修复关闭VSLoader文件残留问题
# -地图画布节点区分度优化
# -地图编辑区域做网格线对齐
# -增加地图快捷键
# -修复地图最小化任然残留窗口的问题
# -新增手动检测更新状态按钮
# -重构地图窗口逻辑
# -优化滚动条逻辑
# -修复地图最小化bug
# -修复地图窗口缩放比例显示错误
# -修复在网络连接断开时监测崩溃问题
# -修复快捷键下地图和主程序窗口间的层级关系
# -修复编辑/新增导致程序崩溃问题
# -修复输入框无法失焦问题
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $root "publish"
$updateOutputPath = if ([System.IO.Path]::IsPathRooted($UpdateOutputDir)) {
    $UpdateOutputDir
}
else {
    Join-Path $root $UpdateOutputDir
}

$zipFileName = "VSLoader_$Version`_$Runtime.zip"
$zipPath = Join-Path $updateOutputPath $zipFileName
$manifestPath = Join-Path $updateOutputPath "manifest.json"

function Assert-FileExists {
    param(
        [string]$Path,
        [string]$Message
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw $Message
    }
}

Write-Host "VSLoader 发布包生成开始" -ForegroundColor Cyan
Write-Host "版本号：$Version"
Write-Host "运行时：$Runtime"
Write-Host "配置：$Configuration"
Write-Host "更新包输出目录：$updateOutputPath"

Set-Location $root

Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force

& (Join-Path $root "build-installer.ps1") `
    -Version $Version `
    -Runtime $Runtime `
    -Configuration $Configuration

Assert-FileExists -Path (Join-Path $publishDir "VSLoader.exe") -Message "publish 目录缺少 VSLoader.exe。"
Assert-FileExists -Path (Join-Path $publishDir "VSLoader.Updater.exe") -Message "publish 目录缺少 VSLoader.Updater.exe。"

if (Test-Path -LiteralPath $updateOutputPath) {
    Remove-Item -LiteralPath $updateOutputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $updateOutputPath | Out-Null

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = [ordered]@{
    version = $Version
    packageFile = $zipFileName
    sha256 = $hash
    releaseNotes = $ReleaseNotes
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "发布包生成完成：" -ForegroundColor Green
Write-Host "自动更新目录：$updateOutputPath"
Write-Host "更新 zip：$zipPath"
Write-Host "manifest：$manifestPath"
Write-Host "安装包目录：$(Join-Path $root 'installer\Output')"
