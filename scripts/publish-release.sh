#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION=$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' Resources/Info.plist)
REPO=liaoweiyang2017/typst-studio-releases
DMG="dist/Typst-Studio-$VERSION-macOS-arm64.dmg"
gh release create "v$VERSION" "$DMG" "$DMG.sha256" dist/release-info.json --repo "$REPO" --target main --draft --title "Typst Studio $VERSION" --notes-file "docs/Release-$VERSION.md"
gh release edit "v$VERSION" --repo "$REPO" --draft=false --latest
echo "Release published. Wait for the public update-manifest workflow to succeed before announcing it."
