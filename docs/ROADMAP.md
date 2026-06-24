# Roadmap

Tracked features and ideas. Roughly in priority order; subject to change.

## Shipped in this batch

1. **Global hotkey** — Configurable system-wide shortcut to toggle the popover from anywhere. Right now you have to find the tray icon, which Windows often hides into the overflow chevron.
2. **GitHub Copilot provider** — Reads `GET https://api.github.com/copilot_internal/user` using the GitHub CLI's stored token. Surfaces premium-interactions / chat / completions quotas depending on the plan tier.
3. **ccusage integration for Claude** — Scans `~/.claude/projects/**/*.jsonl` (Claude Code's local session log) and surfaces today's local token spend on the Claude tab. Local-only, no API roundtrip and no NPX dependency.
4. **In-app update installer** — Settings now has an Install now button. CodexBar downloads the new `.installer.exe` to `%TEMP%`, verifies it against the published `.sha256` sidecar, launches the Inno Setup installer elevated with `/CLOSEAPPLICATIONS /SILENT /SUPPRESSMSGBOXES`, and exits the running instance so the installer can replace its binaries.

## Next up (5–9)

5. **Microsoft Store distribution (MSIX)**. Add a Windows Application Packaging Project to the solution producing `.msixbundle` alongside the existing installer + portable zip. Requires `broadFileSystemAccess` capability approval from the Store reviewers (justification: app reads known CLI credential file paths under `%USERPROFILE%`). Costs $19 one-time for the individual Partner Center account. Gains: silent auto-update, no SmartScreen, Store search reach.

6. **Plugin-based provider system**. Today each provider is hard-coded as a C# class in `CodexBar.Core/Providers`. Refactor to load providers as drop-in DLLs (or declarative JSON specs + a small expression DSL) so new providers don't require rebuilding the app. Bigger architectural lift; do after the provider list is reasonably stable.

7. **Local HTTP API on `127.0.0.1:6736`**. Optional HTTP server exposing GET `/usage` that returns the same JSON snapshot the popover renders. Lets other apps (Stream Deck, custom dashboards, Slack bots) consume CodexBar data without parsing the UI. Bind localhost-only, off by default, opt-in via Settings.

8. **Proxy support (HTTP / SOCKS5)**. Honor system proxy by default; allow per-provider proxy override in Settings. Needed for corporate users behind egress proxies.

9. **More providers**. Long tail to consider, each is a small port: Copilot is in this batch, leaving:
   - Amp (free tier, bonus, credits)
   - Antigravity (all models)
   - Factory / Droid (standard, premium tokens)
   - JetBrains AI Assistant (quota, remaining)
   - Kiro (credits, bonus credits, overages)
   - Kimi Code (session, weekly)
   - MiniMax (coding plan session)
   - OpenCode Go (5h, weekly, monthly spend limits)
   - Windsurf (prompt credits, flex credits)
   - Z.ai (session, weekly, web searches)

   Prioritize by audience size — Copilot first (in this batch), Windsurf/JetBrains second tier.

## Considered, not committed

- **Sub-metric depth per provider**. OpenUsage breaks Claude into session / weekly / extra-usage / ccusage and Codex into session / weekly / reviews / credits. We currently show one or two meters per provider. Worth doing once the providers themselves are stable.
- **Per-provider color theming** in the popover and dock tiles. Was started in a branch and reverted; revisit after the Codex/Claude/Cursor/Gemini logos are produced.
- **Native Windows widgets** (Microsoft.Windows.Widgets) — surfaces usage on the Widgets board without opening the popover.

## Adding a provider — auth-expiry contract

Every provider MUST distinguish an expired/revoked credential from a transient
network error so the user gets an actionable "sign in again" message instead of
a blank tab or silently-stale numbers.

When a credential is rejected by the server (HTTP 401/403, or a refresh that
cannot recover), signal re-auth in ONE of these two ways:

- Throw-style providers (you call response.EnsureSuccessStatusCode and let
  exceptions reach RefreshScheduler — e.g. Codex, Claude, Gemini): detect
  401/403 before EnsureSuccessStatusCode and
  `throw new AuthenticationRequiredException("<actionable re-auth message>");`

- Return-style providers (you inspect StatusCode and return a snapshot — e.g.
  Cursor, Copilot): return
  `UsageSnapshot.RequiresAuthentication(Provider, "<DisplayName>", "<actionable re-auth message>");`
  (NOT MissingCredentials — that is only for "never configured".)

Rules:
- The message must tell the user exactly how to re-authenticate (the CLI command
  or the Settings field), not the raw HTTP/.NET exception text.
- Do NOT use the re-auth signal for timeouts, DNS failures, or 5xx — let those
  propagate so the last-good snapshot is kept as stale.
- Map HTTP 429 (and 503 with Retry-After) to RateLimitException, never to auth.
- Add a per-provider entry to ProviderLinks.SetupUri so the popover "Reconnect"
  button and the toast deep-link to the right docs page.
- Add a unit test asserting a 401/403 produces AuthState.RequiresAuthentication.

## Not planned

- Cross-platform port (Linux/macOS). macOS is covered by [steipete/CodexBar](https://github.com/steipete/CodexBar) and [robinebers/openusage](https://github.com/robinebers/openusage); Linux audience for AI coding usage trackers is small enough to not justify the WinUI 3 → Avalonia/MAUI rewrite.
- Multi-account per provider. Today the app reads one credential file per provider. Multi-account would require account-switching UI and storage.
- Telemetry. The app never phones home. This isn't going to change.
