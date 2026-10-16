# Typst Studio 0.1.3

## Changes

- Outline headings now expand recursively by default, with manual collapse still available. Lazy rows reduce offscreen view work.
- The Issues panel background fills the entire sidebar width and height, including the empty “No problems” state.
- Switching between recently opened source files restores the existing decoded PDF immediately. Each retained document keeps its Typst watch process and incremental state.
- The cache retains at most three source-document sessions, evicts the least recently used, and clears when changing project roots. It does not persist private preview data into the application bundle or repository.
- Inactive document import changes continue updating its preview. Disk source edits trigger validation, and stale/failed previews remain unavailable for export.
- Welcome no longer opens the previous document just to compare draft contents; it uses stored recovery metadata, avoiding a startup wait for unavailable files.
- Reusing a compiler result no longer decodes the same PDF again. Process cleanup no longer calls an unbounded Foundation wait on UI shutdown.

## Verification

The previous version failed the regression “switching back discarded the existing PDF.” The new regression covers main → preface → main, imported-file changes while inactive, disk edits, failure recovery and bounded cache eviction. Existing real-editor responsiveness, equal-pane layout and incremental compilation tests also run locally.

For the user's 182-page EMDoc, model-level restoration of the existing PDF was about 0.13 ms. This measures cached state restoration, not the complete PDFKit layout/paint time. It avoids the roughly 1.6-second cold rebuild when returning to a retained document. First opens and evicted documents still compile normally.

## Distribution

macOS 14+, Apple Silicon arm64; Typst 0.15.1 remains bundled and icon 06 is unchanged. Ad-hoc signed, not Apple-notarized. Built and released locally without GitHub Actions.
