[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "[setup] $Message" -ForegroundColor Cyan
}

function Invoke-Git {
    param(
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    & git -C $WorkingDirectory @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed in '$WorkingDirectory': git $($Arguments -join ' ')"
    }
}

$repoRoot = $PSScriptRoot
$configPath = Join-Path $repoRoot 'bootstrap.json'
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Missing bootstrap configuration: $configPath"
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$frameworkPath = Join-Path $repoRoot $config.framework.directory
$unityProjectPath = Join-Path $repoRoot $config.unity.projectDirectory
$projectVersionPath = Join-Path $unityProjectPath 'ProjectSettings/ProjectVersion.txt'

Write-Step 'Checking Git'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git was not found. Install Git and run setup.bat again.'
}
& git --version

if (-not (Test-Path -LiteralPath $unityProjectPath -PathType Container)) {
    throw "Unity project directory is missing: $unityProjectPath"
}

Write-Step 'Checking the Unity project version'
if (-not (Test-Path -LiteralPath $projectVersionPath -PathType Leaf)) {
    throw "Unity ProjectVersion.txt is missing: $projectVersionPath"
}
$versionLine = Get-Content -LiteralPath $projectVersionPath | Select-Object -First 1
$actualUnityVersion = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()
if ($actualUnityVersion -ne $config.unity.version) {
    throw "Unity version mismatch. Expected $($config.unity.version), project uses $actualUnityVersion."
}
Write-Host "[ok] Unity $actualUnityVersion"

if (-not (Test-Path -LiteralPath $frameworkPath)) {
    Write-Step "Cloning Alloy Framework into '$($config.framework.directory)'"
    & git clone --branch $config.framework.branch --single-branch $config.framework.repository $frameworkPath
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to clone Alloy Framework.'
    }
}
elseif (-not (Test-Path -LiteralPath (Join-Path $frameworkPath '.git') -PathType Container)) {
    throw "The framework path exists but is not a Git repository: $frameworkPath"
}
else {
    Write-Step 'Updating the existing Alloy Framework checkout'
    $originUrl = (& git -C $frameworkPath remote get-url origin).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "The framework repository has no origin remote: $frameworkPath"
    }
    if ($originUrl -ne $config.framework.repository) {
        throw "Unexpected Alloy Framework origin '$originUrl'. Expected '$($config.framework.repository)'."
    }

    Invoke-Git -WorkingDirectory $frameworkPath -Arguments @('fetch', '--prune', 'origin')
    $changes = & git -C $frameworkPath status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the Alloy Framework working tree.'
    }
    $currentBranch = (& git -C $frameworkPath branch --show-current).Trim()
    if ($changes) {
        Write-Warning 'Alloy Framework has local changes; fetch completed, automatic pull was skipped.'
    }
    elseif ($currentBranch -ne $config.framework.branch) {
        Write-Warning "Alloy Framework is on branch '$currentBranch'; fetch completed, automatic pull was skipped."
    }
    else {
        Invoke-Git -WorkingDirectory $frameworkPath -Arguments @('pull', '--rebase', 'origin', $config.framework.branch)
    }
}

Write-Step 'Applying repository-local Git settings'
$gitRepositories = @($repoRoot, $frameworkPath)
foreach ($gitRepository in $gitRepositories) {
    if (Test-Path -LiteralPath (Join-Path $gitRepository '.git')) {
        Invoke-Git -WorkingDirectory $gitRepository -Arguments @('config', '--local', 'pull.rebase', $config.git.pullRebase.ToString().ToLowerInvariant())
        Invoke-Git -WorkingDirectory $gitRepository -Arguments @('config', '--local', 'fetch.prune', $config.git.fetchPrune.ToString().ToLowerInvariant())
    }
}

$gitUserName = & git config --get user.name
$gitUserEmail = & git config --get user.email
if (-not $gitUserName -or -not $gitUserEmail) {
    Write-Warning 'Git user.name or user.email is not configured. Configure both before committing.'
}

Write-Host ''
Write-Host '[ok] Setup completed.' -ForegroundColor Green
Write-Host "Unity project: $unityProjectPath"
Write-Host "Framework:     $frameworkPath"

