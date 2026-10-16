# Windows 0.1.8 references

- [HarborFiles](https://github.com/liaoweiyang2017/HarborFiles): examined installer language/task entries and grouped settings layout. The application stays WinUI 3; no Harbor source files were changed.
- [WinUIEdit](https://github.com/BreeceW/WinUIEdit): pinned NuGet 0.0.5-prerelease, native Scintilla control for ARM64/x64. SourceEditor contains UTF-8/UTF-16 coordinate adaptation and delayed syntax styling. Syntax styling does not modify source text or add undo steps. WinUIEdit is a prerelease dependency, so native smoke coverage is required for upgrades.
- [TeXstudio](https://github.com/texstudio-org/texstudio): consulted embedded PDF viewer organization. Typst Studio keeps PDFium rendering and Tinymist maps, and adopts resizable adjacent source/PDF panes, compact navigation and zoom controls.
- [Inno Setup Chinese translation](https://github.com/jrsoftware/issrc/blob/main/Files/Languages/ChineseSimplified.isl): bundled for reproducible bilingual installer builds.

No GitHub Actions build or release workflow was added. Build and packaging remain local. Windows uses the windows-v0.1.8 release tag; the application version matches macOS 0.1.8 without replacing its Sparkle feed.
