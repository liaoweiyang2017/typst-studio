# Typst Studio for Windows

Windows 0.2.3 uses a native WinUI 3 interface, a persistent Tinymist process, and PDFium rendering. The macOS application continues to use SwiftUI/AppKit/PDFKit.

## Features

- Native icon sidebar selector: Project files (default), Project search, Document outline, Math symbols, and Problems.
- Source search includes unsaved buffers, supports case sensitivity, cancels obsolete requests, and caps results at 200. Search is scoped to Typst source files.
- Main-document PDF outline preserves cross-file headings and hierarchy, expands by default, and navigates to PDF locations. Current-source headings are the fallback before a PDF is available.
- Searchable Mac symbol catalog inserts Typst math syntax at the selection. Compiler problems navigate to their source line and can be recompiled from the panel.

- Fluent welcome page with Open Folder, New from Template, Open File and Recent Projects; native top menu bar and grouped settings page.
- A Mica-backed sidebar with colored SVG file icons, 32 DIP native tree rows and a bold compilation entry. Rounded editor/PDF surfaces retain draggable pane dividers.
- A native SelectorBar with a problem badge, an accent Compile action, compact formatting commands, readable PDF zoom, 1.5× code line spacing and live word count.
- Bidirectional navigation briefly outlines the source row or PDF destination, including formula and cross-file jumps.

- Open a project folder or Typst file, automatically choose main.typ, filter and navigate source files.
- Native Scintilla editing with Typst syntax highlighting and optional line numbers, undo/redo, find, font sizing and plain-text paste. Unsaved buffers are retained when switching source files; switching resets the visible editor's undo history.
- Debounced in-memory compilation. Failed builds keep the previous PDF and prevent exporting stale output.
- Continuous PDF preview with fit width, zoom, visible-page rendering, text selection/copy, and source-map navigation.
- Ctrl+Shift+J maps the source cursor to PDF; Ctrl+click PDF or select text and press Ctrl+Alt+J maps back. The main preview remains active when editing an included file.
- Atomic saves preserve CRLF/LF; external disk conflicts are reported rather than overwritten. Save Copy preserves a separate copy of the current text.
- English/Simplified Chinese, native light/dark/system appearance including the title bar, adjustable editor/PDF split, recent projects and per-user settings.
- Configurable line-comment shortcut (Ctrl+/ by default), wrapping, font size, automatic compilation and typing delay. PDF standard/night/soft-green display modes do not alter exported files.
- English/Chinese installer with an optional desktop shortcut.

The Windows version focuses on editing, compiling and preview. Package/library sources outside the opened project are not automatically opened. Unrendered syntax may have no mapped page position. Distribution packages include .NET, WinUI, Tinymist and PDFium; no compiler download is needed on first launch. Typst package imports may use the standard package cache/network.

## Build

Use Windows with .NET 10 SDK, Visual Studio C++ tools (ARM64/x64), Rust >= 1.92 and LLVM/Clang. Inno Setup 6 builds the per-user installer. NuGet versions are pinned in the project files.

1. Build Tinymist at the commit recorded in `native-dependencies.json`, applying `../vendor/tinymist-source-map.patch`. `tools/build-tinymist.cmd` documents the two MSVC targets used by the local build. Supply the Windows SDK/MSVC environment for each target.
2. Place the resulting `tinymist.exe` in `native/win-arm64` and `native/win-x64`.
3. Download `pdfium-win-arm64.tgz` / `pdfium-win-x64.tgz` from [PDFium binaries chromium/8100](https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium%2F8100). Verify the hashes in `native-dependencies.json`, and place each `bin/pdfium.dll` alongside its matching helper. License texts are tracked in `licenses/`.
4. Run `tools\test.cmd`, then `tools\build.cmd`. An architecture argument (`arm64` or `x64`) restricts either command.
5. Run Inno Setup against `installer/TypstStudio.iss` with `SourceDirectory` and `TargetArchitecture`. `tools/package.cmd` is the local invocation. Outputs are self-contained; the installer does not change file associations or require administrator privileges.

`tools/vm.py` is an optional macOS/Parallels development helper for explicit PowerShell commands; it preserves Unicode quoting without changing Windows execution policy.

## Validation

`tests/TypstStudio.Tests` exercises actual compiler and native PDF binaries: Unicode offsets, formulas, repeated formulas in different files, rotations, unsaved includes, errors/recovery, PDF pixels/text hits, atomic save and external conflicts.

The app's `--smoke <owned-fixture-directory>` mode drives the real WinUI editor and preview, verifies editing/undo and cross-file navigation, and writes `native-smoke.json` and screenshots `native-smoke.png`, `welcome.png`, and `settings.png` in that fixture. It uses isolated settings and leaves the test window open for inspection unless `--smoke-exit` is supplied. Only use a disposable fixture created for the test.

## Source and releases

Source and all platform installers share the main public typst-studio repository and its v0.2.3 Release. The Mac 0.2.2 assets and update metadata are carried forward unchanged. Architecture-specific signed feeds select Windows packages. Windows 0.2.3 packages are not Authenticode-signed.

Successful compilation writes the original PDF beside its entry source. Native startup update checks use NetSparkle core 3.1.0 with Strict Ed25519 verification and a WinUI state bar; installation waits for explicit restart approval and saved-document protection.
