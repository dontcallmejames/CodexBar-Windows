# CodexBar – implementation notes

## Data source
- Read newest `rollout-*.jsonl` from `~/.codex/sessions/**` (respects `CODEX_HOME`).
- Parse lines bottom‑up for the latest `token_count` event: fields used are `rate_limits.primary|secondary.used_percent`, `window_minutes`, `resets_at` (epoch seconds), plus `timestamp`.
- Account info is decoded locally from `~/.codex/auth.json` (`id_token` JWT → `email`, `chatgpt_plan_type`).
- No browser scraping and no `/status` text parsing.

## Refresh model
- `RefreshFrequency` presets: Manual, 1m, 2m (default), 5m; persisted in `UserDefaults`.
- Background Task detaches on app start, wakes per cadence, calls `UsageFetcher.loadLatestUsage()`.
- Manual “Refresh now” menu item always available; stale/errors are surfaced in-menu and dim the icon.
- Optional future: auto‑seed a log if none exists via `codex exec --skip-git-repo-check --json "ping"`; currently not executed to avoid unsolicited usage.

## UI / icon
- `MenuBarExtra` only (LSUIElement=YES). No Dock icon. Label replaced with custom NSImage.
- Icon: 20×18 template image; top bar = 5h window, bottom hairline = weekly window; fill represents “percent remaining.” Dimmed when last refresh failed.
- Menu shows 5h + weekly rows (percent left, used, reset time), last-updated time, account email + plan, refresh cadence picker, Refresh now, Quit.

## App structure (Swift 6, macOS 15+)
- `UsageFetcher`: log discovery + parsing, JWT decode for account.
- `UsageStore`: state, refresh loop, error handling.
- `SettingsStore`: persisted cadence.
- `IconRenderer`: template NSImage for bar.
- Entry: `CodexBarApp`.

## Packaging & signing
- `Scripts/package_app.sh`: swift build (arm64), writes `CodexBar.app` + Info.plist, copies `Icon.icns` if present.
- `Scripts/sign-and-notarize.sh`: uses APP_STORE_CONNECT_* creds and Developer ID identity (`Y5PE65HELJ`) to sign, notarize, staple, zip (`CodexBar-0.1.0.zip`). Adjust identity/versions as needed.

## Limits / edge cases
- If no `token_count` yet in the latest session, menu shows “No usage yet.”
- Schema changes to Codex events could break parsing; errors surface in the menu.
- Only arm64 scripted; add x86_64/universal if desired.

## Alternatives considered
- Fake TTY + `/status`: unnecessary; structured `token_count` already present in logs after any prompt.
- Browser scrape of `https://chatgpt.com/codex/settings/usage`: skipped (cookie handling & brittleness).
