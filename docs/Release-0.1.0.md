# Typst Studio 0.1.0

Native macOS Typst editor with a compact three-pane workspace inspired by the supplied Edist reference.

- File sidebar with folder disclosure, blue selection, document search, outline, symbols and issues.
- Source editor with line numbers, current-line tint, red/blue/green syntax colors, native find/undo and bottom formatting controls.
- Continuous PDF preview with live zoom percentage, page position, fit, detached window, source-follow toggle and PDF export.
- Bundled Typst 0.15.1, automatic compilation, unsaved-change prompts and draft recovery.
- Selected 06 periwinkle/lavender app icon.

System requirements: macOS 14+, Apple Silicon. This release is ad-hoc signed, not Developer ID signed or notarized. Gatekeeper may require explicit approval after downloading. Intel is not included in this DMG.

Known limitations: regular-expression highlighting and keyword completion; text-based source/preview matching; no incremental compiler, handwriting recognizer, Vim layer or update service. Refer to Edist-analysis.md for feature coverage.

Build: `scripts/build-app.sh`, then `scripts/package-dmg.sh`.
