#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
REPO=liaoweiyang2017/typst-studio
MAC_RELEASE=${1:?Pass the existing Mac release tag, for example v0.2.2}
VERSION=$(python3 -c "import xml.etree.ElementTree as E; print(E.parse('Windows/Directory.Build.props').findtext('.//Version'))")
STAGE=$(mktemp -d /tmp/typst-studio-windows-release.XXXXXX)
trap 'rm -rf "$STAGE"' EXIT
mkdir -p dist
python3 scripts/sign-windows-updates.py
# A Windows-only release must keep the Mac latest/download channel intact.
gh release download "$MAC_RELEASE" --repo "$REPO" --dir "$STAGE" \
  --pattern appcast.xml --pattern release-info.json --pattern update.json \
  --pattern 'Typst-Studio-*-macOS-arm64.dmg' --pattern 'Typst-Studio-*-macOS-arm64.dmg.sha256'
python3 - "$STAGE" "$VERSION" <<'PY'
from pathlib import Path
import hashlib,json,sys
stage=Path(sys.argv[1]); version=sys.argv[2]
info=json.loads((stage/'release-info.json').read_text())
manifest=json.loads((stage/'update.json').read_text())
mac=stage/f"Typst-Studio-{info['version']}-macOS-arm64.dmg"
digest=hashlib.sha256(mac.read_bytes()).hexdigest()
assert manifest['version']==info['version'] and manifest['sha256']==digest
assert (stage/(mac.name+'.sha256')).read_text().split()[0]==digest
files=[mac]+[Path(f'Windows/artifacts/installers/Typst-Studio-{version}-Windows-{arch}-Setup.exe') for arch in ['arm64','x64']]
checks=[]
for file in files:
    line=hashlib.sha256(file.read_bytes()).hexdigest()+'  '+file.name+'\n'
    checks.append(line)
    if file.suffix=='.exe': file.with_suffix(file.suffix+'.sha256').write_text(line)
Path(f'dist/SHA256SUMS-{version}.txt').write_text(''.join(checks))
PY
gh release create "v$VERSION" --repo "$REPO" --target main --draft \
  --title "Typst Studio Windows $VERSION" --notes-file "docs/Release-$VERSION.md" \
  "Windows/artifacts/installers/Typst-Studio-$VERSION-Windows-arm64-Setup.exe" \
  "Windows/artifacts/installers/Typst-Studio-$VERSION-Windows-arm64-Setup.exe.sha256" \
  "Windows/artifacts/installers/Typst-Studio-$VERSION-Windows-x64-Setup.exe" \
  "Windows/artifacts/installers/Typst-Studio-$VERSION-Windows-x64-Setup.exe.sha256" \
  "dist/SHA256SUMS-$VERSION.txt" \
  dist/appcast-windows-arm64.xml dist/appcast-windows-arm64.xml.signature \
  dist/appcast-windows-x64.xml dist/appcast-windows-x64.xml.signature \
  "$STAGE"/*
gh release edit "v$VERSION" --repo "$REPO" --draft=false --latest
