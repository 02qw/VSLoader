param(
    [string]$Version = "2.0.1",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$FrameworkDependent,
    [string]$Publisher = "shee",
    [string]$IsccPath = "C:\Users\shee_\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
    [string]$CodeCompareRoot = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $root "VSLoader\VSLoader.csproj"
$updaterProjectPath = Join-Path $root "VSLoader.Updater\VSLoader.Updater.csproj"
$publishDir = Join-Path $root "publish"
$updaterPublishDir = Join-Path $root "publish-updater"
$installerDir = Join-Path $root "installer"
$issPath = Join-Path $installerDir "VSLoader.iss"
$appName = "VSLoader"
$outputBaseFilename = "VSLoader_Setup_$Version"
$codeCompareProjectPath = if ([string]::IsNullOrWhiteSpace($CodeCompareRoot)) {
    Join-Path (Split-Path -Parent $root) "upAndown\src\UpAndown.CodeCompare\UpAndown.CodeCompare.csproj"
}
else {
    Join-Path $CodeCompareRoot "src\UpAndown.CodeCompare\UpAndown.CodeCompare.csproj"
}

function Resolve-IsccPath {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (Test-Path -LiteralPath $ExplicitPath) {
            return (Resolve-Path -LiteralPath $ExplicitPath).Path
        }

        throw "找不到指定的 Inno Setup 编译器：$ExplicitPath"
    }

    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidatePaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path -LiteralPath $candidatePath) {
            return $candidatePath
        }
    }

    throw "未找到 Inno Setup 编译器 ISCC.exe。请先安装 Inno Setup 6，或使用 -IsccPath 指定 ISCC.exe 路径。"
}

function Assert-FileExists {
    param(
        [string]$Path,
        [string]$Message
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw $Message
    }
}

Write-Host "VSLoader 安装包构建开始" -ForegroundColor Cyan
Write-Host "版本号：$Version"
Write-Host "配置：$Configuration"
Write-Host "运行时：$Runtime"

Assert-FileExists -Path $projectPath -Message "找不到项目文件：$projectPath"
Assert-FileExists -Path $updaterProjectPath -Message "找不到更新器项目文件：$updaterProjectPath"
Assert-FileExists -Path $issPath -Message "找不到 Inno Setup 脚本：$issPath"

if (Test-Path -LiteralPath $publishDir) {
    Write-Host "清理旧发布目录：$publishDir"
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $updaterPublishDir) {
    Write-Host "清理旧更新器发布目录：$updaterPublishDir"
    Remove-Item -LiteralPath $updaterPublishDir -Recurse -Force
}

$selfContained = -not $FrameworkDependent.IsPresent
Write-Host "正在发布 .NET 程序..."
dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained:$selfContained `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败。"
}

Assert-FileExists -Path $codeCompareProjectPath -Message "找不到代码对比工具项目：$codeCompareProjectPath"
$codeCompareOutputDir = Join-Path $publishDir "Tools\CodeCompare"
if (Test-Path -LiteralPath $codeCompareOutputDir) {
    Remove-Item -LiteralPath $codeCompareOutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $codeCompareOutputDir -Force | Out-Null

Write-Host "正在发布代码对比工具..."
dotnet publish $codeCompareProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained:$selfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -o $codeCompareOutputDir

if ($LASTEXITCODE -ne 0) {
    throw "代码对比工具发布失败。"
}

Assert-FileExists -Path (Join-Path $codeCompareOutputDir "UpAndown.CodeCompare.exe") -Message "代码对比工具发布目录缺少 UpAndown.CodeCompare.exe。"

Write-Host "正在发布更新器..."
dotnet publish $updaterProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained:$selfContained `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -o $updaterPublishDir

if ($LASTEXITCODE -ne 0) {
    throw "更新器 dotnet publish 失败。"
}

Copy-Item -Path (Join-Path $updaterPublishDir "*") -Destination $publishDir -Recurse -Force

Assert-FileExists -Path (Join-Path $publishDir "VSLoader.exe") -Message "发布目录缺少 VSLoader.exe。"
Assert-FileExists -Path (Join-Path $publishDir "VSLoader.Updater.exe") -Message "发布目录缺少 VSLoader.Updater.exe。"
Assert-FileExists -Path (Join-Path $publishDir "Config\batch-rules.example.csv") -Message "发布目录缺少 Config\batch-rules.example.csv。"
Assert-FileExists -Path (Join-Path $publishDir "Config\batch-rules.regex.example.csv") -Message "发布目录缺少 Config\batch-rules.regex.example.csv。"

$resolvedIsccPath = Resolve-IsccPath -ExplicitPath $IsccPath
Write-Host "使用 Inno Setup：$resolvedIsccPath"
Write-Host "正在生成安装包..."

& $resolvedIsccPath `
    "/DAppName=$appName" `
    "/DAppVersion=$Version" `
    "/DPublisher=$Publisher" `
    "/DOutputBaseFilename=$outputBaseFilename" `
    "/DSourceDir=$publishDir" `
    $issPath

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup 打包失败。"
}

$setupPath = Join-Path $installerDir "Output\$outputBaseFilename.exe"
Assert-FileExists -Path $setupPath -Message "安装包生成后未找到：$setupPath"

Write-Host "安装包构建完成：" -ForegroundColor Green
Write-Host $setupPath
