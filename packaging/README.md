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
version, builds the checked-out `GITHUB_SHA`, stages the locked core, signs the
platform bundle, and only then creates release archives. Do not use these
scripts to publish locally built binaries.

Linux releases are built inside an Ubuntu 22.04 container and require glibc
2.35 or newer. This covers Ubuntu 22.04/24.04 and distributions with an
equivalent or newer glibc baseline; older systems are not claimed as supported.

## Release workflow

Run `.github/workflows/release-desktop.yml` with a stable tag such as `v1.2.3`,
or push that tag. The workflow publishes only the Avalonia desktop client:

- Windows x64 and arm64 ZIP archives with Authenticode-signed `Miaomiao.exe`
  and `MiaomiaoHelper.exe`;
- notarized and stapled macOS x64 and arm64 DMGs containing `Miaomiao.app`;
- Linux x64 and arm64 DEB and RPM packages;
- `SHA256SUMS`, an armored signature for it, and detached GPG signatures for
  every release file.

There is no unsigned release fallback. Configure these repository secrets:

- `MIAOMIAO_WINDOWS_CERT_PFX_BASE64`
- `MIAOMIAO_WINDOWS_CERT_PASSWORD`
- `MIAOMIAO_WINDOWS_CERT_SHA256`
- `MIAOMIAO_MACOS_CERT_P12_BASE64`
- `MIAOMIAO_MACOS_CERT_PASSWORD`
- `MIAOMIAO_MACOS_SIGN_IDENTITY`
- `MIAOMIAO_MACOS_NOTARY_APPLE_ID`
- `MIAOMIAO_MACOS_NOTARY_TEAM_ID`
- `MIAOMIAO_MACOS_NOTARY_APP_PASSWORD`
- `MIAOMIAO_RELEASE_GPG_PRIVATE_KEY`
- `MIAOMIAO_RELEASE_GPG_FINGERPRINT`
- `MIAOMIAO_RELEASE_GPG_PASSPHRASE`

`MIAOMIAO_WINDOWS_CERT_SHA256` is the SHA-256 fingerprint of the end-entity
certificate, not the PFX file. Its certificate subject must contain
`Miaomiao`; this prevents accidentally shipping another publisher's signature.
The macOS identity must be an Apple Developer ID Application certificate that
belongs to the configured notary team.

Every installed application directory contains `SOURCE-COMMIT`, `SOURCE-URL`,
`LICENSE`, `THIRD_PARTY_NOTICES.md`, the bundled third-party license texts, and
the exact core lock used for that build. Keep the release's GitHub repository
public and available at `SOURCE-URL` to satisfy corresponding source
obligations.
