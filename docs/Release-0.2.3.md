# Typst Studio Windows 0.2.3

This release refreshes the native Windows workspace while keeping the macOS application at 0.2.2.

- Colored SVG file icons distinguish Typst, PDF, JSON, Markdown, images, scripts and other documents. Yellow folders change their icon when expanded; the actual compilation entry is bold. Native selection indicators remain intact.
- The native sidebar selector uses consistent Fluent icons and a problem-count badge anchored to the Problems item.
- Compile is the main accent action. Compact formatting commands, a wider zoom field, rounded document surfaces and 1.5× source line spacing improve everyday editing. The status bar includes a live word count.
- Source/PDF navigation shows a prominent temporary destination outline in both directions, including formulas and included source files.
- Original PDF colors remain the default for new profiles. Existing reading preferences are retained; green/night modes affect display only.

Download the Windows ARM64 or x64 installer matching your device. The compiler, PDF renderer and Windows/.NET runtime are bundled. The installer offers English/Simplified Chinese and an optional desktop shortcut. Windows packages are not Authenticode-signed; in-app updates verify the signed feed and installer.

macOS 0.2.2 assets and update metadata are carried forward unchanged so Windows-only publication does not interrupt Mac update checks. No Mac source changes or rebuilds are included. Builds and packaging run locally without GitHub Actions. Windows x64 validation is performed under Windows ARM emulation.
