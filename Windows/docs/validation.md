# Windows 0.1.0 validation

Validation host: Windows 11 ARM64 in Parallels, with system display scaling enabled. The x64 application and native dependencies run under Windows ARM emulation; physical x64 hardware has not been tested.

The console integration suite passed for ARM64 and x64 using the bundled Tinymist and PDFium. Coverage includes Unicode source coordinates, formula and cross-file source maps, repeated formulas, rotated content, in-memory edits, compilation errors/recovery, PDF text/pixel rendering, atomic saves, and external file conflicts.

The native WinUI smoke harness checks project loading, source-to-preview and preview-to-included-source navigation, editing, automatic compilation, and undo. It captures the document workspace, grouped settings page, and welcome/recent-project page. Use `--smoke-exit` to close the test window after writing its result.

The application has a Windows-native title bar and menu flyouts. Editor and preview share a flat workspace separated by thin rules; only welcome actions and settings rows use Fluent cards. Initial window dimensions account for system DPI.

The first Windows version uses a native plain-text editor. It does not yet provide the Mac editor's syntax highlighting, symbol browser, or all Mac preferences. Windows installers are not Authenticode-signed.
