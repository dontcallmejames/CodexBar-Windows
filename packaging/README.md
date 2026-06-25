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

The live bucket is **[dontcallmejames/scoop-codexbar](https://github.com/dontcallmejames/scoop-codexbar)** (built from `ScoopInstaller/BucketTemplate`). Users install with:

```powershell
scoop bucket add codexbar https://github.com/dontcallmejames/scoop-codexbar
scoop install codexbar
```

The live manifest is `bucket/codexbar.json` in that repo. It points at the portable `.zip` (not the installer), creates a Start Menu shortcut, and writes nothing next to the exe — settings live in `%APPDATA%\CodexBar`, so no `persist` is needed.

**Updates are automatic.** The bucket's **Excavator** GitHub Action runs every 4 hours, and its `autoupdate` reads each release's `.zip.sha256` sidecar, so a new stable release bumps the bucket manifest on its own — no need to hand-edit the Scoop manifest per release. `checkver` uses the GitHub shorthand, which ignores `-preview.N` tags.

`scoop/codexbar.json` in this folder is the original seed/reference; the bucket copy is canonical. To change fields (e.g. add a `post_install`), edit the bucket repo directly.

In-app updater caveat: CodexBar's "Install update" button runs the Inno `.installer.exe` into `%LOCALAPPDATA%\Programs\CodexBar` regardless of install source, which conflicts with a Scoop install. The bucket manifest's `notes` + README steer Scoop users to `scoop update codexbar`; a durable fix (updater self-disables for portable installs) is tracked separately.

Later: submit to **`ScoopInstaller/Extras`** (PR) once the app has traction, so users can install without adding the bucket first.
