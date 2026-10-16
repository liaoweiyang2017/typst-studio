# Typst Studio 0.1.4

## Interaction and navigation

- Recent-project rows now highlight on press/selection and display loading feedback before navigation; duplicate opens are suppressed while checking a project.
- The five sidebar modes use native AppKit controls with full rectangular hit areas, first-click activation, and immediate mouse-down actions. Hit testing covers the blank space around each icon.
- Outline rows remain expanded by default and distinguish hierarchy with bold top-level headings, secondary nested text, explicit indentation and vertical guide lines. Individual branches can still collapse.
- Each cached document remembers its PDF page, viewport destination, manual zoom and fit-width preference. Switching to a short file and back restores the previous reading location rather than starting at page one. State lasts for the retained in-memory document session.

## PDF reading colors

Settings → PDF appearance offers Automatic, Original colors, Night and Green reading mode. In Automatic mode, light appearance uses original colors; “PDF in dark appearance” chooses Night or Green. This follows the app's System/Light/Dark setting and is persisted. Detached previews follow the same policy.

Core Image display filters tint the PDF viewport without changing the document or triggering compilation. The tint affects images as well as text. Exported PDFs retain their original page content and colors.

## Native macOS appearance

The application still uses AppKit's standard window buttons. The release build now passes the actual installed SDK version to the linker: the prior binary incorrectly recorded SDK 14.0 despite being compiled against current headers. This build records SDK 27.0 with minimum macOS 14.0, allowing system-native appearance rather than legacy linked-SDK compatibility styling. Validated on the available macOS 27 host; macOS 26 was not separately visually tested.

## Verification and distribution

- All five button corners are hit-tested, first-click handling is checked, and every activation dispatches exactly one action.
- A real hosted PDFKit regression reproduced page 4 → preface → page 1 before the fix, and now verifies page, within-page offset and zoom restoration.
- Tests cover automatic light/dark color policy, rendered night/green pixels, and unchanged exported PDF rendering.
- Existing compilation, cache, dependency, responsiveness and equal-pane regressions pass. Native UI checks verified four-level headings and live Night/Green switching.
- Apple Silicon arm64, macOS 14+, bundled Typst 0.15.1 and unchanged icon 06. Ad-hoc signed, not Apple-notarized. Local builds only; no GitHub Actions.
