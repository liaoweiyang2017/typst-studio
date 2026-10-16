# Windows 0.1.9 sidebar

Uses the native WinUI ComboBox with icon/text content, following the [WinUI Gallery](https://github.com/microsoft/WinUI-Gallery). Panel controls are retained when switching, and search/symbol filters are preserved. The outline is refreshed from the latest PDF and expanded by default. The default is Project files.

The Mac sidebar was inspected in Sources/TypstStudio/App.swift and SidebarTools.swift. Its outline is based on PDF bookmarks, not the language server's programming-symbol outline. Windows therefore reads [PDFium bookmark destinations](https://github.com/chromium/pdfium/blob/main/public/fpdf_doc.h) on a background task and uses the existing preview. This avoids extra compilation and preserves compiled numbering/cross-file structure. The symbol table is copied from the Mac implementation; handwriting is not included.

Project search runs off the UI thread, waits 200 ms after typing, cancels obsolete work, and reports up to 200 source-line matches. Disk files above 1 MB are skipped; already-open buffers are searched in memory. Generated `.typst-studio-*` snapshots are excluded. Diagnostics retain LSP UTF-16 positions, avoiding a Unicode-scalar conversion when selecting an error location.

Validation includes native PDF outline extraction, unsaved/case-sensitive/bounded search, symbol insertion context, structured diagnostics, both architecture builds, and real WinUI panel screenshots and visible-item checks. The native harness disables its file watcher so writing test screenshots inside the fixture cannot cause extra compilation.

Release uses windows-v0.1.9. No GitHub Actions build is added, and the macOS update feed remains separate.
