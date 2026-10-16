# Windows 0.1.8 validation

Validation host: Windows 11 ARM64 in Parallels, with system display scaling enabled. The x64 application and native dependencies run under Windows ARM emulation; physical x64 hardware has not been tested.

The console integration suite passed for ARM64 and x64 using the bundled Tinymist and PDFium. Coverage includes Unicode source coordinates, formula and cross-file source maps, repeated formulas, rotated content, in-memory edits, compilation errors/recovery, PDF text/pixel rendering, atomic saves, and external file conflicts.

The native WinUI smoke harness checks project loading, source-to-preview and preview-to-included-source navigation, editing, automatic compilation, and undo. It captures the document workspace, grouped settings page, and welcome/recent-project page. Use `--smoke-exit` to close the test window after writing its result.

The application has a Windows-native title bar and menu flyouts. Editor and preview share a flat workspace separated by thin rules; only welcome actions and settings rows use Fluent cards. Initial window dimensions account for system DPI.

The editor now uses pinned WinUIEdit/Scintilla native controls with a small Typst syntax scanner. The native harness also checks colored spans, reversible line comments as one undo action, line-number toggling, adjustable pane ratios, English labels, light/dark settings, preference persistence, and settings bounds at normal and narrow window sizes.

A reproduced settings regression placed the right edge at 1319 DIPs inside a 1187-DIP viewport. The fix constrains the ScrollViewer horizontally and centers an 860-DIP content column. A separate WinUI startup failure was caused by registering unnamed OEM virtual-key 191 as a KeyboardAccelerator. Ctrl+/ now uses the routed key handler; named alternative chords still use native accelerators.

Windows installers are not Authenticode-signed. The x64 build has been exercised under Windows ARM emulation, not physical x64 hardware.

Installer validation uses the same payload and language/task definitions with an isolated QA AppId and shortcut name, preserving the user's existing installation. ARM64 is installed in Chinese with a desktop shortcut, and x64 in English without it, then uninstalled. Production installers are separately built from the unchanged production AppId.
