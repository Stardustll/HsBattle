param(
    [string]$SourceDll,
    [string]$TargetDir,
    [string]$TargetFile
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($SourceDll)) {
    $SourceDll = $env:HSBATTLE_INSTALL_SOURCE_DLL
}

if ([string]::IsNullOrWhiteSpace($SourceDll)) {
    $SourceDll = Join-Path $PSScriptRoot "Release\HsBattle.dll"
}

if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = $env:HSBATTLE_INSTALL_TARGET_DIR
}

if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = Join-Path $PSScriptRoot "Release"
}

if ([string]::IsNullOrWhiteSpace($TargetFile)) {
    $TargetFile = $env:HSBATTLE_INSTALL_TARGET_FILE
}

if ([string]::IsNullOrWhiteSpace($TargetFile)) {
    $TargetFile = "HsBattle.dll"
}

$pluginName = [System.IO.Path]::GetFileNameWithoutExtension($TargetFile)

if (-not (Test-Path -LiteralPath $SourceDll)) {
    Write-Output "install.bat: build output not found: `"$SourceDll`""
    exit 0
}

$candidateRoots = @()
if (-not [string]::IsNullOrWhiteSpace($env:HSBATTLE_HEARTHSTONE_DIR)) {
    $candidateRoots += $env:HSBATTLE_HEARTHSTONE_DIR
}

if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles_x86)) {
    $candidateRoots += (Join-Path $env:ProgramFiles_x86 "Hearthstone")
}
elseif (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
    $candidateRoots += (Join-Path ${env:ProgramFiles(x86)} "Hearthstone")
}

if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    $candidateRoots += (Join-Path $env:ProgramFiles "Hearthstone")
}

$hsRoot = $null
foreach ($candidate in $candidateRoots) {
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        continue
    }

    $coreDll = Join-Path $candidate "BepInEx\core\BepInEx.dll"
    if (Test-Path -LiteralPath $coreDll) {
        $hsRoot = $candidate
        break
    }
}

if ($null -eq $hsRoot) {
    Write-Output "install.bat: Hearthstone/BepInEx not found. Set HSBATTLE_HEARTHSTONE_DIR to your game directory if needed."
    exit 0
}

$pluginDir = Join-Path $hsRoot "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null

try {
    Copy-Item -LiteralPath $SourceDll -Destination (Join-Path $pluginDir $TargetFile) -Force

    $pdbSource = Join-Path $TargetDir ($pluginName + ".pdb")
    if (Test-Path -LiteralPath $pdbSource) {
        Copy-Item -LiteralPath $pdbSource -Destination (Join-Path $pluginDir ($pluginName + ".pdb")) -Force
    }

    Write-Output "install.bat: copied `"$TargetFile`" to `"$pluginDir`""
}
catch {
    Write-Output "install.bat: copy skipped for `"$SourceDll`""
    exit 0
}
