# Screenshot follow-up — 2026-10-18

User-requested scope: each platform shows two real editor screenshots, light UI with original white PDF and dark UI with green reading mode. Update both README languages. No product behavior or release-version change.

Plan: use examples/showcase in disposable demo projects; capture native rendering; verify all four combinations; update captions and image paths; push documentation.

Acceptance: four editor images visibly show the specified UI/PDF combination. README.md and README_CN.md both link to all four. Existing welcome/settings examples remain available.

Capture: macOS uses the native application through CUA and its appearance/PDF settings. Windows uses the existing native RenderTargetBitmap capture path, now with separate light and dark processes and a --dark-green capture-only option. Setting Application.RequestedTheme before native resource initialization keeps screenshot resource resolution consistent; normal launch is unaffected. PDF colors use the application's existing reading-mode renderer, without image recoloring. A local ARM64 build passed with zero warnings/errors. Release installers are unchanged.

Result: all four images visually reviewed; both README image links validated. No private project content used.
