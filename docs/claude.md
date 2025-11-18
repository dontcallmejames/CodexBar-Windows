# Claude support plan (CodexBar)

Goal: add optional Claude Code usage alongside Codex, with a Claude-themed menu bar icon and independent on/off toggles.

## Proposed UX
- On launch, detect availability:
  - Codex CLI: `codex --version`.
  - Claude Code CLI: `claude --version`.
- Settings: two checkboxes, “Show Codex” and “Show Claude”; display detected version number next to each (e.g., “Claude (2.0.44)” or “Not installed”).
- Menu bar:
- When both are enabled, render a Claude-specific template icon; inside the menu content show two usage rows (Codex 5h/weekly, Claude session/week). Keep current icon style for Codex-only, Claude icon for Claude-only.
- If neither source is enabled, show empty/dim bars with a hint to enable a source.
- Refresh: reuse existing cadence; Claude probe runs only if Claude is enabled and present.

### Claude menu-bar icon (crab notch homage)
- Base two-bar metaphor remains.
- Top bar: add two 1 px “eye” cutouts spaced by 2 px; add 1 px outward bumps (“claws”) on each end; same height/weight as current.
- Bottom bar: unchanged hairline.
- Size: 20×18 template, 1 px padding; monochrome-friendly; substitute this template whenever Claude is enabled (or use Codex icon for Codex-only).

## Data path (Claude)
- Reuse the tmux-driven `/usage` probe from Agent Sessions (`tools/claude_usage_capture.sh`):
  - Launch `claude` in a detached tmux, send `/usage`, tab to Usage, capture screen, parse into JSON with fields:
    - `session_5h.pct_used`, `session_5h.resets`
    - `week_all_models.pct_used`, `week_all_models.resets`
    - `week_opus` optional
  - Parse JSON in Swift to `RateWindow` equivalents (percent + reset text; limits unknown).
- Errors to surface: CLI missing, tmux missing, auth required, parsing failed.
- No transcript parsing needed; no tokens leave the device.

## Implementation steps
1) Settings model: add provider flags + detected versions; persist in UserDefaults.
2) Detection: on startup, run `codex --version` / `claude --version` once (background) and cache strings.
3) Provider abstraction: allow Codex, Claude, Both; gate refresh loop per selection.
4) Bundle script: add `Resources/claude_usage_capture.sh`, mark executable at runtime before launching.
5) ClaudeUsageFetcher: small async wrapper that runs the script, decodes JSON, maps to UI model.
6) IconRenderer: accept a style enum; use new Claude template image when Claude is enabled (or both).
7) Menu content: conditionally show Codex row, Claude row, or an empty-state message when none enabled.
8) Tests: fixture JSON parsing; guard the runtime script test behind an env flag.

## Open items / decisions
- Which template asset to use for the Claude icon (color vs monochrome template); default to a monochrome template PDF sized 20×18.
- Whether to auto-enable Claude when detected the first time; proposal: keep default off, show “Detected Claude 2.0.44 (enable in Settings)”.
- Weekly vs session reset text: display the string parsed from the CLI; do not attempt to compute it locally.
