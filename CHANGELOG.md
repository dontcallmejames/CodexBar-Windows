# Changelog

## 0.1.0 — 2025-11-16
- Initial CodexBar release: macOS 15+ menu bar app, no Dock icon.
- Reads latest Codex CLI `token_count` events from session logs (5h + weekly usage, reset times); no extra login or browser scraping.
- Shows account email/plan decoded locally from `auth.json`.
- Horizontal dual-bar icon (top = 5h, bottom = weekly); dims on errors.
- Configurable refresh cadence, manual refresh, and About/GitHub links.
- Async off-main log parsing for responsiveness; strict-concurrency build flags enabled.
- Packaging + signing/notarization scripts (arm64); build scripts convert `.icon` bundle to `.icns`.
