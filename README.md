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

The portable zip is still published for testers who prefer a no-install build. Unzip the portable package and run `CodexBar.WinApp.exe`.

Requirements:
- Windows 11
- Signed-in provider tools or credentials for each provider you enable
- .NET is bundled in the installer and portable preview packages when built self-contained

## Supported Providers

- Codex: reads Codex CLI OAuth credentials and calls OpenAI/Codex usage APIs.
- Claude: reads Claude CLI OAuth credentials or a manual Claude cookie header, then calls Anthropic usage APIs.
- Cursor: preview support through a manual `Cookie:` header copied from a signed-in Cursor browser request.
- Gemini: preview support through Gemini CLI OAuth credentials.

## First Run

1. Start `CodexBar.WinApp.exe`.
2. Click the CodexBar tray icon.
3. Open Settings.
4. Enable only the providers you use.
5. Use each provider's Test button to verify credentials and see the latest provider-specific status.
6. Use each provider's Help button to open the matching Windows setup notes.
7. Use Settings > Check for Updates... to compare your build with the latest GitHub Release.
8. Use Settings > Report a Bug... to copy a redacted diagnostic summary and open the GitHub bug form.

## Provider Setup

Windows-specific setup notes live in:

- [Codex on Windows](docs/windows-codex.md)
- [Claude on Windows](docs/windows-claude.md)
- [Cursor on Windows](docs/windows-cursor.md)
- [Gemini on Windows](docs/windows-gemini.md)

## Privacy

CodexBar reads known credential/configuration files for enabled providers and sends usage requests directly to the matching provider endpoint. Provider credentials stay on your machine and are not sent to a CodexBar service.

The preview does not crawl your disk. It checks specific paths such as `%USERPROFILE%\.codex\auth.json`, `%USERPROFILE%\.claude\.credentials.json`, and `%USERPROFILE%\.gemini\oauth_creds.json`, plus manual cookie text you paste into Settings.

## Known Limitations

- Windows support is a public preview.
- Cursor support is manual-cookie only for this preview.
- Gemini support requires Gemini CLI OAuth credentials; API key and Vertex AI modes are not supported in the preview.
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
