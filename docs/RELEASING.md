# Local release workflow

All source and release assets live in `liaoweiyang2017/typst-studio`. Do not use GitHub Actions. A release is complete only after all three packages and the signed platform feeds have been verified remotely.

## Prepare

1. Update the visible version in both platforms and release notes; increment the Mac `CFBundleVersion`. Write GitHub Release titles and notes in English, using `Typst Studio VERSION` and `docs/Release-VERSION.md`. Keep the default README in English and the Chinese translation in `README_CN.md`.
2. Run `macOS/scripts/test.sh`, Windows `tools\test.cmd`, native smoke tests and the relevant isolated-update harness.
3. Build Mac with `macOS/scripts/build-app.sh` into a non-cloud temporary directory, then `macOS/scripts/package-dmg.sh`. Build Windows with `Windows/tools/build.cmd` and Inno Setup. Review every architecture/version before publishing.
4. Refresh the screenshots only from `examples/showcase` copied into a disposable demo directory, using isolated app settings. Never capture private projects for the README.

## Keys and signing

- Mac: `scripts/sign-update.sh` uses the existing Sparkle signing account `typst-studio-updates` in the local login Keychain. Approve any required Keychain prompt yourself.
- Windows: `scripts/sign-windows-updates.py` expects the local Ed25519 PEM key at `~/.config/typst-studio/updates/windows-ed25519.pem` (or `--key`). Keep it owner-readable only and back it up securely. It must match `Windows/UpdatePublicKey.txt` and the public key compiled into the WinUI application.
- An initial Windows key can be created with OpenSSL 3's `genpkey -algorithm ED25519`; do not generate a replacement for an established channel. Key rotation needs a transition release trusted by the existing key.
- Set `OPENSSL_BIN` or put OpenSSL 3 first in PATH. The signature tools never need a private key in GitHub, Git, screenshots or an environment dump.

The Windows signer emits `appcast-windows-arm64.xml`, `appcast-windows-x64.xml`, and a `.signature` file for each. Both feed and installer signatures are required by NetSparkle Strict mode. Mac `appcast.xml` uses Sparkle's archive signature.

## Publish

Run `scripts/publish-release.sh` after all packages are ready. It signs local artifacts, creates a draft `vMAJOR.MINOR.PATCH`, uploads all assets, publishes it as latest, and verifies the legacy Mac update manifest locally. All commands must succeed before announcing the release.

Required assets:

- `Typst-Studio-VERSION-macOS-arm64.dmg` and its `.sha256` file
- `Typst-Studio-VERSION-Windows-arm64-Setup.exe`
- `Typst-Studio-VERSION-Windows-x64-Setup.exe`
- `SHA256SUMS-VERSION.txt`
- `release-info.json` and `appcast.xml`
- both `appcast-windows-ARCH.xml` files and their `.signature` files
- `update.json`, generated after re-downloading and checking the Mac release

The latest Release must contain all platform feeds. Publishing a later release with only one platform would break the other platforms' stable feed URLs. Windows enclosures are validated against the main repository, exact version, asset name and installed architecture.

Compare GitHub asset digests with local SHA-256 values. Verify the Mac feed's build number and each Windows feed's version/size/signature. Test installation in a separate application identity/directory; never silently overwrite a developer's working installation during tests.

## Updating

Mac Sparkle and Windows NetSparkle perform downloads and validation off the UI thread. A ready update does not close the app until the user approves installation and the document-save flow permits it. Inno receives fixed local arguments with `/STUDIOUPDATE=1`; only this silent update mode relaunches the installed application. The updater does not force-kill other editors.

Version 0.2.2 migrates from the retired `typst-studio-releases` repository. Users of earlier releases need one manual installation, because the old Mac URL disappears and old Windows releases have no updater.

## Windows-only releases

When macOS stays on an existing release, run `scripts/publish-windows-release.sh vMAC_VERSION` with the prior Mac tag, for example `v0.2.2`. It signs the new Windows packages and carries forward the unchanged Mac installer, checksum, Sparkle feed and legacy manifest. Their existing download URLs and signatures remain valid. Verify the public latest-feed URLs and remote asset digests before announcing. Do not run the combined publication script, which derives its version from the Mac bundle.
