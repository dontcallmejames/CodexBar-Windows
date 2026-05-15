param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$DotNet = "dotnet",
    [string]$SignTool = "",
    [string]$SigningCertificatePath = $env:CODEXBAR_SIGNING_CERTIFICATE_PATH,
    [string]$SigningCertificatePassword = $env:CODEXBAR_SIGNING_CERTIFICATE_PASSWORD,
    [string]$TimestampUrl = $(if ([string]::IsNullOrWhiteSpace($env:CODEXBAR_SIGNING_TIMESTAMP_URL)) { "http://timestamp.digicert.com" } else { $env:CODEXBAR_SIGNING_TIMESTAMP_URL }),
    [switch]$SkipSigning
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $repoRoot "version.env"
$version = "dev"
$buildNumber = "0"
$windowsPreviewNumber = ""
if (Test-Path -LiteralPath $versionFile) {
    foreach ($line in Get-Content -LiteralPath $versionFile) {
        if ($line -match "^MARKETING_VERSION=(.+)$") {
            $version = $Matches[1]
        }
        if ($line -match "^BUILD_NUMBER=(.+)$") {
            $buildNumber = $Matches[1]
        }
        if ($line -match "^WINDOWS_PREVIEW_NUMBER=(.+)$") {
            $windowsPreviewNumber = $Matches[1]
        }
    }
}
if ([string]::IsNullOrWhiteSpace($windowsPreviewNumber)) {
    $windowsPreviewNumber = $buildNumber
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
        Write-Host "Signing skipped for '$Path': -SkipSigning was provided."
        return
    }
    if ([string]::IsNullOrWhiteSpace($SigningCertificatePath)) {
        Write-Host "Signing skipped for '$Path': CODEXBAR_SIGNING_CERTIFICATE_PATH is not configured."
        return
    }
    if (-not (Test-Path -LiteralPath $SigningCertificatePath)) {
        throw "Signing certificate not found at '$SigningCertificatePath'."
    }
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "File to sign not found at '$Path'."
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

$distRoot = Join-Path $repoRoot "dist\windows"
$publishDir = Join-Path $distRoot "CodexBar-Windows-$version-$Runtime"
$zipPath = Join-Path $distRoot "CodexBar-Windows-$version-$Runtime.zip"
$checksumPath = "$zipPath.sha256"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

# Resolve dotnet to the actual executable to avoid PowerShell function/alias collisions
$dotnetExe = if (Test-Path -LiteralPath $DotNet) {
    $DotNet
} else {
    $resolved = Get-Command $DotNet -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $resolved) { throw "Could not resolve '$DotNet' to an executable." }
    $resolved.Source
}

# dotnet publish
$publishArgs = @(
    'publish',
    (Join-Path $repoRoot "src\windows\CodexBar.WinUI\CodexBar.WinUI.csproj"),
    '-c', $Configuration,
    '-r', $Runtime,
    '--self-contained', 'true',
    "-p:Version=$version",
    "-p:InformationalVersion=$version",
    '-p:IncludeSourceRevisionInInformationalVersion=false',
    "-p:BuildNumber=$buildNumber",
    "-p:WindowsPreviewNumber=$windowsPreviewNumber",
    '-o', $publishDir,
    '--verbosity', 'minimal'
)
& $dotnetExe @publishArgs

$appExecutablePath = Join-Path $publishDir "CodexBar.WinUI.exe"
Invoke-WindowsCodeSigning $appExecutablePath

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
"$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $zipPath)" | Set-Content -LiteralPath $checksumPath -Encoding ascii

[pscustomobject]@{
    PublishDirectory = $publishDir
    ZipPath = $zipPath
    ChecksumPath = $checksumPath
    SignedExecutablePath = $appExecutablePath
}
