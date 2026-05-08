param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$DotNet = "dotnet"
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

# dotnet publish
& $DotNet publish `
    (Join-Path $repoRoot "src\windows\CodexBar.WinApp\CodexBar.WinApp.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Version=$version `
    -p:InformationalVersion=$version `
    -p:IncludeSourceRevisionInInformationalVersion=false `
    -p:BuildNumber=$buildNumber `
    -p:WindowsPreviewNumber=$windowsPreviewNumber `
    -o $publishDir `
    --verbosity minimal

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
"$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $zipPath)" | Set-Content -LiteralPath $checksumPath -Encoding ascii

[pscustomobject]@{
    PublishDirectory = $publishDir
    ZipPath = $zipPath
    ChecksumPath = $checksumPath
}
