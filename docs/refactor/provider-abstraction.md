# Provider Abstraction follow-ups

## TODO next
- Extract Provider registry to a single source (e.g., `Sources/CodexBar/Providers.swift`) that constructs both `ProviderMetadata` and `ProviderSpec` entries; inject into `UsageStore` to keep specs/labels/capabilities in one place.
- Move dashboard/open actions into metadata (optional dashboard URL and capabilities like `supportsDashboard`, `supportsCredits`, `supportsOpus`) and have `MenuDescriptor`/actions use them instead of hard-coded entries.
- Add `ProviderToggleStore` helper with unit tests to cover defaults, persistence, and the legacy-key purge so `SettingsStore` can focus on UI state only.
- Add tests for provider enablement/metadata defaults and for the new registry wiring.
- Centralize UI strings (headlines, credits hints, toggle labels) in metadata to avoid drift between `MenuDescriptor` and `PreferencesView`.

## Nice-to-haves
- Let provider metadata declare dashboard deep-links for future providers.
- Consider collapsing `UsageProvider` and metadata into an enum with associated data if Swift 6 features allow cleaner construction.
- Remove remaining direct `SettingsStore` references in UI to rely solely on `UsageStore` projection methods.
