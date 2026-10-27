# Windows Fluent UI 0.2.3

Date: 2026-10-27. Status: implementation confirmed by the user.

## Scope and rules

Apply the user's Windows UI specification from 2026-10-27. Windows becomes 0.2.3; macOS remains 0.2.2 with its existing binaries and feed. Build Windows ARM64/x64 locally, publish source and packages to the main repository, and run no GitHub Actions.

- ICON: 16×16 colored SVGs for closed/open yellow folders, blue Typst documents, red PDF, orange JSON braces, gray-blue Markdown, green images, gray terminal scripts and default documents. One extension dictionary and one icon-resolution entry. File tree uses 8 DIP icon/text spacing, 32 DIP rows and BodyTextBlockStyle. Preserve native single-selection indicator/background. Mark actual compilation entry SemiBold, including after changing entry; merely browsing files does not change it.
- NAV: reuse the existing SelectorBar; Segoe Fluent Icons at 16 DIP. Keep native underline. Attach the existing InfoBadge to the problems item instead of the containing bar. Zero count hides the badge; changing language/count does not reset selection.
- TOOLS: compile uses AccentButtonStyle with icon/text. Other workspace command buttons use neutral styles. Zoom control minimum width 96 DIP and readable 100%. Use compact, uniformly styled AppBarButton formatting commands. Preserve real command bindings and tooltips.
- PDF: existing Standard/Night/Green setting remains the only reading-color setting. Fresh profiles default to Standard, while existing explicit choices remain respected. Original PDF pixels stay white in Standard, with native shadow or thin outline; viewing surface uses LayerFillColorDefaultBrush. Exported/written PDF bytes remain untouched.
- LAYOUT: retain native Mica/title-bar extension, transparent sidebar, 8 DIP rounded editor/preview surfaces, 4–8 DIP pane gaps and functioning drag splitters. Use theme resources. Set code line height near 1.5× font size and tertiary-colored line numbers. Right status area includes word count using CaptionTextBlockStyle, updated only on content changes rather than every caret move.
- SIMPLICITY: no new services/interfaces, feature flags, user options, fallback chains, logs or unrelated refactoring. No new broad exception handling; maintain established file-safety and external-IO handling. SVG artwork may use fixed colors; application surfaces and text use theme resources.

## Acceptance

1. Every listed extension, uppercase extension and unmapped file has the correct icon; folder icon changes immediately on expand/collapse. SVGs load in both packaged architectures without fallback assets.
2. Tree row height/gap/text style and actual compile-entry emphasis are correct. File browsing, filtering, lazy expansion and inverse source navigation remain functional.
3. Five SelectorBar items respond on first click; selection underline and badge position/count remain correct at normal/minimum sidebar widths and in both languages.
4. At default/minimum window sizes and tested DPI, zoom text and format commands are visible and usable; compile is the sole accent-styled workspace action.
5. Light/dark appearances show readable editor/line numbers, white Standard PDF and optional green PDF. Rounded surfaces, gaps, header alignment and resizing remain coherent.
6. Existing compiler, PDF output, buffer/conflict protection, cross-file/formula mapping and formatting undo regressions pass. Word count handles English text, CJK and empty text without rescanning on caret-only changes.
7. Native ARM64 and emulated x64 smoke pass. Two 0.2.3 installers and signed Windows feeds are verified remotely. Latest Release retains unchanged Mac 0.2.2 update assets; no Mac source/binary version changes.
8. Clean task-owned Mac/Parallels staging and intermediate outputs after dependent validation. Retain current final installers and checksums, sources, user files, installed apps and shared toolchains. Record limits and cleanup failures.

## Approved follow-up: navigation visibility

The user explicitly requested more visible bidirectional-navigation feedback. Windows 0.2.3 adds a theme-accent outline/translucent band at the mapped source row and a larger outline at the PDF target, lasting three seconds. These overlays never edit source/PDF bytes or change the mapping result. No configuration is added; Mac remains unchanged. Verify both directions show feedback, feedback clears, and existing formula/cross-file tests continue passing.

The user further requested a more elegant formatting icon set after reviewing the first pass. The seven compact native AppBarButtons now use a coherent set of 16 DIP theme-colored vector paths for bold, italic, underline, center, bullets, numbered list and summation, replacing font-glyph/formula-font exceptions. Existing actions and tooltips remain unchanged.
