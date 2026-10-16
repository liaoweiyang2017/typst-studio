# Typst Studio 0.1.2

Fixes the project-opening UI freeze and adds persistent incremental compilation.

## Fixes

- Fixed a SwiftUI/AppKit feedback loop: programmatic cursor movement during editor updates emitted selection notifications, which republished model state and immediately rebuilt the editor. Recent Projects and Open Folder could therefore starve the main event loop before rendering the source. Editor updates now suppress delegate feedback, and unchanged cursor positions are not published again.
- The editor and PDF preview now start at equal widths using a small native split view. The divider remains draggable and preserves the chosen ratio when resizing.
- Lazy directory expansion runs filesystem reads off the main thread.

## Compilation

- Keeps one bundled `typst watch` process per active source, retaining Typst's incremental cache instead of starting a cold compiler after every edit.
- Coalesces edits while busy, guards published results by revision, supports imported-file change notifications, handles errors and warnings, and bounds hung compilation.
- Keeps the private shadow-source inode stable for file-provider folders. Bounded notification retries cover Typst's dependency-registration interval immediately after compilation. Original source files are not overwritten for preview.
- Draft recovery writes run on a background queue; edit debounce reduced to 120 ms. PDF decoding also happens off the main thread.
- Installed `.app` builds use only `Contents/Helpers/typst`. They do not fall back to a Homebrew/system compiler or download a compiler. Source-only development tests can explicitly use `TYPST_BIN`.

Local EMDoc benchmark (182 pages): cold watch compile 1.590 s; three warm edits 0.478 / 0.256 / 0.408 s. These are observed compile/result delivery timings and exclude the 120 ms typing debounce; first compilation still does the full work. Actual times depend on document changes, fonts and machine.

## Verification and distribution

- Real NSHostingView/AppKit editor regression: old code starved an 8-second heartbeat; fixed code loads and completes the 3-second heartbeat. Also asserts the initial 50/50 layout.
- Compiler tests cover nested projects, errors/recovery, dependencies, rapid coalesced revisions, one-process reuse, all symbols, and a local large-document benchmark.
- macOS 14+, arm64, Typst 0.15.1 bundled. Ad-hoc signed, not Apple-notarized.
- Built locally; no GitHub Actions. Document-specific packages not already cached may still need network access; the compiler itself requires no download.

Exact PDF/source mapping and semantic completion remain outside this release. Opening an included source still compiles the selected file; keep main.typ selected to render the complete document.
