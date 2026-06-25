# Antigravity Provider Design

## Background

Google retired the consumer-tier Gemini CLI on 2026-06-18 and shut down the login servers for individual tiers (Free, AI Pro, Google One, Ultra). CodexBar's `GeminiProvider` depends entirely on that path: it reads `~/.gemini/oauth_creds.json`, refreshes tokens at `oauth2.googleapis.com/token`, and reads quota from `cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota`. All three are now dead for consumer accounts, and users can no longer log back in to refresh the credentials. Paid Gemini Code Assist licenses are unaffected.

Google's replacement is Antigravity (the `agy` CLI and the Antigravity IDE), which hosts a local Codeium-derived language server. That server exposes per-model quota for Claude, Gemini Pro, and Gemini Flash. The upstream macOS app (`steipete/CodexBar`) already reads it via `Sources/CodexBarCore/Providers/Antigravity/AntigravityStatusProbe.swift`. This spec ports that provider to the Windows (.NET) app.

## Goal

Add an Antigravity provider to the Windows app that reads quota from the local Antigravity language server and shows Claude, Gemini Pro, and Gemini Flash lanes plus the plan tier and account email, matching the macOS app's card.

## Behavior

- Discover a running Antigravity language server, read its quota, and map it to a `UsageSnapshot` with one `RateWindow` per model lane.
- Show all lanes the server returns (Claude, Gemini Pro, Gemini Flash), the plan tier, and the account email.
- Enabled by default, like Codex/Claude/Cursor/Gemini.
- When Antigravity is not running, show "Antigravity isn't running" rather than an error or empty card.
- When the account is identified but quota is denied, show "Limits not available" rather than an empty card.
- Leave the existing `GeminiProvider` in place for paid Code Assist users, but have it report the deprecation in a readable way (see Gemini Provider Changes).

## Protocol (ground truth from the macOS probe)

Discovery and read, in order:

1. **Find the process.** Match an executable named `language_server` / `language-server` (optional `_suffix`, optional `.exe`), `antigravity-cli` / `antigravity_cli`, or `agy` (path-anchored), carrying an Antigravity marker (`--app_data_dir antigravity*`, or an `antigravity` path segment). Read the command line to classify it as CLI or IDE.
2. **Find its loopback port(s).** Enumerate the listening TCP ports owned by the matched PID.
3. **Get the CSRF token.** Parse `--csrf_token <token>` from the command line. Also capture `--extension_server_port <port>` and `--extension_server_csrf_token <token>` for the HTTP fallback. The IDE requires a token; the `agy` CLI accepts an empty token.
4. **Call the RPC**, trying methods in order until one returns quota:
   1. `RetrieveUserQuotaSummary` — body `{"forceRefresh": true}`
   2. `GetUserStatus` — body `{"metadata": {"ideName": "antigravity", "extensionName": "antigravity", "ideVersion": "unknown", "locale": "en"}}`
   3. `GetCommandModelConfigs` — same metadata body
   - URL: `{scheme}://127.0.0.1:{port}/exa.language_server_pb.LanguageServerService/{Method}`
   - Headers: `X-Codeium-Csrf-Token: {token}`, `Connect-Protocol-Version: 1`
   - Try both `http` and `https`. Loopback certificate validation is bypassed for 127.0.0.1 / ::1 only.
5. **Map the response.**
   - `RetrieveUserQuotaSummary`: `groups[].displayName`, `groups[].buckets[]` with `bucketId`, `displayName`, `remainingFraction`, `resetTime`, `resetDescription`, `disabled`.
   - Legacy `GetUserStatus`: `userStatus.cascadeModelConfigData.clientModelConfigs[]` with `label`, `modelOrAlias.model`, `quotaInfo.remainingFraction`, `quotaInfo.resetTime`; plan from `userStatus.userTier.preferredName` (fallback `userStatus.planStatus.planInfo.preferredName`); email from `accountEmail`.
   - `UsedPercent = clamp((1 - remainingFraction) * 100, 0, 100)`, matching the existing Gemini mapper.
   - Reset time: ISO-8601 first, numeric epoch seconds as fallback.
   - Assign each bucket/model to a stable lane (Claude, Gemini Pro, Gemini Flash) by model-family matching, not label order.

## Architecture

New folder `src/windows/CodexBar.Core/Providers/Antigravity/`, mirroring the existing per-provider layout:

- `AntigravityProvider : IUsageProvider` — orchestrates locate, then fetch, then map. Returns the not-running / not-available / limits-not-available snapshots. `Provider => UsageProvider.Antigravity`.
- `IAntigravityProcessLocator` — returns the candidate(s): PID, listening loopback ports, CSRF token, extension-server port/token, and a CLI/IDE classification. This is the only OS-specific surface.
- `WindowsAntigravityProcessLocator : IAntigravityProcessLocator` — the Windows implementation (see New Infrastructure).
- `AntigravityLanguageServerClient` — given scheme, port, and token, POSTs the RPC chain over an injected `HttpClient` and returns the first quota-bearing response. No process or socket logic here.
- `AntigravityUsageMapper` — pure function from parsed response to `UsageSnapshot`. Unit-testable with fixture JSON.
- `AntigravityModels.cs` — response DTOs for both the summary and legacy shapes.

The provider, client, and mapper take their dependencies by constructor injection so each is testable without real WMI or sockets. The locator is the seam that fakes the OS in tests.

## New Infrastructure

The Windows codebase has no process-inspection, port-to-PID, or loopback-TLS code today. Three contained additions:

- **Process command line + executable path** via `System.Management` (`Win32_Process`, fields `CommandLine` and `ExecutablePath`). In-process, no shelling out to PowerShell.
- **Listening port to owning PID** via a single P/Invoke to `iphlpapi!GetExtendedTcpTable` with `TCP_TABLE_OWNER_PID_LISTENER`, filtered to the matched PID's loopback rows. Covers both IPv4 and IPv6 loopback.
- **Loopback TLS bypass** via a dedicated `HttpClient` for this provider whose handler sets `ServerCertificateCustomValidationCallback` to accept the certificate only when the host is 127.0.0.1 or ::1. Built in `AppHostBuilder` and passed only to `AntigravityProvider`, so the shared `HttpClient` keeps normal certificate validation.

Both `System.Management` and the P/Invoke are Windows-only, which is acceptable for this app.

## Error Handling

- No matching process, or a matched process with no loopback port: snapshot built via `UsageSnapshot.MissingCredentials(UsageProvider.Antigravity, "Antigravity", "Antigravity isn't running.")`.
- IDE process found but no CSRF token on its command line: skip that candidate. If no candidate succeeds, report "Antigravity isn't available."
- All RPCs reachable but quota denied after the account is identified: report "Limits not available" with the known email/plan, not an empty card.
- Network or parse failures: surface as an error snapshot, consistent with other providers, so a refresh can retry.

## Gemini Provider Changes

Keep `GeminiProvider` registered and enabled. When a refresh fails with 401/403 on the token-refresh or quota call (the signature of the consumer-tier shutdown), return a snapshot whose message reads "Gemini CLI was retired June 18, 2026. Your Gemini usage now appears under Antigravity." Use the existing missing/auth snapshot factory so the card renders as a status, not a crash. No other provider behavior changes.

## Wiring

- Add `Antigravity` to the `UsageProvider` enum.
- Add `AntigravityEnabled` (default `true`) to `AppSettings` and its `Default`.
- Register in `AppHostBuilder.BuildProviders` (`if (settings.AntigravityEnabled) list.Add(new AntigravityProvider(loopbackHttpClient, new WindowsAntigravityProcessLocator()))`) and add the `IsEnabled` case.
- Add `Antigravity` cases to `ProviderLinks` for setup (`docs/windows-antigravity.md`), dashboard (`https://antigravity.google`), and status (Google Cloud status).
- Add a settings toggle in `SettingsWindow.xaml` bound to a new `AntigravityEnabled` view-model property.
- The popover card and tab are driven by `EnabledProviders`, so no extra UI code beyond the toggle.
- Add a `docs/windows-antigravity.md` setup page.

## Tests

- **Mapper**: fixture JSON for both `RetrieveUserQuotaSummary` and legacy `GetUserStatus` shapes; assert lane assignment, percent, reset time, plan, and email.
- **Client**: reuse the existing `QueueHandler` mock; assert RPC URLs, headers, fallback order, and http-then-https behavior.
- **Locator**: a fake `IAntigravityProcessLocator` exercises not-running, IDE-without-token, and CLI-empty-token paths through the provider.
- **Gemini deprecation**: assert the 401/403 path returns the retirement message.
- No live WMI or sockets in tests. The full Windows suite remains the release gate.

## Out of Scope

- Account switching for Antigravity.
- Reading the Windows Credential Manager (discovery is process- and port-based, so no keyring read is needed).
- Any change to other providers beyond the Gemini deprecation message.
- Removing or disabling the Gemini provider.
