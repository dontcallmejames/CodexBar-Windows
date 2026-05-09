param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$DotNet = "dotnet",
    [string]$InnoSetupCompiler = "",
    [string]$SignTool = "",
    [string]$SigningCertificatePath = $env:CODEXBAR_SIGNING_CERTIFICATE_PATH,
    [string]$SigningCertificatePassword = $env:CODEXBAR_SIGNING_CERTIFICATE_PASSWORD,
    [string]$TimestampUrl = $(if ([string]::IsNullOrWhiteSpace($env:CODEXBAR_SIGNING_TIMESTAMP_URL)) { "http://timestamp.digicert.com" } else { $env:CODEXBAR_SIGNING_TIMESTAMP_URL }),
    [switch]$SkipSigning,
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

function Find-SignTool {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath) {
            return (Resolve-Path -LiteralPath $RequestedPath).Path
        }

        throw "signtool.exe not found at '$RequestedPath'."
    }

    $command = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $kitRoots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    )
    foreach ($kitRoot in $kitRoots) {
        if (-not (Test-Path -LiteralPath $kitRoot)) {
            continue
        }

        $candidates = Get-ChildItem -LiteralPath $kitRoot -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue
        $candidate = $candidates |
            Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
            Sort-Object -Property FullName -Descending |
            Select-Object -First 1
        if (-not $candidate) {
            $candidate = $candidates |
                Sort-Object -Property FullName -Descending |
                Select-Object -First 1
        }
        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "signtool.exe was not found. Install the Windows SDK or pass -SignTool."
}

function Invoke-WindowsCodeSigning {
    param([string]$Path)

    if ($SkipSigning) {
        Write-Host "Signing skipped: -SkipSigning was provided."
        return
    }
    if ([string]::IsNullOrWhiteSpace($SigningCertificatePath)) {
        Write-Host "Signing skipped: CODEXBAR_SIGNING_CERTIFICATE_PATH is not configured."
        return
    }
    if (-not (Test-Path -LiteralPath $SigningCertificatePath)) {
        throw "Signing certificate not found at '$SigningCertificatePath'."
    }

    $resolvedSignTool = Find-SignTool $SignTool
    $resolvedCertificate = (Resolve-Path -LiteralPath $SigningCertificatePath).Path
    $arguments = @(
        "sign",
        "/fd", "SHA256",
        "/td", "SHA256",
        "/tr", $TimestampUrl,
        "/f", $resolvedCertificate
    )
    if (-not [string]::IsNullOrWhiteSpace($SigningCertificatePassword)) {
        $arguments += @("/p", $SigningCertificatePassword)
    }
    $arguments += $Path

    & $resolvedSignTool @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe failed with exit code $LASTEXITCODE."
    }
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

Invoke-WindowsCodeSigning $installerPath

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $installerPath
"$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $installerPath)" | Set-Content -LiteralPath $checksumPath -Encoding ascii

[pscustomobject]@{
    InstallerPath = $installerPath
    ChecksumPath = $checksumPath
}
