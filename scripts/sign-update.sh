#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION=$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' Resources/Info.plist)
STAGE=$(mktemp -d /tmp/typst-studio-appcast.XXXXXX)
trap 'rm -rf "$STAGE"' EXIT
cp "dist/Typst-Studio-$VERSION-macOS-arm64.dmg" "$STAGE/"
# The signing key stays in the local login Keychain, never in GitHub or the repository.
.build/artifacts/sparkle/Sparkle/bin/generate_appcast --account typst-studio-updates \
  --maximum-deltas 0 --download-url-prefix "https://github.com/liaoweiyang2017/typst-studio-releases/releases/download/v$VERSION/" \
  -o "$STAGE/appcast.xml" "$STAGE"
cp "$STAGE/appcast.xml" dist/appcast.xml
