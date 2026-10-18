# 0.2.2 acceptance evidence

Validation date: 2026-10-18. Builds are local; no GitHub Actions were used.

## Completed validation

- PDF01–03: Mac PDFOutput tests pass for adjacent output, byte equality, unchanged mtime on identical output, read-only/symlink protection and source preservation. Windows core tests pass for adjacent output, byte equality and read-only protection. Both use original compiler PDF bytes, not rendered preview filters.
- Existing compiler/model regression coverage passes for retained entry on file switches and inverse navigation, unsaved included-file compilation, stale navigation cancellation and failed-compile preview retention.
- Full Mac regression suite: passed (`/tmp/typst-tests-022-final.log`).
- Windows core tests: ARM64 and x64 passed (`C:\TypstStudio-build\tests-022.log`), including signed-feed/package tampering, wrong architecture, downgrade, missing feed, download cancellation and canceled installation.
- Windows native Release smoke: ARM64 and x64 reports both `success: true`, version 0.2.2. Covers cross-file/formula mapping, explicit compile target, stable PDF on browsing, undo, find/replace, sidebar and preview controls. x64 ran under Windows ARM emulation.
- UPD04: isolated QA installers upgraded actual 0.2.1 payloads to 0.2.2 on ARM64 and x64, in paths containing spaces and Chinese characters. Harness reports confirmed cancel protection, external-save-conflict protection, saved source and exactly one installer handoff. Inno logs report installation success; installed executable version is 0.2.2.0; one restarted GUI process per installation was observed. The harness runs the new updater with an injected 0.2.1 current version; this does not imply the original 0.2.1 Windows app has an updater.
- Installer regression caught an early `{app}` expansion in AppMutex. Moved the path-dependent mutex check to PrepareToInstall, rebuilt and repeated both upgrade tests successfully.
- Running-instance guard: a second installer attempt while the ARM64 QA app was open returned Inno exit code 7 with an explicit close-windows message; no forced shutdown.
- Production Windows installer and feed signatures generated with OpenSSL were independently verified using the shipping Chaos.NaCl verifier for both architectures.
- Mac release build and DMG signature/checksum validation pass; Sparkle appcast generated using the existing local signing key.
- DOC01: six real product screenshots reviewed; demo content comes from examples/showcase, not private EMDoc. English README.md and README_CN.md links checked. All example entry files compile without external packages.
- Initial history pattern scan: 24 commits / 388 blobs, no matching private-key/token patterns. Current staged-file scan: 203 tracked files, no matching private-key/token patterns. These checks are not an exhaustive security audit.

## Limits

- macOS keeps its existing Sparkle update implementation; its cancellation/restart regression tests passed. No new production-version Mac replacement was forced over the user's installation.
- Windows download/verification runs on background tasks and native interaction smoke passes. A full manual network-throttled editing session has not been measured as a latency benchmark.
- No physical x64 Windows machine was used. macOS is ad-hoc signed, not notarized; Windows installers are not Authenticode-signed. Update signatures are separate cryptographic checks.
- Disk exhaustion and all possible antivirus/file-lock interactions have not been exhaustively simulated.

## Publication

Source commit `8822519` is pushed. The main repository is public under MIT, and private vulnerability reporting is enabled. Release `v0.2.2` has an English title/body and all three installers. GitHub asset SHA-256 digests match local files; all five platform feed/signature URLs were downloaded anonymously and match local files. The legacy update.json points to the main repository and the correct Mac hash. Main repository Actions run count is zero.

Historical main-repository Releases were deleted after verification. Old distribution Releases are being removed. Deleting `liaoweiyang2017/typst-studio-releases` returned HTTP 403 because the gh credential lacks `delete_repo`; the user has been asked to authorize that scope. The old repository itself must not be reported deleted until that succeeds.
