#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
swift build -c release --arch arm64
BIN_DIR=$(swift build -c release --arch arm64 --show-bin-path)
APP=${APP_DESTINATION:-"$HOME/Applications/Typst Studio.app"}
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$APP/Contents/Helpers" .build/AppIcon.iconset
install -m 755 "$BIN_DIR/TypstStudio" "$APP/Contents/MacOS/TypstStudio"
TYPST_BIN=${TYPST_BIN:-/opt/homebrew/bin/typst}
install -m 755 "$TYPST_BIN" "$APP/Contents/Helpers/typst"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" Resources/AppIcon.png --out ".build/AppIcon.iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" Resources/AppIcon.png --out ".build/AppIcon.iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns .build/AppIcon.iconset -o "$APP/Contents/Resources/AppIcon.icns"
cp Resources/Info.plist "$APP/Contents/Info.plist"
cp Resources/THIRD_PARTY_NOTICES.txt "$APP/Contents/Resources/"
cp Resources/Typst-LICENSE "$APP/Contents/Resources/"
xattr -cr "$APP"
codesign --force --sign - "$APP/Contents/Helpers/typst"
codesign --force --sign - "$APP"
codesign --verify --deep --strict "$APP"
VERSION=$(/usr/libexec/PlistBuddy -c "Print CFBundleShortVersionString" "$APP/Contents/Info.plist")
[[ "$(lipo -archs "$APP/Contents/Helpers/typst")" == "arm64" ]] || { echo "Typst compiler must be arm64"; exit 1; }
mkdir -p dist
ditto -c -k --sequesterRsrc --keepParent "$APP" "dist/Typst-Studio-$VERSION-macOS-arm64.zip"
echo "$APP"
