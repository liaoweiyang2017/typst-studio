#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION=$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' macOS/Resources/Info.plist)
REPO=liaoweiyang2017/typst-studio
DMG="dist/Typst-Studio-$VERSION-macOS-arm64.dmg"
bash scripts/sign-update.sh
python3 scripts/sign-windows-updates.py
python3 - "$VERSION" <<'PY'
from pathlib import Path
import hashlib,sys
version=sys.argv[1]
files=[Path(f'dist/Typst-Studio-{version}-macOS-arm64.dmg')]+sorted(Path('Windows/artifacts/installers').glob(f'Typst-Studio-{version}-Windows-*-Setup.exe'))
assert len(files)==3 and all(p.is_file() for p in files)
Path(f'dist/SHA256SUMS-{version}.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n' for p in files))
PY
gh release create "v$VERSION" "$DMG" "$DMG.sha256" \
  "Windows/artifacts/installers/Typst-Studio-$VERSION-Windows-arm64-Setup.exe" \
  "Windows/artifacts/installers/Typst-Studio-$VERSION-Windows-x64-Setup.exe" \
  "dist/SHA256SUMS-$VERSION.txt" dist/release-info.json dist/appcast.xml \
  dist/appcast-windows-arm64.xml dist/appcast-windows-arm64.xml.signature \
  dist/appcast-windows-x64.xml dist/appcast-windows-x64.xml.signature \
  --repo "$REPO" --target main --draft --title "Typst Studio $VERSION" --notes-file "docs/Release-$VERSION.md"
gh release edit "v$VERSION" --repo "$REPO" --draft=false --latest
GH_REPO="$REPO" python3 scripts/publish-update-manifest.py "v$VERSION"
echo "All platform packages and signed update feeds published. Verify remote digests before announcing."
