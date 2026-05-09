# Legacy macOS Sources

This folder preserves the upstream Swift/macOS CodexBar app for reference while this repository focuses on the Windows port.

Legacy provider docs, fork notes, Sparkle docs, and old GitHub Pages assets are in `docs/` inside this folder.

The Windows app, installer, docs, and release workflow live at the repository root under `src/windows`, `installer/windows`, `docs`, `Scripts`, and `.github/workflows/windows.yml`.

Use this folder only when intentionally reviewing or maintaining the legacy macOS code:

```bash
cd legacy-macos
swift test
```

The Sparkle release path is intentionally guarded. `Scripts/release.sh` requires `CODEXBAR_RUN_LEGACY_MACOS_RELEASE=1` and `SPARKLE_LIB` before it can run. It is not used for Windows releases.
