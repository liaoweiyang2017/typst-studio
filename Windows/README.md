# Typst Studio for Windows

Windows 0.1.0 uses a native WinUI 3 interface, a persistent Tinymist process, and PDFium rendering. The macOS application continues to use SwiftUI/AppKit/PDFKit.

## Features

- Fluent welcome page with Open Folder, New from Template, Open File and Recent Projects; native top menu bar and grouped settings page.
- A continuous three-pane workspace with thin separators, without rounded cards around source or PDF.

- Open a project folder or Typst file, automatically choose main.typ, filter and navigate source files.
- Native plain-text editing with undo/redo, find, font sizing and plain-text paste. Unsaved buffers are retained when switching source files; switching resets the visible editor's undo history.
- Debounced in-memory compilation. Failed builds keep the previous PDF and prevent exporting stale output.
- Continuous PDF preview with fit width, zoom, visible-page rendering, text selection/copy, and source-map navigation.
- Ctrl+Shift+J maps the source cursor to PDF; Ctrl+click PDF or select text and press Ctrl+Alt+J maps back. The main preview remains active when editing an included file.
- Atomic saves preserve CRLF/LF; external disk conflicts are reported rather than overwritten. Save Copy preserves a separate copy of the current text.
- Native light/dark/system appearance, recent projects and per-user settings.

The first Windows release focuses on editing, compiling and preview. Package/library sources outside the opened project are not automatically opened. Unrendered syntax may have no mapped page position. Distribution packages include .NET, WinUI, Tinymist and PDFium; no compiler download is needed on first launch. Typst package imports may use the standard package cache/network.

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

Source stays in the private typst-studio repository. Windows releases use `windows-v0.1.0` tags in the public distribution repository and do not replace the macOS Sparkle latest feed. Windows 0.1.0 packages are not Authenticode-signed.
