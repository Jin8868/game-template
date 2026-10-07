[CmdletBinding()]
param(
    [string]$Platform = 'Windows',
    [string]$Language = 'English(US)'
)

$ErrorActionPreference = 'Stop'
# 保留原有使用入口，元数据转换和文件交付由框架工具完成。
$unityRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$frameworkTools = & (Join-Path $PSScriptRoot 'ResolveFrameworkTools.ps1') -UnityProjectRoot $unityRoot
& (Join-Path $frameworkTools 'ExportAudioContent.ps1') -UnityProjectRoot $unityRoot `
    -Platform $Platform -Language $Language