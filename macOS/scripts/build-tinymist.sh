#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
REV=45b50f0d928e825248fa1c2fcbcd68549e957b5d
SRC=../tools/tinymist
if [[ ! -d "$SRC/.git" ]]; then
  git clone https://github.com/Myriad-Dreamin/tinymist "$SRC"
  git -C "$SRC" checkout "$REV"
fi
[[ "$(git -C "$SRC" rev-parse HEAD)" == "$REV" ]] || { echo 'Unexpected Tinymist revision'; exit 1; }
if git -C "$SRC" diff --quiet; then
  git -C "$SRC" apply "$PWD/../vendor/tinymist-source-map.patch"
fi
DIFF=$(mktemp)
trap 'rm -f "$DIFF"' EXIT
git -C "$SRC" diff --binary > "$DIFF"
cmp "$DIFF" ../vendor/tinymist-source-map.patch || { echo 'Tinymist checkout contains unexpected changes'; exit 1; }
(cd "$SRC" && cargo build --release --locked -p tinymist-cli --bin tinymist)
