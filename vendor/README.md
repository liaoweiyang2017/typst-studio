# Tinymist source-map patch

Pinned upstream: `45b50f0d928e825248fa1c2fcbcd68549e957b5d` (v0.15.10).
Build with `macOS/scripts/build-tinymist.sh` (Rust >= 1.92, network needed only for developer dependencies).
The application bundles the resulting arm64 helper; users do not download compilers at runtime.

The mapping patch modifies `tinymist-query/src/jump.rs`:
- Accept rendered mathematical identifier spans as well as text spans.
- Resolve a selection beginning at the first character of a syntax leaf.
- Use exact spans and glyph offsets; remove the numeric-span-distance fallback. If a math function identifier has no rendered glyph, resolve its enclosing call span (radical/fraction), never a neighboring unrelated span.
- Hit-test formula lines/curves as well as text glyphs.
- Apply group transforms forward and their inverse for hit testing.

Unrendered syntax and missing compiler spans return no target. A macro source span can legitimately have multiple output locations. The UI chooses the page nearest the current viewport.

The upstream preview control protocol uses Unicode scalar columns (as implemented by Typst SourceLines, despite the older protocol comment mentioning UTF-8 bytes), unlike LSP's negotiated UTF-16 positions. `SourceCoordinates.swift` owns that conversion. SwiftUI/PDFKit remain responsible for all visible UI; no web view is embedded.

The preview actor returns explicit no-target responses. On macOS, the dependency watcher adds a 300 ms metadata check with full-resolution timestamps to catch missed per-file FSEvents. Unchanged files are not read. Only files actually read by the compiler are monitored.
