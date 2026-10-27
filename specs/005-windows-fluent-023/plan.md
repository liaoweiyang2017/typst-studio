# Technical plan

## Existing implementation

MainWindow.xaml owns the three-pane layout and TreeView template. MainWindow.Files.cs uses lazy TreeViewNode children and a small FileItem record with a Glyph property; there is no separate application-wide ViewModel framework. SidebarTabs.cs already wraps native SelectorBar and InfoBadge, but the math tab uses a different font and the badge is anchored to the whole bar. SourceEditor.cs styles Scintilla; MainWindow.EditorTools.cs builds formatting commands. PdfViewer.cs already supports Standard/Night/Green and ThemeShadow. MainWindow.Chrome.cs already enables Mica and extends content into the title bar. Modular pinned WinUI packages already compile SelectorBar, so no dependency update is needed for that control.

## Implementation

1. Add local SVG assets with paths rather than font-dependent SVG text. Use a case-insensitive static extension dictionary; FileItem exposes the resolved SvgImageSource and compilation-entry FontWeight. Use minimal property notification for folder expansion/entry updates; retain the existing lazy tree model and default TreeViewItem template.
2. Keep SidebarTabs and update its icon/badge placement directly. Use native item content/layout rather than copying a full SelectorBar template.
3. Use native compact AppBarButtons with a single style; preserve formatting actions. Increase the existing zoom width token to 96 DIP and check narrow-toolbar overflow thresholds.
4. Add surface corner radii and symmetric pane spacing to the current layout, preserving named editor/preview elements and drag behavior. Keep the default PDF reading mode and existing user choices. Adjust Scintilla ascent/descent based on current font/DPI. Compute word count alongside the existing content-change count, using an explicit English-token/CJK-character rule documented in acceptance.
5. Restrict version changes to Windows version metadata, installer labels and Windows documentation. For v0.2.3, sign new Windows feeds and carry unchanged macOS 0.2.2 feed/manifest/assets into the latest Release. Reuse the prior Mac download URL and signature; do not recompile or resign Mac.
6. Extend existing native smoke checks only for meaningful changed behavior (icons, entry change, layout/badge/zoom/line metrics, formatting and count). Review actual light/dark screenshots. Reuse per-architecture staging during retries, then perform the mandated build cleanup.

## Primary implementation references

- Microsoft WinUI Gallery source: https://github.com/microsoft/WinUI-Gallery — native SelectorBar, TreeView, image and command examples.
- Existing Gallery SelectorBar usage: https://github.com/microsoft/WinUI-Gallery/blob/main/WinUIGallery/Pages/HomePage.xaml
- WinUI TreeView design/selection rationale: https://github.com/microsoft/microsoft-ui-xaml/issues/4305
- WinUI compact AppBarButton API: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.appbarbutton.iscompact
- Command bar guidance: https://learn.microsoft.com/en-ca/windows/apps/design/controls/command-bar
- Scintilla ascent/descent documentation: https://scintilla.org/SciDocSearch.html

## Approval and tooling

The user confirmed this iteration's implementation plan on 2026-10-27.

Serena's mandatory initial instructions were read. Its C# symbol query failed because the Mac host has no .NET 10 runtime; the tool explicitly instructed stopping and waiting instead of attempting workarounds. The user subsequently directed debugging inside Parallels instead. No Mac runtime is installed; use Windows VM SDK/build/runtime diagnostics and native smoke verification for this iteration.
