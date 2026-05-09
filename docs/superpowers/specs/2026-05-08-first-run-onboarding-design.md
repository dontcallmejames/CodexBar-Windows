# First-Run Onboarding Design

## Goal

Add a compact setup window for brand-new CodexBar Windows installs so users can choose providers, see credential status, and open setup help without hunting through Settings first.

## Behavior

- Show onboarding only when the settings file does not exist at app startup.
- Do not show onboarding for existing users who already have a settings file.
- Keep tray and taskbar-dock usage available while onboarding is open.
- Let the user enable or disable Codex, Claude, Cursor, and Gemini.
- Show the same provider credential status language used by Settings.
- Offer Help buttons for each provider.
- Save writes settings, applies the selected providers, closes onboarding, and refreshes usage.
- Skip writes the current settings defaults, closes onboarding, and prevents the window from returning on the next launch.

## Architecture

- `FirstRunViewModel` mirrors the provider enablement and account-status subset of `SettingsViewModel`.
- `FirstRunWindow` owns only the setup UI and raises events for save, skip, and provider help.
- `App` decides whether onboarding is needed by checking whether the settings file existed before loading settings.
- `App` positions onboarding near the current app anchor using the existing placement helper.
- Settings remains the long-form configuration surface.

## Error Handling

- If onboarding save or skip cannot write settings, the window shows a settings error and stays open.
- Provider Help uses the existing provider setup URLs.
- If settings load fails, the app keeps the existing fallback to default settings and does not infer onboarding from the failed load.

## Tests

- Unit tests cover first-run detection.
- View-model tests cover provider status and conversion back to `AppSettings`.
- Shell tests cover the onboarding window controls and event wiring.
- Existing full Windows test suite remains the release gate.
