# Windows Release Checklist

Use this checklist for the first public preview and later Windows prereleases.

## Current Preview Target

- Tag: `v0.25.0-preview.1`
- Installer: `CodexBar-Windows-0.25-win-x64.installer.exe`
- Installer checksum: `CodexBar-Windows-0.25-win-x64.installer.exe.sha256`
- Portable package: `CodexBar-Windows-0.25-win-x64.zip`
- Portable checksum: `CodexBar-Windows-0.25-win-x64.zip.sha256`
- Release type: GitHub Release marked as a prerelease

## Before Tagging

1. Confirm `README.md` describes CodexBar for Windows, links to the original macOS project, and says the preview supports Codex, Claude, Cursor, and Gemini.
2. Confirm provider setup docs exist for Codex, Claude, Cursor, and Gemini under `docs/windows-*.md`.
3. Run the Windows test suite:

```powershell
dotnet test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal
```

4. Build the portable package:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\package-windows.ps1 -DotNet dotnet
```

5. Build the installer when Inno Setup 6 is available:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\package-windows-installer.ps1 -DotNet dotnet -SkipPortablePackage
```

6. Smoke launch the portable app from a clean folder and confirm the tray icon, taskbar dock, settings window, and enabled providers open without crashing.

## Tag And Publish

1. Tag the release commit:

```powershell
git tag v0.25.0-preview.1
git push origin v0.25.0-preview.1
```

2. Wait for the `Windows` GitHub Actions workflow to finish.
3. Confirm the GitHub Release exists and is marked prerelease.
4. Confirm the release contains all Windows assets:
   - `CodexBar-Windows-0.25-win-x64.installer.exe`
   - `CodexBar-Windows-0.25-win-x64.installer.exe.sha256`
   - `CodexBar-Windows-0.25-win-x64.zip`
   - `CodexBar-Windows-0.25-win-x64.zip.sha256`

## Release Notes

Include these points in the first preview release notes:

- Windows 11 tray app inspired by Peter Steinberger's original CodexBar.
- Supports Codex and Claude as the primary preview providers.
- Includes preview support for Cursor manual cookies and Gemini CLI OAuth.
- Includes a translucent tray popover and optional taskbar dock.
- Installer is the recommended path; portable zip remains available for no-install testing.
- Settings includes Check for Updates..., which opens the latest GitHub Release.
- Provider credentials stay on the user's Windows profile. Any future telemetry must be opt-in and documented before release.

## After Publishing

1. Download the installer and release zip from GitHub on a Windows 11 machine.
2. Verify the checksums:

```powershell
Get-FileHash -Algorithm SHA256 .\CodexBar-Windows-0.25-win-x64.installer.exe
Get-Content .\CodexBar-Windows-0.25-win-x64.installer.exe.sha256
Get-FileHash -Algorithm SHA256 .\CodexBar-Windows-0.25-win-x64.zip
Get-Content .\CodexBar-Windows-0.25-win-x64.zip.sha256
```

3. Run the installer and confirm CodexBar starts.
4. Unzip the portable package and run `CodexBar.WinApp.exe`.
5. Open a smoke-test issue if any provider fails with normal local credentials.
