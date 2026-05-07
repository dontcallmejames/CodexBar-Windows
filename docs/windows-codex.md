# Codex on Windows

Codex support in the Windows preview uses local Codex CLI credentials.

## Credential Source

CodexBar checks `%CODEX_HOME%\auth.json` when `CODEX_HOME` is set, then `%USERPROFILE%\.codex\auth.json`.

Sign in with the Codex CLI before enabling Codex in CodexBar.

## What CodexBar Reads

The app reads the Codex OAuth token data from `auth.json`. It does not read project files for usage totals in this Windows preview path.

## Endpoint Family

CodexBar calls OpenAI/Codex account and usage API endpoints with the local OAuth access token. Token refresh follows the Codex CLI credential data when a refresh token is available.

## What Is Displayed

The tray popover can show:

- Signed-in account email when available
- Plan or account tier when returned by the provider
- Usage windows such as session and weekly limits
- Reset times for returned usage windows

## Common Setup Errors

- `auth.json` is missing: run the Codex CLI sign-in flow.
- The token is expired and cannot refresh: sign in with the Codex CLI again.
- No usage appears: confirm the same Windows user account owns `%USERPROFILE%\.codex\auth.json`.
- Provider data looks stale: use Refresh from the tray popover after signing in again.
