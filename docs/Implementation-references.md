# 0.1.1 implementation references

Reviewed on 2026-10-16 before implementation.

- https://github.com/SuperMegaFort/TypstEdit at `b90e3ffa8f45f55b14dd5144ae0f7460a931fd28`: `Sources/TypstCompiler.swift` uses a sibling shadow source and a Typst process to preserve relative imports. We adopted the sibling-source approach, independently implemented unique names, explicit project root, process cancellation/timeout and cleanup. Its source was not copied or vendored (no root license file was found).
- https://github.com/duhman/pdfview at `a5ecacf1eb6ca1144af6e990385b1e5206ea6514`: `Sources/Views/PDFKitView.swift` separates automatic and manual scaling, uses PDFKit notifications and avoids needless document replacement. We retained PDFKit and implemented width fitting directly rather than introducing a dependency. No code was copied.
- https://typst.app/docs/reference/symbols/sym/: checked symbol names against the bundled compiler; tests compile every palette expression.
- https://github.com/morluto/rea, installed npm 6.5.0: re-ran `inspect-artifact`, `inspect-macho` and `inspect-plist` on the locally installed Edist app. Local evidence is in ignored `analysis/edist-*-0.1.1*.json`. This is bundle/native metadata evidence, not recovery of Edist's Swift source or runtime algorithm. User-supplied screenshots are the primary UI behavior/style reference.

The implementation remains SwiftUI/AppKit/PDFKit with no new runtime framework. File tree traversal is lazy; project indexing/search and compilation run off the UI thread. Compilation is serial and revision-guarded. Build and release scripts run locally; no CI workflow was added.

## 0.1.2 research and validation

- https://github.com/typst/typst/blob/v0.15.1/crates/typst-cli/src/watch.rs: the official watch loop retains incremental cache and registers dependencies after compile_once completes. This informed the persistent process and bounded missing-event retry. CLI logs were experimentally checked for success, warning and error statuses.
- https://github.com/typst/typst/blob/main/docs/dev/architecture.md: Typst's incremental compiler reuses previous computation within a compiler process; launching a new CLI process for each edit loses this state.
- The previously inspected TypstEdit uses a persistent watch process and a native two-pane splitter. No source was copied (license availability remains unclear); our splitter is a small independently implemented NSSplitView wrapper.
- https://github.com/xoengineering/FileTree was considered for navigation; adding another dependency would not fix the observed selection-notification loop, so the existing lightweight tree was retained with background directory reads.

The original freeze was reproduced in the actual 0.1.1 app. `sample` captured repeated SourceEditor.updateNSView → NSTextView selection change → StudioModel.selectionChanged → SwiftUI graph transactions. A hosted-editor run-loop regression failed before the fix and passed after it; model-only compilation tests were insufficient to detect this defect.
