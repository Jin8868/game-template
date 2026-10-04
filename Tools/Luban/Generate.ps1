[CmdletBinding()]
param([switch]$DataOnly)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$configurationRoot = Join-Path $repositoryRoot 'DesignData\Luban'
$unityProjectRoot = Join-Path $repositoryRoot 'UnityProj'
$enumDefinitionGenerator = Join-Path $PSScriptRoot 'GenerateEnumDefines.ps1'
$lubanExecutable = Join-Path $PSScriptRoot 'Luban\Luban.exe'
$outputCodeDirectory = Join-Path $unityProjectRoot 'Assets\Scripts\Runtime\Generated\Config'
$outputDataDirectory = Join-Path $unityProjectRoot 'Assets\Res\Config\Luban'
if (-not (Test-Path -LiteralPath $enumDefinitionGenerator)) { throw "未找到枚举定义生成脚本：$enumDefinitionGenerator" }
& $enumDefinitionGenerator
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path -LiteralPath $lubanExecutable)) { throw "未找到 Luban CLI：$lubanExecutable" }
$generationArguments = @('-t', 'client', '-d', 'bin', '--strict', '--conf', (Join-Path $configurationRoot 'luban.conf'), '-x', "outputDataDir=$outputDataDirectory")
if (-not $DataOnly) { $generationArguments += '-c'; $generationArguments += 'cs-bin'; $generationArguments += '-x'; $generationArguments += "outputCodeDir=$outputCodeDirectory" }
& $lubanExecutable @generationArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }