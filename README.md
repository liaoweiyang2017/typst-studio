# Typst Studio

A native macOS Typst editor built with SwiftUI, AppKit and PDFKit. Initial working version 0.1.0.

Open `~/Applications/Typst Studio.app` (the verified local build). Installable DMG and ZIP archives are produced in `dist/`. Build and sign app bundles outside iCloud-synced folders. Includes Typst 0.15.1; no terminal setup is required for this local build.

- Edit and preview side by side, with automatically debounced compilation.
- Open/save `.typ` documents, browse a project folder, navigate headings.
- Export current successful preview as PDF; failed builds retain the last good preview.
- Syntax highlighting, Esc completion, native find and undo, spell checking, symbol palette, font selection, themes and detached preview.
- Draft recovery in `~/Library/Application Support/TypstStudio/Recovery.json`.
- Command-click PDF text for approximate source lookup; optional cursor-follow text matching.

Build: `scripts/build-app.sh` (Xcode command-line tools and local Typst compiler required). Override `TYPST_BIN` for a different compiler path.

Tests: `swiftc Sources/TypstStudio/Compiler.swift Tests/CompilerSmoke.swift -o .build/compiler-smoke && .build/compiler-smoke`.

See [analysis and feature gaps](docs/Edist-analysis.md). This is an initial implementation, not full Edist feature parity. No proprietary Edist code or assets are bundled.

Icon selection: **06 — 鸢尾蓝紫**, selected by the user. All ten color proposals are in `Resources/icon-variants/index.html`.

## Release 0.1.0

Run `scripts/package-dmg.sh` after building to generate the Apple Silicon DMG with an Applications shortcut and SHA-256 checksum. See [release notes](docs/Release-0.1.0.md).
