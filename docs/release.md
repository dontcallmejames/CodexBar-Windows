# Release process (CodexBar)

SwiftPM-only; package/sign/notarize manually (no Xcode project). Sparkle feed is served from GitHub Releases. Checklist below merges Trimmy’s release flow with CodexBar specifics.

## Prereqs
- Xcode 26+ installed at `/Applications/Xcode.app` (for ictool/iconutil and SDKs).
- Developer ID Application cert installed: `Developer ID Application: Peter Steinberger (Y5PE65HELJ)`.
- ASC API creds in env: `APP_STORE_CONNECT_API_KEY_P8`, `APP_STORE_CONNECT_KEY_ID`, `APP_STORE_CONNECT_ISSUER_ID`.
- Sparkle keys: public key already in Info.plist; private key path set via `SPARKLE_PRIVATE_KEY_FILE` when generating appcast.

## Icon (glass .icon → .icns)
```
./Scripts/build_icon.sh Icon.icon CodexBar
```
Uses Xcode’s `ictool` + transparent padding + iconset → Icon.icns.

## Build, sign, notarize (arm64)
```
./Scripts/sign-and-notarize.sh
```
What it does:
- `swift build -c release --arch arm64`
- Packages `CodexBar.app` with Info.plist and Icon.icns
- Embeds Sparkle.framework, Updater, Autoupdate, XPCs
- Codesigns **everything** with runtime + timestamp (deep) and adds rpath
- Zips to `CodexBar-<version>.zip`
- Submits to notarytool, waits, staples, validates

Gotchas fixed:
- Sparkle needs signing for framework, Autoupdate, Updater, XPCs (Downloader/Installer) or notarization fails.
- Use `--timestamp` and `--deep` when signing the app to avoid invalid signature errors.
- Avoid `unzip` — it can add AppleDouble `._*` files that break the sealed signature and trigger “app is damaged”. Use Finder or `ditto -x -k CodexBar-<ver>.zip /Applications`. If Gatekeeper complains, delete the app bundle, re-extract with `ditto`, then `spctl -a -t exec` to verify.

## Appcast (Sparkle)
After notarization:
```
SPARKLE_PRIVATE_KEY_FILE=/path/to/ed25519-priv.key \
./Scripts/make_appcast.sh CodexBar-0.1.0.zip \
  https://raw.githubusercontent.com/steipete/CodexBar/main/appcast.xml
```
Uploads not handled automatically—commit/publish appcast + zip to the feed location (GitHub Releases/raw URL).

## Tag & release
```
git tag v<version>
./Scripts/make_appcast.sh ...
# upload zip + appcast to Releases
# then create GitHub release (gh release create v<version> ...)
```

## Checklist (quick)
- [ ] Update versions (scripts/Info.plist, CHANGELOG, About text)
- [ ] `swiftformat`, `swiftlint`, `swift test` (zero warnings/errors)
- [ ] `./Scripts/build_icon.sh` if icon changed
- [ ] `./Scripts/sign-and-notarize.sh`
- [ ] Generate Sparkle appcast with private key
  - Sparkle ed25519 private key path: `/Users/steipete/Library/CloudStorage/Dropbox/Backup/Sparkle-VibeTunnel/sparkle-private-key-KEEP-SECURE.txt`
- [ ] Upload zip + appcast to feed; publish tag + GitHub release so Sparkle URL is live (avoid 404)
- [ ] Version continuity: confirm the new version is the immediate next patch/minor (no gaps) and CHANGELOG has no skipped numbers (e.g., after 0.2.0 use 0.2.1, not 0.2.2)
- [ ] Download uploaded `CodexBar-<ver>.zip`, unzip via `ditto`, run, and verify signature (`spctl -a -t exec -vv CodexBar.app` + `stapler validate`)
- [ ] Confirm `appcast.xml` points to the new zip/version and renders correct release notes
- [ ] When creating the GitHub release, paste the CHANGELOG entry as Markdown list (one `-` per line, blank line between sections); visually confirm bullets render correctly after publishing
- [ ] Keep a previous signed build in `/Applications/CodexBar.app` to test Sparkle delta/full update to the new release
- [ ] For Sparkle verification: if replacing `/Applications/CodexBar.app`, quit first, replace, relaunch, and test update

## Troubleshooting
- **White plate icon**: regenerate icns via `build_icon.sh` (ictool) to ensure transparent padding.
- **Notarization invalid**: verify deep+timestamp signing, especially Sparkle’s Autoupdate/Updater and XPCs; rerun package + sign-and-notarize.
- **App won’t launch**: ensure Sparkle.framework is embedded under `Contents/Frameworks` and rpath added; codesign deep.
- **App “damaged” dialog after unzip**: re-extract with `ditto -x -k`, removing any `._*` files, then re-verify with `spctl`.
- **Update download fails (404)**: ensure the release asset referenced in appcast exists and is published in the corresponding GitHub release; verify with `curl -I <enclosure-url>`.
