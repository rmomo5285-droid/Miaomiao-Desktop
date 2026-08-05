# Miaomiao core bundle lock

Release packages use a reviewed, immutable core bundle for every target. The
current lock points at commit `753dae71e7260b8374739f18bc683912aee8dfe6` of
`2dust/v2rayN-core-bin`, with a locally verified SHA-256 for each archive. A
bundle must be a ZIP file containing exactly one `bin/` directory. Only that
directory is copied into the Miaomiao package.

Blank entries are intentional. Release packaging fails until each requested
target is pinned. Never point an entry at a branch URL, a `latest` URL, or a
mutable artifact.

Packaging is designed for GitHub Actions. CI installs the exact .NET SDK
version, builds the checked-out `GITHUB_SHA`, stages the locked core, and only
then creates release archives. The release job signs every finished archive
with the Miaomiao GPG release key. Do not use these scripts to publish locally
built binaries.

The release validation job also runs `branding/verify-orange-icons.sh`. Regenerate
icons with `branding/generate-orange-icons.sh` and complete installed-app visual
review before authorizing a release workflow.

Linux releases are built inside an Ubuntu 22.04 container and require glibc
2.35 or newer. This covers Ubuntu 22.04/24.04 and distributions with an
equivalent or newer glibc baseline; older systems are not claimed as supported.

## Release workflow

Run `.github/workflows/release-desktop.yml` with a stable tag such as `v1.2.3`,
or push that tag. The workflow publishes only the Avalonia desktop client:

- Windows x64 and arm64 ZIP archives;
- macOS x64 and arm64 DMGs containing `Miaomiao.app`;
- Linux x64 and arm64 DEB and RPM packages;
- `SHA256SUMS`, an armored signature for it, and detached GPG signatures for
  every release file, plus the Miaomiao GPG public key.

Configure these repository secrets:

- `MIAOMIAO_RELEASE_GPG_PRIVATE_KEY`
- `MIAOMIAO_RELEASE_GPG_FINGERPRINT`
- `MIAOMIAO_RELEASE_GPG_PASSPHRASE`

This follows the upstream v2rayN release model: detached GPG signatures verify
the downloaded files, but they are not Windows Authenticode signatures or Apple
Developer ID notarization. Windows SmartScreen and macOS Gatekeeper can
therefore show an unrecognized-developer warning on first launch.

Every installed application directory contains `SOURCE-COMMIT`, `SOURCE-URL`,
`LICENSE`, `THIRD_PARTY_NOTICES.md`, the bundled third-party license texts, and
the exact core lock used for that build. Keep the release's GitHub repository
public and available at `SOURCE-URL` to satisfy corresponding source
obligations.
