# Typst Studio 0.1.1

Fixes opening multi-file Typst projects and updates the native workspace to match the supplied Edist references more closely.

## Changes

- Welcome screen with Open Folder, New Project from Template, Open File and recent projects. Unsaved drafts can be restored explicitly.
- Opening a folder automatically discovers `main.typ`, including nested source folders, loads it and compiles it. Paths are normalized before comparing file identities.
- Compilation uses a unique temporary sibling source file instead of stdin, preserving relative imports while retaining the selected project root. Original document files are never overwritten by preview compilation; temporary sources are removed after compilation.
- Obsolete compilation jobs are cancelled; slow jobs have a bounded timeout. The last successful preview remains visible on errors and exporting stale output is blocked.
- Lazy file tree includes all non-hidden file kinds and empty folders, expands the selected source's parents, and highlights the selected row across the sidebar width.
- Debounced background project search, generated PDF heading hierarchy, 83 searchable math symbols, and centered empty issue/search states. No Draw tab or handwriting model.
- PDF opens at fit-to-width and adapts when the preview pane resizes. Manual zoom remains under user control. Detached previews have independent zoom controls.
- Retains selected periwinkle/lavender icon 06.

## Validation

Local arm64 debug/release builds, compiler regression script, nested imports, auto-load/compile, file enumeration, empty folders, project search, failed-compile recovery, fit-to-width resizing, cancellation, and every symbol expression. The user's local EMDoc project was tested without changing source content: 182 PDF pages. Timings are local observations, not performance guarantees. The project itself is not bundled or uploaded.

## Distribution

macOS 14 or later, Apple Silicon (arm64), bundled Typst 0.15.1. Ad-hoc signed; not Apple Developer ID signed or notarized. No GitHub Actions workflows are used.

The preview still uses complete CLI compilation rather than an incremental compiler service. Source/PDF lookup is text matching, not exact source maps. Opening an individual included file compiles that file as the current source; keep the project's main file selected for the complete document.
