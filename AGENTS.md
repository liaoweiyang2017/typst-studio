# Typst Studio

For requirements, planning, implementation, reviews, or releases, read [.specify/memory/constitution.md](.specify/memory/constitution.md) first. It records the user's specification-first workflow and implementation approval gate.

The current baseline draft is [specs/001-project-baseline/spec.md](specs/001-project-baseline/spec.md). Treat historical release behavior as evidence, not as approval for new scope. Record each iteration's requirements, plan, tasks, and acceptance evidence under its own specs directory.

Active approved iteration: [0.2.2 open source and updates](specs/004-open-source-updates-022/spec.md). For 0.2.2 implementation or release work, read its spec, plan and tasks. macOS sources/builds are under `macOS/`; Windows sources/builds under `Windows/`. Root `scripts/` handles shared publication. Source and all release assets use the main public `liaoweiyang2017/typst-studio` repository. No GitHub Actions; signing keys stay outside Git.
