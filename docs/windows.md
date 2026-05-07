# Windows development

The Windows CodexBar port lives under `src/windows`.

## Requirements

- Windows 11
- .NET 9 SDK
- Existing Codex and/or Claude credentials:
  - Codex: `%CODEX_HOME%\auth.json` or `%USERPROFILE%\.codex\auth.json`
  - Claude: `%USERPROFILE%\.claude\.credentials.json` or manual cookie header in settings
  - Cursor: manual cookie header in settings
  - Gemini: `%USERPROFILE%\.gemini\oauth_creds.json` from Gemini CLI OAuth

## Build

```powershell
dotnet build src\windows\CodexBar.Windows.sln
```

## Test

```powershell
dotnet test src\windows\CodexBar.Windows.sln
```

## Run

```powershell
dotnet run --project src\windows\CodexBar.WinApp\CodexBar.WinApp.csproj
```

The app starts in the notification area. Click the CodexBar tray icon to open the popover.

## Report Bugs

Use Settings > Report a Bug... to copy a redacted diagnostic summary and open the GitHub bug report form. The summary includes app version, Windows version, enabled providers, provider freshness, and latest visible provider errors. It does not include tokens, cookies, OAuth files, or credential contents.

## Package

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\package-windows.ps1 -DotNet dotnet
```

The portable preview zip and checksum are written under `dist\windows`.

## Release

Use [windows-release-checklist.md](windows-release-checklist.md) before tagging a Windows prerelease.
