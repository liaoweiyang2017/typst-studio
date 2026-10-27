# Windows 0.2.3 acceptance

Date: 2026-10-27. Implementation confirmed by the user. Compilation and native debugging performed in the local Parallels Windows ARM VM; x64 runs under emulation. Mac 0.2.2 source/binaries are unchanged.

## Verified locally

- ARM64/x64 Release builds: zero warnings/errors. Both payloads include all nine SVG assets.
- Native reports in validation-arm64.json and validation-x64.json: success, 0.2.3, 200% rasterization scale. Checks cover case-insensitive/default extension mapping, real native SVG decode, folder expand/collapse, live entry emphasis, badge placement/zero hiding, theme switching, 1.5× native line height, empty/English/CJK word counts and native formatting controls.
- Both native runs verify exact repeated-formula/cross-file source mapping, source/PDF destination overlays, three-second clearing, stable entry on file browse/inverse, error-preview retention, rapid compile-target changes, native highlighting, format/find/replace undo, sidebar restore, zoom/page navigation and narrow-window settings/welcome layout.
- Core ARM64/x64 regression suite passes: signed update verification, tampered feeds/packages, architecture/downgrade/cancel behavior, project discovery/search, Unicode offsets, CRLF and atomic-save conflict protection, adjacent PDF protection, PDFium/text/formula navigation, unsaved dependencies and compiler recovery.
- Real light/dark-green editor screenshots visually reviewed after the final vector formatting-icon update. Native tree indicator and subtle selection background are preserved. Navigation screenshot confirms a full source-row outline and a clearly visible PDF formula-target outline.
- Theme regression found that the old application-level brush lookup returned light brushes during a root-level dark override. Replaced the fallback search with direct themed XAML-resource resolution; switching both directions now passes.
- SVG verification uses native SetSourceAsync decoding; ImageOpened is not a reliable repeated-load notification for cached shared SVG sources.
- Production Windows feed/package Ed25519 signatures are independently checked with the shipping managed verifier after packaging.

- Isolated per-architecture QA installers using the final payload installed successfully; installed App DLL hashes match the final publish directories. Both installed applications passed the full native suite after installer work completed (installed-arm64.json / installed-x64.json).

## Delivery and cleanup

Pending final installer checks, source push, public Release/feed verification and removal of identified build/test staging. Complete these before reporting the task finished.

## Limits

No physical x64 device, exhaustive GPU/driver matrix or general latency benchmark. Native UI tests cover actual control geometry/theme behavior and existing commands; two-page fixture compile timings are evidence of regression execution, not an Edist performance comparison. Windows installers remain unsigned by Authenticode; application update feeds/packages are cryptographically signed.
