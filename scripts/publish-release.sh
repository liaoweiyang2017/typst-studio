#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION=$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' Resources/Info.plist)
REPO=liaoweiyang2017/typst-studio-releases
DMG="dist/Typst-Studio-$VERSION-macOS-arm64.dmg"
bash scripts/sign-update.sh
gh release create "v$VERSION" "$DMG" "$DMG.sha256" dist/release-info.json dist/appcast.xml --repo "$REPO" --target main --draft --title "Typst Studio $VERSION" --notes-file "docs/Release-$VERSION.md"
gh release edit "v$VERSION" --repo "$REPO" --draft=false --latest
GH_REPO="$REPO" python3 scripts/publish-update-manifest.py "v$VERSION"
echo "Release and locally verified update manifest published."
