# Contributing

Thank you for helping improve Typst Studio.

1. Describe the problem, platform, architecture, app version, and a small redistributable Typst example in an issue. Do not include private documents or signing material.
2. Discuss substantial behavior or architecture changes before implementing them. Requirements and acceptance evidence live in `specs/`.
3. Keep macOS code under `macOS/` and Windows code under `Windows/`. Shared release scripts and the pinned Tinymist patch remain at the root.
4. Use native controls and existing abstractions. Do not add a UI framework to solve a small presentation issue.
5. Run the relevant tests in `macOS/scripts/test.sh` or `Windows/tools/test.cmd`, and verify changed native interactions. Describe what was tested and any limitations in the pull request.

Compilation must not silently save source buffers. Failed writes must retain existing files. Updates must verify signatures and respect unsaved-document cancellation. Background work must not block editing.

Signing keys are maintainer-only and stay outside Git. Contributors can build locally without release credentials. Submitted original contributions use the repository's MIT license; preserve third-party attribution.
