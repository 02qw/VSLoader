param(
    [string]$Version = "3.0.4",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$UpdateOutputDir = ".\release-update",
    [string]$ReleaseNotes = 
    "
    V3重构了整个项目，修复了许多已知问题。
    -3.0.2 修复了最新版本提示错误的问题。
    -3.0.3 修复了设置配置界面滚动失灵的问题。
    -3.0.4 updater 的更新内容展示bug修复。
    "
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
