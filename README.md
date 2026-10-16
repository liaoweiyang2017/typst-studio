# Typst Studio

A native macOS Typst editor built with SwiftUI, AppKit and PDFKit. Version 0.1.4.

Open `~/Applications/Typst Studio.app` (the verified local build). Installable DMG and ZIP archives are produced in `dist/`. Build and sign app bundles outside iCloud-synced folders. Includes Typst 0.15.1; no terminal setup is required for this local build.

- Edit and preview side by side, with debounced incremental compilation through persistent Typst processes for up to three recently used source files.
- Open/save `.typ` documents, browse a project folder, navigate headings.
- Export current successful preview as PDF; failed builds retain the last good preview.
- Syntax highlighting, Esc completion, native find and undo, spell checking, symbol palette, font selection, themes and detached preview.
- Draft recovery in `~/Library/Application Support/TypstStudio/Recovery.json`.
- Command-click PDF text for approximate source lookup; optional cursor-follow text matching.

Build: `scripts/build-app.sh` (Xcode command-line tools and local Typst compiler required). Override `TYPST_BIN` for a different compiler path.

Tests: `scripts/test.sh`. Set `STUDIO_TEST_PROJECT=/path/to/large/project` for the optional local large-document regression.

See [analysis and feature gaps](docs/Edist-analysis.md). This is an initial implementation, not full Edist feature parity. No proprietary Edist code or assets are bundled.

Icon selection: **06 — 鸢尾蓝紫**, selected by the user. The selected production icon is `Resources/AppIcon.png`; alternative proposals remain local.

## Release 0.1.4

Run `scripts/package-dmg.sh` after building to generate the Apple Silicon DMG with an Applications shortcut and SHA-256 checksum. See [release notes](docs/Release-0.1.4.md).

Opening a folder discovers its main source automatically. Preview compilation uses temporary sibling files, so the source directory must be writable. The project root controls asset/import access. Search indexes text files up to 1 MB, returns at most 200 hits, and excludes generated build directories. The sidebar still displays those directories lazily.

[Implementation references](docs/Implementation-references.md) document the GitHub research and REA inspection.

The installed application always uses its bundled `Contents/Helpers/typst` binary. It never downloads a compiler or falls back to Homebrew. New third-party package imports may still need the standard Typst package cache/network.

Recent document switches reuse decoded PDF previews and incremental compiler state. Three sessions are retained per project; older sessions are evicted. Outline headings expand by default.

PDF reading colors are configured in Settings: Automatic/Original/Night/Green, with a separate dark-appearance preference. They affect preview only. Cached documents also restore their reading position and zoom.
