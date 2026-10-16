#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
APP=${APP_DESTINATION:-"$HOME/Applications/Typst Studio.app"}
VERSION=$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' "$APP/Contents/Info.plist")
ARCH=$(lipo -archs "$APP/Contents/MacOS/TypstStudio" | tr ' ' '-')
STAGE=$(mktemp -d /tmp/typst-studio-dmg.XXXXXX)
trap 'rm -rf "$STAGE"' EXIT
codesign --verify --deep --strict "$APP"
ditto --norsrc "$APP" "$STAGE/Typst Studio.app"
ln -s /Applications "$STAGE/Applications"
cat > "$STAGE/安装说明.txt" <<TXT
Typst Studio $VERSION

将 Typst Studio.app 拖到 Applications 文件夹。
需要 macOS 14 或更高版本。本构建适用于 Apple Silicon。
包含 Typst 0.15.1 编译器，首次使用不需要安装开发工具。

此版本使用本地 ad-hoc 签名，尚未通过 Apple Developer ID 公证。
下载后 macOS 可能需要在系统设置的「隐私与安全性」中确认打开。

快捷键：⌘O 打开，⌘S 保存，⌘B 编译，⇧⌘E 导出 PDF，⌘F 查找。
TXT
mkdir -p dist
hdiutil create -volname "Typst Studio $VERSION" -srcfolder "$STAGE" -ov -format UDZO "dist/Typst-Studio-$VERSION-macOS-$ARCH.dmg"
hdiutil verify "dist/Typst-Studio-$VERSION-macOS-$ARCH.dmg"
shasum -a 256 "dist/Typst-Studio-$VERSION-macOS-$ARCH.dmg" > "dist/Typst-Studio-$VERSION-macOS-$ARCH.dmg.sha256"
