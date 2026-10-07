[CmdletBinding()]
param(
    [string]$WwiseInstallationPath,
    [string]$Platform = 'Windows'
)

$ErrorActionPreference = 'Stop'
# 项目入口只传递项目目录，制作与导出实现位于框架中。
$unityRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$frameworkTools = & (Join-Path $PSScriptRoot 'ResolveFrameworkTools.ps1') -UnityProjectRoot $unityRoot
& (Join-Path $frameworkTools 'GenerateSoundBanks.ps1') -UnityProjectRoot $unityRoot `
    -WwiseInstallationPath $WwiseInstallationPath -Platform $Platform