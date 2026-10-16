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

## 0.1.3 document-switch research

- https://github.com/Myriad-Dreamin/tinymist/blob/main/CHANGELOG/CHANGELOG-2025.md describes avoiding VFS cache invalidation during document switching and managing dedicated compiler instances. This supports retaining per-document state rather than resetting the entire compiler on every selection.
- https://github.com/pndv/typst-renderer describes per-editor preview selection and a pinned main-file option. Pinned-root editing is a different behavior from this task's per-file preview, so no main-file semantics were changed.
- REA 6.5.0 `inspect-native-dispatch-metadata` initially rejected Edist's universal executable due to its 64 MiB analysis limit. Extracting its arm64 slice with `lipo` allowed static metadata inspection. Evidence ID: `ev_fe039b16ddee7e2faf4b411ef1833a411f2996f08428009f555902207613672c`; arm64 SHA-256: `0d792b6d55cdab33a720fb0fdc635541bc69a3fca7e74130fd8f0b4656cf2029`.
- Observed metadata: `DocumentStore` contains `documents` and `activeDocument`; `ViewerCache` has `pdf`, `media`, `bib`; `OpenDocument` stores selection, `scrollOffset`, `textRevision`, `parkedLayout`, and `layoutParkStamp`. This is evidence of retained document/view state, not a proof of Edist's exact eviction policy or compiler algorithm.
- Typst Studio independently implements a small three-entry LRU of decoded PDF + watch process. No Edist code or resources are used. Raw inspection artifacts and binary slices remain local under ignored analysis/tools directories.

## 0.1.4 interaction, view restoration and color research

- https://developer.apple.com/documentation/swiftui/view/contentshape(_:eofill:) documents explicit hit-test shapes. GitHub search found examples of plain buttons needing a full label hit shape. This implementation uses small AppKit buttons with explicit rectangular hit testing and first-click acceptance for the five modes, rather than depending on symbol pixels.
- REA's Edist arm64 metadata exposes an NSView-derived `HoverCircleButton` with `onClick`, `imageView`, `hovering`, `mouseDown:`, `mouseUp:` and tracking-area handlers. This shows native mouse handling exists in Edist, but does not prove that this exact class implements its five sidebar controls. No Edist button source was recovered or copied.
- https://developer.apple.com/documentation/pdfkit/pdfdestination and PDFView.currentDestination provide page-space viewport destinations. Typst Studio stores the page index and destination point alongside scale/fit mode in the existing bounded document session.
- The local macOS 27 SDK's NSWindow API exposes standardWindowButton. Inspection of the old executable's LC_BUILD_VERSION revealed `sdk 14.0`; explicit linker platform version flags now use `xcrun --show-sdk-version` while keeping deployment 14.0. No private window APIs or custom traffic-light drawing are used.
- https://github.com/sumirr/fastpdf at `096034fac94e4b9bc8529a5961313a37939da00c`, FilteredPDFView.swift, demonstrates Core Image filtering with PDFKit. Our implementation uses independently defined color matrices on the bounded PDF viewport via NSView.contentFilters, avoiding full-document image conversion. No source was copied.
- https://developer.apple.com/documentation/appkit/nsview/compositingfilter links the supported contentFilters API; the SDK NSView.h confirms filters apply to view content/sublayers. Tests render exported PDF pages before/after view coloring; byte comparisons are deliberately avoided because PDFKit may regenerate serialization metadata.

## 0.1.5 update distribution and hover behavior

- https://github.com/sparkle-project/Sparkle was reviewed as the established full auto-install framework. This request is implemented as a smaller URLSession-based update checker/download offer rather than introducing automatic installation, signing keys and helper services.
- https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows and https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax describe release publication triggers and contents permissions. The public distribution workflow is restricted to release/dispatch events and uses a pinned official checkout action; it has no access to private source or user credentials.
- The app consumes the public latest-release update.json with an ephemeral URLSession, bounded timeout/manifest size, strict stable-version parsing, and fixed GitHub repository asset URLs. It never executes manifest content or silently installs a downloaded binary.
- Welcome buttons use one shared SwiftUI ButtonStyle with full-label contentShape, onHover state and pressed feedback. No tracking areas or manual cursor push/pop are needed.

## 0.1.6 — Settings and language

- https://github.com/sindresorhus/Settings — native multi-pane settings reference. We use SwiftUI List + grouped Form directly, without a dependency.
- https://github.com/Abedalkareem/LanguageManager-SwiftUI — runtime language selection reference. Our small English/Simplified Chinese catalog resolves a persisted language selection and keeps preference identifiers independent of translated labels.
- https://developer.apple.com/documentation/swiftui/form — native grouped controls.

App-owned settings, welcome and primary command labels switch immediately; OS dialogs and raw compiler diagnostics retain their own language. Localization does not reset the editor or PDF cache.
