# Typst Studio

A native macOS Typst editor built with SwiftUI, AppKit and PDFKit. Version 0.1.8.

Open `~/Applications/Typst Studio.app` (the verified local build). Installable DMG and ZIP archives are produced in `dist/`. Build and sign app bundles outside iCloud-synced folders. Includes Typst 0.15.1; no terminal setup is required for this local build.

- Edit and preview side by side, with debounced incremental compilation through persistent Tinymist/Typst processes for up to three recently used source files.
- Open/save `.typ` documents, browse a project folder, navigate headings.
- Export current successful preview as PDF; failed builds retain the last good preview.
- Syntax highlighting, Esc completion, native find and undo, spell checking, symbol palette, font selection, themes and detached preview.
- Draft recovery in `~/Library/Application Support/TypstStudio/Recovery.json`.
- Compiler-backed bidirectional source maps for text, formulas and included files. Use ⇧⌘J from source, ⌥⌘J from selected PDF text, or Command-click a PDF glyph. Inverse navigation preserves the originating main-document preview.

Build: `scripts/build-app.sh` (Xcode command-line tools, Rust >= 1.92 for the first Tinymist build, and a local Typst compiler required). `scripts/build-tinymist.sh` builds the pinned, patched helper; see [patch notes](vendor/README.md). Override `TYPST_BIN` or `TINYMIST_BIN` only for development.

Tests: `scripts/test.sh`. Set `STUDIO_TEST_PROJECT=/path/to/large/project` for the optional local large-document regression.

See [analysis and feature gaps](docs/Edist-analysis.md). This is an initial implementation, not full Edist feature parity. No proprietary Edist code or assets are bundled.

Icon selection: **06 — 鸢尾蓝紫**, selected by the user. The selected production icon is `Resources/AppIcon.png`; alternative proposals remain local.

## Release 0.1.8

Run `scripts/package-dmg.sh` after building to generate the Apple Silicon DMG with an Applications shortcut and SHA-256 checksum. See [release notes](docs/Release-0.1.8.md).

Opening a folder discovers its main source automatically. Preview compilation uses in-memory LSP documents; unsaved edits are not written into the source directory. The project root controls asset/import access. Search indexes text files up to 1 MB, returns at most 200 hits, and excludes generated build directories. The sidebar still displays those directories lazily.

[Implementation references](docs/Implementation-references.md) document the GitHub research and REA inspection.

The installed application uses its bundled `Contents/Helpers/tinymist` for incremental compilation and mapping, with Typst 0.15.1 included. A standalone `typst` helper is also bundled. It never downloads a compiler or falls back to Homebrew. New third-party package imports may still need the standard Typst package cache/network.

Recent document switches reuse decoded PDF previews and incremental compiler state. Three sessions are retained per project; older sessions are evicted. Outline headings expand by default.

PDF reading colors are configured in Settings: Automatic/Original/Night/Green, with a separate dark-appearance preference. They affect preview only. Cached documents also restore their reading position and zoom.

## Updates and publishing

Public downloads: https://github.com/liaoweiyang2017/typst-studio-releases/releases . Source remains in this private repository. Startup checks are asynchronous and configurable. Settings now offer separate automatic PDF palettes for light and dark appearance.

After local build/tests and DMG packaging, run `scripts/publish-release.sh`. The release script signs the archive using the local Keychain and uploads the Sparkle appcast. The public repository Action verifies the signature and checksum and generates the legacy `update.json`; check its successful run before announcing a release. No private-source access token is used by that workflow.

Windows migration remains analysis only: [WinUI 3 assessment](docs/Windows-WinUI3-assessment.md).

## Windows

Windows 0.1.9 uses a native WinUI 3 interface with a persistent Tinymist backend and PDFium preview. See [Windows documentation](Windows/README.md) for builds, source-map navigation, and known limitations. macOS continues to use the Swift native application. Windows installers use separate `windows-v0.1.9` releases.
