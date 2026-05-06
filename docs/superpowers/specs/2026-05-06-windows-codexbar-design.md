# Windows CodexBar Design

## Summary

Build a Windows 11 native port of CodexBar inside this repository while preserving the original app's purpose, behavior, and visual feel. The first milestone supports Codex and Claude only. Additional providers are added later through the same provider contract.

The Windows port uses a native .NET desktop architecture rather than reusing the existing Tauri/Rust Windows port linked from upstream. The initial user-facing shell is a configurable hybrid:

- A notification-area tray app is the primary native anchor.
- A CodexBar-styled popover opens from the tray icon.
- An optional docked overview window can sit near the taskbar for persistent visibility.
- A real Windows Widgets Board provider is a later packaging milestone.

## Goals

- Preserve CodexBar's small background-app feel: no main window by default, fast glanceable usage, and settings only when needed.
- Match the original menu card look and information hierarchy for Codex and Claude.
- Support Codex and Claude usage windows, reset countdowns, identity, credits where available, error states, and local cost usage.
- Create a provider/data contract that makes future provider ports mechanical.
- Keep all provider parsing and platform path logic covered by tests.

## Non-Goals

- Do not port every provider in the first milestone.
- Do not embed an arbitrary app surface directly into the Windows taskbar, because Windows 11 does not expose that as a supported third-party app model.
- Do not build Sparkle-style auto-update in the MVP.
- Do not require WSL for the native Windows app.
- Do not use the existing Tauri/Rust Win-CodexBar project as the implementation base.

## Platform Basis

Windows notification-area icons are the supported equivalent of a macOS menu bar status item. CodexBar for Windows should use the notification area APIs for the tray icon and mouse interactions.

Windows Widgets are hosted in the Windows Widgets Board, not embedded directly in the taskbar. A Widgets Board provider requires packaged app registration, so it belongs after the tray MVP.

The Windows App SDK provides modern window management APIs for WinUI 3 and desktop apps. The preferred shell is .NET with WinUI 3 if the local toolchain supports it cleanly. WPF is the fallback if WinUI packaging/runtime setup blocks fast progress.

Reference docs:

- Windows notification area: https://learn.microsoft.com/en-us/windows/win32/shell/notification-area
- Windows App SDK windowing: https://learn.microsoft.com/windows/apps/windows-app-sdk/windowing/windowing-overview
- Windows Widgets: https://learn.microsoft.com/en-us/windows/apps/develop/widgets/

## Architecture

Add a Windows-native app alongside the existing macOS Swift source rather than mutating the Swift app in place.

Proposed solution layout:

- `src/windows/CodexBar.WinApp`: desktop entry point, tray lifecycle, popover window, settings window, optional docked overview window.
- `src/windows/CodexBar.Core`: shared Windows provider model, refresh scheduling, Codex and Claude providers, parsing, credential discovery, cost scanning, and snapshot types.
- `src/windows/CodexBar.Tray`: notification-area icon abstraction, click handling, icon rendering, and native interop.
- `src/windows/CodexBar.Tests`: unit and view-model tests for provider parsing, path discovery, refresh behavior, and menu state.
- `src/windows/CodexBar.Widgets`: later Widgets Board provider after MSIX packaging is introduced.

Primary data flow:

```text
RefreshScheduler
  -> CodexProvider / ClaudeProvider
  -> UsageSnapshot
  -> TrayIconRenderer
  -> PopoverViewModel
  -> DockedOverviewViewModel
```

The Windows `UsageSnapshot` should stay close to the Swift model's semantics: provider id, rate windows, credits, account identity, source label, stale/error state, status incident state, and token cost summaries. This keeps UI parity and future provider ports straightforward.

## UI Design

The tray icon uses a merged Codex/Claude meter by default. Settings can later expose one icon per provider if that proves valuable, matching the macOS "Merge Icons" concept.

Clicking the tray icon opens a CodexBar-styled popover with provider tabs. The popover should preserve:

- Provider tab row with icon, name, active state, and mini meter.
- Provider title, "updated" label, and plan/source hint.
- Session and weekly usage rows with progress bars and reset countdowns.
- Model-specific rows for Claude where available.
- Extra usage and cost sections when data exists.
- Actions such as Add Account, Usage Dashboard, Status Page, Settings, About, and Quit.

The optional docked overview window shows compact Codex and Claude rows with provider name, percent remaining or used according to display settings, reset hints, and stale/error styling. It is controlled by a "Dock overview near taskbar" setting.

Settings sections:

- General: launch at login, refresh cadence, startup behavior.
- Display: merged tray icon, show percent used vs remaining, reset time style, docked overview toggle.
- Providers: Codex and Claude enablement, source selection, manual cookies/tokens, status actions.
- Advanced: storage/cost scanning, logs, privacy toggles, credential cache reset.
- About: version, source repo, licenses.

## Codex MVP

Supported data sources:

1. OAuth usage API from `~\.codex\auth.json` or `%CODEX_HOME%\auth.json`.
2. CLI RPC fallback through `codex app-server`.
3. Local cost usage scan from known Codex JSONL locations.

Displayed data:

- 5-hour or primary session window.
- Weekly or secondary window.
- Credits when available.
- Account email and plan when available.
- Reset countdowns.
- Local today and 30-day cost/token summaries.
- Source label and stale/error state.

OpenAI web dashboard extras are not required for the first milestone. They can be added after the native WebView/cookie import story is settled.

## Claude MVP

Supported data sources:

1. OAuth from `~\.claude\.credentials.json`.
2. Manual Claude cookie header in settings.
3. Windows Credential Manager or DPAPI-protected local cache for saved tokens/cookie headers.
4. Local cost usage scan from known Claude JSONL locations.

Claude CLI PTY fallback is part of the first milestone only if Windows ConPTY probing is reliable during implementation. If it is fragile, it becomes phase 1.1 and the MVP still ships with OAuth plus manual cookies.

Displayed data:

- 5-hour or current session window.
- Weekly window.
- Sonnet/Opus model-specific weekly rows when available.
- Extra usage spend/limit when available.
- Account and org identity when available.
- Reset countdowns.
- Local today and 30-day cost/token summaries.
- Source label and stale/error state.

## Windows Platform Replacements

- AppKit `NSStatusItem` becomes Windows notification-area icon plus custom popover window.
- SwiftUI menu cards become native XAML or WPF views styled to match CodexBar.
- `UserDefaults` becomes `%APPDATA%\CodexBar\config.json`.
- Keychain becomes Windows Credential Manager or DPAPI-protected local secrets.
- `~/Library/Caches/CodexBar` becomes `%LOCALAPPDATA%\CodexBar\Cache`.
- WidgetKit becomes an optional docked overview window first and a Windows Widgets Board provider later.
- Sparkle is deferred. Later distribution can use MSIX, winget, or an installer updater.

## Error Handling

- Missing credentials: show a setup row with source-specific guidance.
- Missing Codex CLI: show install guidance only when CLI is selected or fallback reaches CLI.
- Network/API failure: retain the last good snapshot, dim the tray icon, and show the latest error in the popover.
- Token refresh failure: back off repeated refresh attempts and surface a repair action.
- Manual cookie validation failure: reject invalid values on save and keep the previous valid value.
- Cost scan permission/path errors: keep usage refresh working and show the cost section as unavailable.
- Docked overview errors: mirror provider stale/error state without opening modal alerts.

## Testing

The first implementation should include focused tests before broad UI polish:

- Codex OAuth response decoding.
- Codex token refresh and credential path discovery.
- Codex CLI RPC response mapping with mocked process I/O.
- Claude OAuth response decoding.
- Claude web/manual-cookie response mapping.
- Windows config/cache/log path resolution.
- Local Codex and Claude cost usage scanning.
- Refresh scheduler behavior for success, stale data, and backoff.
- Popover view models for normal, missing-credential, stale, and failed states.
- Tray display model for merged Codex/Claude percent and dimmed error state.

## Provider Expansion Path

After Codex and Claude are stable:

1. Port providers with simple API-token or local-file sources.
2. Add browser cookie import for Chromium/Edge/Brave and Firefox using Windows DPAPI where needed.
3. Add provider status polling parity.
4. Add real Windows Widgets Board provider once MSIX packaging exists.
5. Revisit one-icon-per-provider mode if users want closer macOS status-item parity.

Each new provider should implement the shared provider contract, add parser/path tests, and feed the same snapshot model used by Codex and Claude.

## Open Decisions For Implementation Planning

- Use WinUI 3 if the local environment can build and run a minimal tray/popover app quickly; otherwise use WPF for the MVP and keep the provider/core model UI-framework independent.
- Decide the exact tray interop package after confirming current .NET compatibility.
- Decide Credential Manager versus DPAPI file cache after evaluating developer ergonomics and testability.
- Decide whether Claude CLI ConPTY belongs in milestone 1.0 or 1.1 based on a small reliability spike.
