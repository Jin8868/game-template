[CmdletBinding()]
param(
    [string]$WwiseInstallationPath,
    [string]$Platform = 'Windows'
)

$ErrorActionPreference = 'Stop'
$unityProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$assetsRoot = Join-Path $unityProjectRoot 'Assets'
[xml]$settings = Get-Content (Join-Path $assetsRoot 'WwiseSettings.xml') -Raw -Encoding UTF8
$projectPath = [IO.Path]::GetFullPath((Join-Path $assetsRoot $settings.WwiseSettings.WwiseProjectPath))
$rootOutputPath = [IO.Path]::GetFullPath((Join-Path $assetsRoot $settings.WwiseSettings.RootOutputPath))

# 优先使用命令行或环境变量，避免其他机器依赖提交者的安装位置。
if ([string]::IsNullOrWhiteSpace($WwiseInstallationPath)) {
    $WwiseInstallationPath = $env:WWISE_INSTALLATION_PATH
}
if ([string]::IsNullOrWhiteSpace($WwiseInstallationPath)) {
    $WwiseInstallationPath = $settings.WwiseSettings.WwiseInstallationPathWindows
}
$consolePath = Join-Path $WwiseInstallationPath 'Authoring/x64/Release/bin/WwiseConsole.exe'
if (-not (Test-Path -LiteralPath $consolePath -PathType Leaf)) {
    throw '找不到 WwiseConsole.exe，请通过 -WwiseInstallationPath 指定 Wwise 2025.1.11.9262 安装目录。'
}
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "找不到 Wwise 工程：$projectPath"
}

# 从工程平台列表验证名称，避免拼错平台或将平台名当作路径使用。
[xml]$project = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
$platformDefinition = @($project.WwiseDocument.ProjectInfo.Project.Platforms.Platform) |
    Where-Object { $_.Name -ceq $Platform } | Select-Object -First 1
if ($null -eq $platformDefinition) {
    throw "Wwise 工程没有配置平台：$Platform"
}

# 沿用工程输出设置，确保编辑器与构建前复制使用同一份资源。
& $consolePath generate-soundbank $projectPath --platform $Platform --no-source-control
$generationExitCode = $LASTEXITCODE
if ($generationExitCode -notin @(0, 1)) {
    throw "SoundBank 生成失败，退出码：$generationExitCode"
}
if ($generationExitCode -eq 1) {
    Write-Warning 'Wwise 生成时出现警告，请检查上方输出。'
}
$projectInfoPath = Join-Path $rootOutputPath 'ProjectInfo.json'
if (-not (Test-Path -LiteralPath $projectInfoPath -PathType Leaf)) {
    throw "生成后仍缺少 ProjectInfo.json，请检查 Wwise 工程 Root Output Path：$rootOutputPath"
}
$projectInfo = Get-Content -LiteralPath $projectInfoPath -Raw -Encoding UTF8 | ConvertFrom-Json
$generatedPlatform = $projectInfo.ProjectInfo.Platforms |
    Where-Object { $_.Name -ceq $Platform } | Select-Object -First 1
if ($null -eq $generatedPlatform) {
    throw "ProjectInfo.json 中没有平台：$Platform"
}
$sourcePath = [IO.Path]::GetFullPath((Join-Path $rootOutputPath $generatedPlatform.Path))
if (-not (Test-Path -LiteralPath (Join-Path $sourcePath 'Init.bnk') -PathType Leaf)) {
    throw "生成后仍缺少 Init.bnk：$sourcePath"
}

# 同步一份供直接运行使用；正式构建时 Wwise 插件会重新复制平台资源。
$streamingRoot = Join-Path $assetsRoot 'StreamingAssets'
$bankRoot = Join-Path $streamingRoot $settings.WwiseSettings.WwiseStreamingAssetsPath
$destinationPath = Join-Path $bankRoot $Platform
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
Get-ChildItem -LiteralPath $sourcePath -Force | Copy-Item -Destination $destinationPath -Recurse -Force
Write-Host "SoundBank 已生成：$sourcePath"
Write-Host "SoundBank 已复制：$destinationPath"
