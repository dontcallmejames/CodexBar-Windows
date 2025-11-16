# Notarization

- Use Scripts/sign-and-notarize.sh (arm64, deep+timestamp, notarytool --wait).
- Zips must be extracted with Finder or `ditto -x -k` to avoid AppleDouble (._*) files; `unzip` can insert them and break the sealed signature, leading to Gatekeeper errors ("app is damaged").
- If Gatekeeper complains, remove the bad copy and re-extract cleanly: `rm -rf /Applications/CodexBar.app && ditto -x -k CodexBar-<ver>.zip /Applications` then rerun `spctl -a -t exec`.
- Verify after stapling: `spctl -a -t exec -vv CodexBar.app` and `stapler validate CodexBar.app`.

