# Release process (CodexBar)

This repo is pure SwiftPM; we package/sign/notarize manually (no Xcode project). Sparkle + menu bar specifics included.

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
- Zips to `CodexBar-0.1.0.zip`
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
git tag v0.1.1
./Scripts/make_appcast.sh ...
# upload zip + appcast to Releases
# then create GitHub release (gh release create v0.1.1 ...)
```

## Checklist (quick)
- [ ] Update versions (Package scripts, About text, CHANGELOG)
- [ ] `swiftformat`, `swiftlint`, `swift test`
- [ ] `./Scripts/build_icon.sh` if icon changed
- [ ] `./Scripts/sign-and-notarize.sh`
- [ ] Generate Sparkle appcast with private key
- [ ] Upload zip + appcast to feed, publish release/tag

## Troubleshooting
- **White plate icon**: regenerate icns via `build_icon.sh` (ictool) to ensure transparent padding.
- **Notarization invalid**: verify deep+timestamp signing, especially Sparkle’s Autoupdate/Updater and XPCs; rerun package + sign-and-notarize.
- **App won’t launch**: ensure Sparkle.framework is embedded under `Contents/Frameworks` and rpath added; codesign deep.
- **App “damaged” dialog after unzip**: re-extract with `ditto -x -k`, removing any `._*` files, then re-verify with `spctl`.
