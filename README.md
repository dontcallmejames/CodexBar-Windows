# CodexBar for Windows

CodexBar for Windows is a Windows 11 tray app that keeps AI coding-provider usage visible without opening every provider dashboard.

This project is inspired by Peter Steinberger's original CodexBar for macOS: https://github.com/steipete/CodexBar. The Windows port is maintained separately.

The first public preview supports Codex, Claude, Cursor, and Gemini. Codex and Claude are the primary preview paths; Cursor and Gemini are included as honest preview support while their provider APIs and credential flows continue to settle.

## Screenshot

The Windows preview follows the translucent tray popover style of the original CodexBar.

<p>
  <img src="docs/screenshots/windows-codex-preview.png" alt="CodexBar for Windows Codex usage popover" width="360">
  <img src="docs/screenshots/windows-claude-preview.png" alt="CodexBar for Windows Claude usage popover" width="360">
</p>

## Install

Download the latest Windows preview release from this repository's Releases page. Most users should download and run the `.installer.exe` asset.

The portable zip is still published for testers who prefer a no-install build. Unzip the portable package and run `CodexBar.WinUI.exe`.

Requirements:
- Windows 11
- Signed-in provider tools or credentials for each provider you enable
- .NET is bundled in the installer and portable preview packages when built self-contained

## Provider Support Matrix

| Provider | Credential source | Usage status | Notes |
| --- | --- | --- | --- |
| Codex | Codex CLI OAuth at `%CODEX_HOME%\auth.json` or `%USERPROFILE%\.codex\auth.json` | Primary preview support | Settings can test credentials and open setup help. |
| Claude | Claude CLI OAuth at `%USERPROFILE%\.claude\.credentials.json` or manual cookie header | Primary preview support | OAuth subscriptions are preferred; manual cookies are a fallback. |
| Cursor | Manual `Cookie:` header copied from a signed-in Cursor browser request | Preview support | Cursor does not expose a stable public usage API for this preview. |
| Gemini | Gemini CLI OAuth at `%USERPROFILE%\.gemini\oauth_creds.json` | Preview support | API key and Vertex AI modes are not supported in this preview. |

## First Run

1. Start `CodexBar.WinUI.exe`.
2. On a new install, use the Welcome to CodexBar setup window to enable the providers you want to track.
3. Use the Help button beside any provider that needs credentials.
4. Click Get Started to save your provider choices, or Skip to keep the defaults and configure later.
5. Click the CodexBar tray icon to open the usage popover.
6. Open Settings later to test credentials, change providers, enable the taskbar dock, or paste manual cookie headers.
7. Keep Check for updates automatically enabled, or use Settings > Check for Updates... to compare your build with the latest GitHub Release. When an update is found, Settings changes the action to Open Release.
8. Use Settings > Report a Bug... to copy a redacted diagnostic summary and open the GitHub bug form.

## Updates

CodexBar checks GitHub Releases automatically every 24 hours when enabled in Settings. You can also check manually at any time. The app shows your current version, the latest release it found, and whether an update is available. It does not auto-install updates; use Open Release to download the installer or portable zip.

## Provider Setup

Windows-specific setup notes live in:

- [Codex on Windows](docs/windows-codex.md)
- [Claude on Windows](docs/windows-claude.md)
- [Cursor on Windows](docs/windows-cursor.md)
- [Gemini on Windows](docs/windows-gemini.md)

## Legacy macOS sources

This repository was forked from the original macOS CodexBar project. Legacy macOS sources and docs such as `Package.swift`, `Sources/`, `Tests/`, `Icon.icns`, `appcast.xml`, and Sparkle release scripts are archived under `legacy-macos/` for upstream reference. Windows releases are built from `src/windows`, `installer/windows`, `Scripts/package-windows.ps1`, `Scripts/package-windows-installer.ps1`, and `.github/workflows/windows.yml`.

Do not use `legacy-macos/Scripts/release.sh` for Windows releases. It is the legacy macOS/Sparkle release path and requires an explicit opt-in environment variable before it will run. Legacy Swift and upstream-sync workflows are manual-only.

## Legacy WPF shell

The original Windows preview used a WPF shell (`CodexBar.WinApp`). That shell is archived under `legacy-wpf/CodexBar.WinApp` and `legacy-wpf/CodexBar.Tray` for reference. It is not built or tested as part of the active solution. The active Windows shell is `src/windows/CodexBar.WinUI`, which uses WinUI 3 via the Windows App SDK.

## Privacy

CodexBar reads known credential/configuration files for enabled providers and sends usage requests directly to the matching provider endpoint. Provider credentials stay on your machine and are not sent to a CodexBar service.

The preview does not crawl your disk. It checks specific paths such as `%USERPROFILE%\.codex\auth.json`, `%USERPROFILE%\.claude\.credentials.json`, and `%USERPROFILE%\.gemini\oauth_creds.json`, plus manual cookie text you paste into Settings.

## Known Limitations

- Windows support is a public preview.
- Updates are manual to install: CodexBar can detect an update automatically and open the release, but it does not download or install updates.
- Cursor support is manual-cookie only for this preview.
- Gemini support requires Gemini CLI OAuth credentials; API key and Vertex AI modes are not supported in the preview.
- A provider can show No usage yet when credentials are present but the provider returns no measurable usage windows.
- Provider dashboards and private usage APIs can change without notice.
- The Windows port is separate from the original macOS app and does not yet include every macOS feature.

## Attribution

CodexBar for Windows is inspired by Peter Steinberger's CodexBar for macOS: https://github.com/steipete/CodexBar.

The original CodexBar project is MIT licensed. This Windows preview preserves that upstream attribution and license lineage.

## Contributing

Use the Windows solution under `src/windows`.

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --verbosity minimal
```

Keep provider data siloed by provider, add focused tests for behavior changes, and document any new credential source before exposing it in the UI.

Windows prereleases use the checklist in [docs/windows-release-checklist.md](docs/windows-release-checklist.md).

## License

MIT. Original project copyright and attribution belong to Peter Steinberger and contributors to https://github.com/steipete/CodexBar.
