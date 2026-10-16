# 0.1.1 implementation references

Reviewed on 2026-10-16 before implementation.

- https://github.com/SuperMegaFort/TypstEdit at `b90e3ffa8f45f55b14dd5144ae0f7460a931fd28`: `Sources/TypstCompiler.swift` uses a sibling shadow source and a Typst process to preserve relative imports. We adopted the sibling-source approach, independently implemented unique names, explicit project root, process cancellation/timeout and cleanup. Its source was not copied or vendored (no root license file was found).
- https://github.com/duhman/pdfview at `a5ecacf1eb6ca1144af6e990385b1e5206ea6514`: `Sources/Views/PDFKitView.swift` separates automatic and manual scaling, uses PDFKit notifications and avoids needless document replacement. We retained PDFKit and implemented width fitting directly rather than introducing a dependency. No code was copied.
- https://typst.app/docs/reference/symbols/sym/: checked symbol names against the bundled compiler; tests compile every palette expression.
- https://github.com/morluto/rea, installed npm 6.5.0: re-ran `inspect-artifact`, `inspect-macho` and `inspect-plist` on the locally installed Edist app. Local evidence is in ignored `analysis/edist-*-0.1.1*.json`. This is bundle/native metadata evidence, not recovery of Edist's Swift source or runtime algorithm. User-supplied screenshots are the primary UI behavior/style reference.

The implementation remains SwiftUI/AppKit/PDFKit with no new runtime framework. File tree traversal is lazy; project indexing/search and compilation run off the UI thread. Compilation is serial and revision-guarded. Build and release scripts run locally; no CI workflow was added.
