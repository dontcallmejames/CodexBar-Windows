# CodexBar for Windows Public Preview Design

## Purpose

Prepare the Windows port of CodexBar for a public GitHub preview release as a separate Windows-focused project. The preview should feel useful and honest: polished enough for early adopters to install, explicit about what is supported, and clear that it is inspired by and derived from Peter Steinberger's original CodexBar project.

## Product Positioning

The public project will be named **CodexBar for Windows** in user-facing copy. The repository should use a Windows-specific name such as `CodexBar-Windows`, while the running app may continue to display **CodexBar** in tray, popover, settings, and about surfaces.

The README and About window must clearly acknowledge the original project:

- Link to `https://github.com/steipete/CodexBar`.
- State that this Windows version is a Windows 11 tray port inspired by the original macOS menu bar app.
- Preserve the original MIT license and copyright notice.
- Add a Windows port note without implying that the original author maintains this Windows project.

## Preview Scope

The first public preview will support four providers:

- Codex
- Claude
- Cursor
- Gemini

Codex and Claude are already functional enough to remain in the preview. Cursor and Gemini will be added before release, but their initial Windows support should be intentionally narrow and documented.

Out of scope for this preview:

- Full parity with every upstream macOS provider.
- Widget/taskbar-band integration beyond the current tray popover and optional docked overview.
- Automatic browser cookie extraction for Cursor.
- Code signing, unless a certificate is already available.
- Store distribution.

## Repository Shape

The public repo should be separate from upstream. It should not keep `origin` pointed at `steipete/CodexBar` when publishing.

Recommended remote setup:

- `origin`: the new Windows-focused repository owned by the Windows port maintainer.
- `upstream`: `https://github.com/steipete/CodexBar`, kept read-only for reference and future upstream comparison.

The repo should keep upstream history if practical, because it preserves attribution and makes provenance obvious. If a fresh repo is created instead, the README and license notes become even more important.

## Provider Architecture

Provider code should continue following the current Windows shape:

- `UsageProvider` enum identifies supported providers.
- One provider implementation per provider under `src/windows/CodexBar.Core/Providers/<Provider>`.
- Provider implementations return a shared `UsageSnapshot`.
- `AppSettings` controls whether a provider is enabled.
- `AppServices` constructs enabled providers and gives them to `RefreshScheduler`.
- `PopoverViewModel` only exposes provider tabs for snapshots/providers that are available.

Cursor and Gemini should fit this model rather than adding special UI paths.

## Cursor Preview Support

Cursor will start with manual cookie-header support.

Data source:

- User pastes a `Cookie:` header from a signed-in `cursor.com` browser request into Settings.
- The app stores this value in the local settings JSON with the same privacy posture as the current Claude manual cookie header.

Endpoints:

- `GET https://cursor.com/api/usage-summary`
- `GET https://cursor.com/api/auth/me`

Initial mapping:

- Display name: `Cursor`
- Account email: from `/api/auth/me` when available.
- Primary window: included plan usage from `usage-summary`.
- Secondary window: on-demand usage when available.
- Provider cost: on-demand usage USD when available.
- Reset: billing cycle end date when available.

Error handling:

- Missing cookie: show a missing credentials snapshot with a concise setup message.
- Unauthorized/forbidden response: show a stale/error snapshot that asks the user to refresh their Cursor cookie.
- Unknown response shape: preserve app stability and show "No usage data" rather than crashing.

Deferred Cursor work:

- Browser cookie import.
- In-app Cursor login flow.
- Legacy request-based `GET /api/usage?user=ID` fallback.

## Gemini Preview Support

Gemini will use Gemini CLI OAuth credentials.

Data sources:

- `~/.gemini/settings.json` for auth-mode hints.
- `~/.gemini/oauth_creds.json` for OAuth tokens.
- Installed `gemini` CLI files for OAuth client ID and secret extraction when refresh is required.

Endpoints:

- `POST https://cloudcode-pa.googleapis.com/v1internal:loadCodeAssist`
- `POST https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota`
- `POST https://oauth2.googleapis.com/token` when token refresh is required.

Initial mapping:

- Display name: `Gemini`
- Account email: from `id_token` JWT claims when available.
- Plan/tier: derive from `loadCodeAssist` response when available.
- Primary window: lowest remaining quota among Pro-family models.
- Secondary window: lowest remaining quota among Flash-family models.
- Reset: API reset time for the limiting quota bucket.

Error handling:

- Missing OAuth credentials: missing credentials snapshot that tells the user to sign in with Gemini CLI.
- Unsupported auth mode (`api-key`, `vertex-ai`): clear unsupported-source snapshot, not a crash.
- Refresh failure: show a concise auth refresh message and keep the previous snapshot if one exists.
- Unknown quota shape: preserve app stability and show no usage data.

Deferred Gemini work:

- Vertex AI mode.
- API-key quota display.
- CLI `/stats` PTY fallback.

## Settings UX

Settings should remain simple for the preview:

- Provider enable toggles for Codex, Claude, Cursor, and Gemini.
- Account status rows for all four providers.
- Manual cookie text boxes only where needed for preview support:
  - Claude manual cookie header.
  - Cursor manual cookie header.
- Credential path display for local-file providers:
  - Codex auth file.
  - Claude credentials file.
  - Gemini OAuth credentials file.

Do not build a multi-page settings UI yet. The preview should stay modest unless the single settings window becomes unusable.

## Public Documentation

The README should be rewritten or supplemented so Windows users see the Windows app first.

Required README sections:

- What it is.
- Screenshot.
- Supported providers for the preview.
- Install from GitHub Releases.
- Portable zip instructions.
- Installer instructions if an installer exists.
- First-run setup.
- Provider setup for Codex, Claude, Cursor, and Gemini.
- Privacy and credential handling.
- Known limitations.
- Attribution to the original CodexBar.
- Contributing.
- License.

Provider docs should include Windows-specific pages or sections for:

- Codex
- Claude
- Cursor
- Gemini

These docs should be honest about manual cookie requirements and local credential paths.

## Packaging And Releases

The preview release should produce repeatable Windows artifacts:

- Portable self-contained win-x64 zip.
- Installer, preferably after selecting a packaging tool that can create Start Menu shortcuts and uninstall entries.

Recommended installer direction:

- Use a simple Windows installer tool with scriptable CI support.
- Keep the portable zip even after installer support exists.
- Delay code signing until the release process is otherwise stable or a certificate is available.

Release naming:

- Start with a preview tag such as `v0.1.0-preview.1` for the Windows repo.
- Mark the GitHub Release as prerelease.
- Include checksums for downloadable artifacts.

## CI

GitHub Actions should run on Windows and cover:

- Restore.
- Build.
- Test.
- Publish portable win-x64 artifact.
- Package zip.

Release workflow should be separate from pull-request CI. Pull requests should build and test only; tags should produce downloadable artifacts.

## Privacy And Security

Public release requires explicit credential hygiene:

- Do not log access tokens, refresh tokens, cookies, ID tokens, or full credential file contents.
- Tests should use fake tokens and fixture JSON only.
- Error messages should describe auth state without echoing secret values.
- README should state that credentials stay on the local machine and are used only to request usage/quota data from each provider.
- Manual cookie fields should be documented as sensitive.

The app should continue using local settings under the user's Windows profile. Any future telemetry must be opt-in; no telemetry is included in this preview.

## Testing Strategy

Minimum release gate:

- All existing Windows unit tests pass.
- New Cursor mapper/provider tests cover missing credentials, successful usage-summary mapping, unauthorized responses, and unknown response shape.
- New Gemini credential/mapper/provider tests cover missing credentials, unsupported auth mode, token refresh mapping, quota mapping, and unknown response shape.
- Settings tests cover the new provider toggles/status rows.
- Packaging test or script smoke test confirms the portable zip is created.
- Manual smoke test launches the portable app and confirms the tray popover opens.

Network-facing provider tests should use fake `HttpMessageHandler` responses. No test should require live provider accounts.

## Release Criteria

The first public preview is ready when:

- Codex, Claude, Cursor, and Gemini appear correctly in the UI when enabled.
- Missing credentials states are clear and non-scary.
- At least Codex and Claude are verified against real local credentials.
- Cursor manual cookie flow is documented and tested with mocked responses.
- Gemini CLI OAuth flow is documented and tested with mocked responses.
- Portable zip builds reproducibly.
- Installer decision is either implemented or explicitly deferred in the README.
- README and About include original CodexBar attribution.
- CI is green on the public repo.

## Risks

Cursor's web APIs and cookie requirements may change without notice. The preview should label Cursor support as early and manual.

Gemini's private quota APIs and OAuth client extraction can be brittle. The provider should fail gently and give users setup guidance rather than surfacing raw protocol errors.

Unsigned Windows builds may trigger SmartScreen warnings. The README and release notes should set expectations until signing is available.

Keeping upstream source history and references is important for attribution, but the Windows repo should avoid implying upstream ownership or support responsibility.
