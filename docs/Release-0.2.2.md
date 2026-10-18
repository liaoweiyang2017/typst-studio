# Typst Studio 0.2.2

- Successful compilation now saves a same-name PDF beside the compilation entry on macOS and Windows, including automatic updates. Failed or canceled output preserves the previous file; source buffers are not silently saved.
- Windows adds native background update checks, download progress, strict signed-feed/package verification, cancellation and an explicit install/restart action with unsaved-document protection. Mac retains its native Sparkle update flow.
- All source and installers move to the main `liaoweiyang2017/typst-studio` repository. The project is open source under MIT; third-party licenses remain intact.
- Source is organized under `macOS/` and `Windows/`, with unified English/Chinese documentation and native screenshots.

Install 0.2.2 manually if you are using 0.2.1 or earlier. The previous release repository is retired; all future update feeds use the main repository.

Packages: macOS ARM64, Windows ARM64 and Windows x64. All are built locally without GitHub Actions. Mac uses an ad-hoc application signature (not Developer ID notarized); Windows installers are not Authenticode-signed. In-app update archives are additionally verified with developer signatures. Windows x64 validation runs under Windows ARM emulation.
