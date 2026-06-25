# Packaging manifests

Source-of-truth manifests for distributing CodexBar through package managers. These live here so they're version-controlled; the actual submission happens in the channel's own repo/bucket.

## winget

Files: `winget/dontcallmejames.CodexBar*.yaml` (winget 1.6 multi-file schema — version, installer, locale).

Per release:
1. Bump `PackageVersion` in all three files and the `InstallerUrl` / `ReleaseDate` in the installer manifest.
2. Fill `InstallerSha256` with the installer's SHA-256. Get it from the release's `.sha256` sidecar:
   ```powershell
   gh release download v0.25.0 -R dontcallmejames/CodexBar-Windows -p "*.installer.exe.sha256" -O - | ForEach-Object { ($_ -split '\s+')[0].ToUpper() }
   ```
   winget wants the hash uppercased.
3. Validate + submit:
   ```powershell
   winget validate --manifest packaging\winget
   wingetcreate submit packaging\winget   # or open a PR to microsoft/winget-pkgs under manifests/d/dontcallmejames/CodexBar/0.25.0/
   ```
   `wingetcreate` (install via `winget install wingetcreate`) automates the fork + PR.

Installs via: `winget install dontcallmejames.CodexBar`

Notes: the installer is a per-user Inno Setup installer (`PrivilegesRequired=lowest`, installs to `%LOCALAPPDATA%\Programs\CodexBar`), so `Scope: user` and `InstallerType: inno`.

## Scoop

File: `scoop/codexbar.json`. Scoop is portable-first, so this points at the portable `.zip` (not the installer) and creates a Start Menu shortcut.

Per release:
1. Bump `version`.
2. Fill `hash` with the zip's SHA-256 from its `.sha256` sidecar:
   ```powershell
   gh release download v0.25.0 -R dontcallmejames/CodexBar-Windows -p "*.zip.sha256" -O - | ForEach-Object { ($_ -split '\s+')[0] }
   ```

Distribution options:
- **Personal bucket** (fastest): create a `scoop-codexbar` repo, drop `codexbar.json` in `bucket/`, then users run `scoop bucket add codexbar https://github.com/dontcallmejames/scoop-codexbar` and `scoop install codexbar`.
- **Submit to `extras`**: open a PR to `ScoopInstaller/Extras` once the app has some traction.

Maintenance note: as of 0.25.1, `MARKETING_VERSION` carries the full semver, so release asset filenames are `CodexBar-Windows-<version>-win-x64.*` (e.g. `0.25.1`). The Scoop `autoupdate` URL uses `$version` for both the tag and the filename, so it tracks new releases automatically once `checkver` finds them.
