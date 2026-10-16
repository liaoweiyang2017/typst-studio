# Typst Studio 0.1.5

## Welcome screen

Open Folder, New Project from Template, Open File and recent-project rows now have full-row hover highlights and distinct pressed/selected feedback. Recent-project loading feedback is retained.

## Independent PDF palettes

In Settings, Automatic PDF appearance now has separate choices for light and dark app appearance. Either can use Original colors, Night or Green reading mode. Fixed PDF modes also continue to work independently of the app theme. Settings are available from the welcome screen as well as the editor. Display colors do not change exported PDFs.

## Public updates

Public downloads now live at https://github.com/liaoweiyang2017/typst-studio-releases/releases . Application source remains private.

The app checks for updates asynchronously once per launch by default. This can be disabled in Settings; manual checks are available in the app menu and Settings. New versions offer a DMG download; installation remains user-controlled. Network failures do not block startup or document editing. No GitHub login/token or document information is sent by the app.

The public distribution repository runs GitHub Actions when a stable release is published. It verifies the uploaded DMG's SHA-256 and build metadata, then publishes update.json. This workflow does not build or access the private application source. Releases are uploaded as drafts before publication so validation sees complete assets.

## Verification and requirements

Regression checks cover light/dark palette mapping, version comparison, newer-release detection, no downgrade, once-per-launch behavior, malformed/HTTP-failure recovery, and allowlisted download URLs, alongside the editor/compiler/PDF tests.

Apple Silicon arm64, macOS 14+, bundled Typst 0.15.1. The application remains ad-hoc signed and is not Apple-notarized. Existing 0.1.4 installations need to install this version manually to gain update checking.
