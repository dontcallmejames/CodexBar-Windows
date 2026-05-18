# Claude on Windows

Claude support in the Windows preview uses Claude CLI OAuth credentials when available, with a manual cookie fallback in Settings.

## Credential Source

CodexBar checks `%USERPROFILE%\.claude\.credentials.json` for Claude CLI OAuth credentials. If OAuth credentials are not available, paste a Claude `Cookie:` header into Settings.

## What CodexBar Reads

The app reads OAuth access and refresh token fields from the Claude CLI credentials file, or the manual cookie header saved in CodexBar settings. It only uses the credential source for the Claude provider.

## Endpoint Family

OAuth mode calls Anthropic account and usage API endpoints with `Authorization: Bearer <token>`.

Manual cookie mode calls Claude web API endpoint families on `claude.ai` using the saved cookie header.

## What Is Displayed

The tray popover can show:

- Signed-in account email when available
- Plan or subscription hints when returned by Claude
- Session and weekly usage windows
- Model-specific weekly windows when returned by the provider
- Extra usage or overage information when available

## Local token usage (ccusage)

When Claude Code is installed, CodexBar additionally reads
`%USERPROFILE%\.claude\projects\**\*.jsonl` to compute today's local token spend
in the style of the [ccusage](https://github.com/ryoppippi/ccusage) CLI. The
scan is purely local — no data leaves your machine and no API call is made.
Session files modified more than 8 days ago are skipped without being opened.
Assistant messages are deduplicated by `message.id` so a replayed message is
only counted once.

The token total is shown under the rate-window list on the Claude tab as
`Claude Code: 1.2M tokens today`. To disable in v1, remove the
`~/.claude/projects` directory or revoke read access; there is no in-app toggle.

## Common Setup Errors

- `.credentials.json` is missing: sign in with Claude Code or paste a manual cookie header.
- OAuth credentials do not include usage scope: sign in again with a current Claude CLI.
- Manual cookie is expired: copy a fresh `Cookie:` header from a signed-in `claude.ai` request.
- Account data looks mixed: confirm only the intended Claude credential source is enabled.
