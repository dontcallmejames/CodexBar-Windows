param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$DotNet = "dotnet",
    [string]$InnoSetupCompiler = "",
    [switch]$SkipPortablePackage
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $repoRoot "version.env"
$version = "dev"
if (Test-Path -LiteralPath $versionFile) {
    foreach ($line in Get-Content -LiteralPath $versionFile) {
        if ($line -match "^MARKETING_VERSION=(.+)$") {
            $version = $Matches[1]
        }
    }
}

function Find-InnoSetupCompiler {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath) {
            return (Resolve-Path -LiteralPath $RequestedPath).Path
        }

        throw "Inno Setup compiler not found at '$RequestedPath'."
    }

    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Inno Setup 6 ISCC.exe was not found. Install Inno Setup or pass -InnoSetupCompiler."
}

if (-not $SkipPortablePackage) {
    & (Join-Path $repoRoot "Scripts\package-windows.ps1") `
        -Configuration $Configuration `
        -Runtime $Runtime `
        -DotNet $DotNet
}

$distRoot = Join-Path $repoRoot "dist\windows"
$publishDir = Join-Path $distRoot "CodexBar-Windows-$version-$Runtime"
$installerPath = Join-Path $distRoot "CodexBar-Windows-$version-$Runtime.installer.exe"
$checksumPath = Join-Path $distRoot "CodexBar-Windows-$version-$Runtime.installer.exe.sha256"
$issPath = Join-Path $repoRoot "installer\windows\CodexBar.iss"
$iscc = Find-InnoSetupCompiler $InnoSetupCompiler

if (-not (Test-Path -LiteralPath $publishDir)) {
    throw "Publish directory '$publishDir' does not exist. Run package-windows.ps1 first or omit -SkipPortablePackage."
}
if (Test-Path -LiteralPath $installerPath) {
    Remove-Item -LiteralPath $installerPath -Force
}
if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

& $iscc `
    "/DAppVersion=$version" `
    "/DAppRuntime=$Runtime" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$distRoot" `
    $issPath

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $installerPath
"$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $installerPath)" | Set-Content -LiteralPath $checksumPath -Encoding ascii

[pscustomobject]@{
    InstallerPath = $installerPath
    ChecksumPath = $checksumPath
}
