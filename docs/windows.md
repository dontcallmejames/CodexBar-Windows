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

The app starts in the notification area. On a new install, CodexBar opens a compact setup window where users can enable providers, inspect credential status, and open provider setup help. Click Get Started to save provider choices, or Skip to keep the defaults and configure later.

Click the CodexBar tray icon to open the popover.

## Report Bugs

Use each provider's Test button in Settings to refresh only that provider and show the current credential status. Use the matching Help button to open that provider's Windows setup notes.

Keep Check for updates automatically enabled to let CodexBar check GitHub Releases every 24 hours. You can also use Settings > Check for Updates... to compare the current app version with the latest GitHub Release. Settings shows the current version, latest release, and update status. When an update is available, Settings exposes an Install now button — CodexBar downloads the signed installer, verifies it against the published `.sha256` sidecar, then launches the installer elevated and exits so it can replace the running binaries. The release page link is still available if you'd rather grab the installer or portable zip from GitHub manually.

Use Settings > Report a Bug... to copy a redacted diagnostic summary and open the GitHub bug report form. The summary includes app version, Windows version, update status, enabled providers, provider freshness, and latest visible provider errors, including recent Test results. It does not include tokens, cookies, OAuth files, or credential contents.

## Provider Troubleshooting

Settings uses these provider health states:

- Connected: credentials were found and the latest refresh returned usage data.
- No usage yet: credentials were found, but the provider did not return measurable usage windows yet.
- Needs attention: credentials exist, but the latest refresh or Test action failed. The detail text shows the latest provider-specific error.
- Not connected: the expected credential file or manual credential source is missing.
- Disabled: the provider is turned off in Settings.

For Gemini, sign in with the Gemini CLI OAuth flow and confirm `%USERPROFILE%\.gemini\oauth_creds.json` exists. API key and Vertex AI modes are not supported in this preview.

For Cursor, paste a manual `Cookie:` header from a signed-in Cursor browser request. If Cursor shows No usage yet, the cookie may be accepted while Cursor's private usage endpoint has not returned measurable windows.

For Claude, OAuth subscription credentials are preferred. Manual cookie headers remain available as a fallback.

For Codex, confirm the Codex CLI is signed in and that `%CODEX_HOME%\auth.json` or `%USERPROFILE%\.codex\auth.json` exists.

## Known limitations

- Update installs trigger a UAC prompt and exit the running app so the Inno Setup installer can replace the binaries. Declining UAC leaves CodexBar running and surfaces the error in Settings.
- Cursor support depends on a manual browser cookie header.
- Gemini support is Gemini CLI OAuth only.
- Provider dashboards and private usage APIs can change without notice.

## Package

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\package-windows.ps1 -DotNet dotnet
```

The portable preview zip and checksum are written under `dist\windows`.

When Inno Setup 6 is installed, build the preview installer with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\package-windows-installer.ps1 -DotNet dotnet -SkipPortablePackage
```

The installer and checksum are written under `dist\windows`.

## Release

Use [windows-release-checklist.md](windows-release-checklist.md) before tagging a Windows prerelease.
