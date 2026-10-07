param([Parameter(Mandatory = $true)][string]$UnityProjectRoot)

$ErrorActionPreference = 'Stop'
$packagesRoot = Join-Path $UnityProjectRoot 'Packages'
$manifest = Get-Content -LiteralPath (Join-Path $packagesRoot 'manifest.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json
$dependency = [string]$manifest.dependencies.'com.alloy.framework'
if ($dependency.StartsWith('file:', [StringComparison]::Ordinal)) {
    $packagePath = $dependency.Substring(5)
    if (-not [IO.Path]::IsPathRooted($packagePath)) { $packagePath = Join-Path $packagesRoot $packagePath }
    $frameworkRoot = [IO.Path]::GetFullPath($packagePath)
} else {
    $frameworkRoot = Join-Path $packagesRoot 'com.alloy.framework'
}
$toolsPath = Join-Path $frameworkRoot 'Tools~/Wwise'
if (-not (Test-Path -LiteralPath $toolsPath -PathType Container)) {
    throw '找不到框架 Wwise 工具，请确认 com.alloy.framework 本地包路径及框架版本。'
}
return $toolsPath