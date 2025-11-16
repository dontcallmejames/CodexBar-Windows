#!/usr/bin/env bash
set -euo pipefail

ZIP=${1:?"Usage: $0 CodexBar-<ver>.zip"}
FEED_URL=${2:-"https://raw.githubusercontent.com/steipete/CodexBar/main/appcast.xml"}
PRIVATE_KEY_FILE=${SPARKLE_PRIVATE_KEY_FILE:-}
if [[ -z "$PRIVATE_KEY_FILE" ]]; then
  echo "Set SPARKLE_PRIVATE_KEY_FILE to your ed25519 private key (Sparkle)." >&2
  exit 1
fi
if [[ ! -f "$ZIP" ]]; then
  echo "Zip not found: $ZIP" >&2
  exit 1
fi

# Sparkle provides generate_appcast; ensure it's on PATH (via SwiftPM build of Sparkle's bin) or Xcode dmg
if ! command -v generate_appcast >/dev/null; then
  echo "generate_appcast not found in PATH. Install Sparkle tools (see Sparkle docs)." >&2
  exit 1
fi

generate_appcast \
  --ed-key-file "$PRIVATE_KEY_FILE" \
  --link "$FEED_URL" \
  "$ZIP"

echo "Appcast generated (appcast.xml). Upload alongside $ZIP at $FEED_URL"
