# Edist analysis and implementation status

Inspected 2026-10-16. Input: `/Users/mac/Downloads/Edist.dmg`, mounted read-only. Reference features: https://edist.app/ . Analysis tool: https://github.com/morluto/rea , npm rea-agents 6.5.0.

## Evidence

`analysis/edist-artifact.json`: REA inspect-artifact, package/resource observations.
`analysis/edist-macho.json`: REA inspect-macho, universal x86_64/arm64 Mach-O.
`analysis/edist-dylibs.json`: REA trace-dylib-resolution, framework/load information.
`analysis/edist-plist.json`: REA inspect-plist, Edist 1.4, minimum macOS 15, bundle identifier and document association.

Observed links include SwiftUI, AppKit, PDFKit, CoreText, CoreML, NaturalLanguage, AVFAudio and Sparkle. Resources include DetypifyModel.mlmodelc, symbol classes, TypstDocs, templates, localized strings, and typing audio. These establish packaged dependencies/resources; they do not establish exact runtime algorithms. No source recovery or decompilation of editor/compiler internals was performed. No proprietary resources were copied into the implementation.

## Feature mapping for 0.1.0

| Edist feature | Typst Studio implementation |
| --- | --- |
| Native editor, project sidebar, PDF preview | SwiftUI + NSTextView + PDFKit; running locally |
| Live preview | 350 ms debounce, bounded serial CLI compilation, obsolete result suppression; not incremental |
| Syntax colors | Regex-based initial highlighter; not a full Typst parser |
| Smart completion | Esc-triggered keyword completion; no language server yet |
| Inline errors | Underlined source line, diagnostic panel, jump to line |
| Package handling | Bundled Typst's standard automatic package resolution; network package downloads not smoke-tested |
| PDF to source / follow cursor | Text matching only; repeated text, formulas and generated content are not precise |
| Floating preview | Separate PDFKit window sharing document model |
| Handwritten symbols | Not implemented; searchable symbol palette provided |
| Local fonts | Typst system font discovery; font setting insertion |
| Spell check | macOS spell checking; may also flag code |
| Image/PDF assets | NSImage-based asset preview; PDF asset preview initially shows a rendered representation |
| Typing audio | Optional system sound |
| Themes | System/light/dark, editor font size |
| Local packages | Standard Typst package locations |
| Vim | Not implemented |
| Compiler downloads/version manager | Not implemented; bundled local Typst 0.15.1 |
| Automatic application updates | Not implemented; no update service configured |
| Localization | Initial UI in English; Chinese source text supported by installed fonts |
| Animated caret / rounded selection | Standard native text controls only |

## Validation

Swift debug and release builds passed. Compiler smoke test passed PDF generation, relative include, math, compiler-error capture, and rejection of paths outside project root. Native window inspected via accessibility and screenshot: sample document compiled to a one-page PDF with table and equation. No performance claim beyond observed sample compilation. App is a local ad-hoc signed Apple Silicon build, not notarized or Intel-tested.

## Next work

Add Tinymist or an equivalent persistent language/compiler service for semantic completion, exact source mapping and incremental compilation. Add multi-document sessions, dependency change watching, Chinese UI localization, broader save/close regression tests and a distributable universal toolchain before calling this full feature parity.
