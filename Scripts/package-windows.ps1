param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$DotNet = "dotnet"
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

$distRoot = Join-Path $repoRoot "dist\windows"
$publishDir = Join-Path $distRoot "CodexBar-Windows-$version-$Runtime"
$zipPath = Join-Path $distRoot "CodexBar-Windows-$version-$Runtime.zip"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

# dotnet publish
& $DotNet publish `
    (Join-Path $repoRoot "src\windows\CodexBar.WinApp\CodexBar.WinApp.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir `
    --verbosity minimal

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

[pscustomobject]@{
    PublishDirectory = $publishDir
    ZipPath = $zipPath
}
