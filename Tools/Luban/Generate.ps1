[CmdletBinding()]
param([switch]$DataOnly)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$configurationRoot = Join-Path $repositoryRoot 'DesignData\Luban'
$unityProjectRoot = Join-Path $repositoryRoot 'UnityProj'
$enumDefinitionGenerator = Join-Path $PSScriptRoot 'GenerateEnumDefines.ps1'
$lubanExecutable = Join-Path $PSScriptRoot 'Luban\Luban.exe'
$outputCodeDirectory = Join-Path $unityProjectRoot 'Assets\Scripts\Runtime\Generated\Config'
$outputDataDirectory = Join-Path $unityProjectRoot 'Assets\Res\Config\Luban'
if (-not (Test-Path -LiteralPath $enumDefinitionGenerator)) { throw "Enum generator not found: $enumDefinitionGenerator" }
Write-Output '[Luban] Generating enum definitions...'
& $enumDefinitionGenerator
if (-not (Test-Path -LiteralPath $lubanExecutable)) { throw "Luban CLI not found: $lubanExecutable" }
Write-Output '[Luban] Generating config code and binary data...'
$generationArguments = @('-t', 'client', '-d', 'bin', '--strict', '--conf', (Join-Path $configurationRoot 'luban.conf'), '-x', "outputDataDir=$outputDataDirectory")
if (-not $DataOnly) { $generationArguments += '-c'; $generationArguments += 'cs-bin'; $generationArguments += '-x'; $generationArguments += "outputCodeDir=$outputCodeDirectory" }
& $lubanExecutable @generationArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output '[Luban] Generation completed.'
