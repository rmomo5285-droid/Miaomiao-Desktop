#!/usr/bin/env bash
set -euo pipefail

TARGET_SET="${1:?target set is required}"
shift
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=linux-build-common.sh
source "$SCRIPT_DIR/linux-build-common.sh"

RPM_TOPDIR="${HOME}/rpmbuild"
PKGROOT="miaomiao-publish"
declare -a BUILT_PACKAGES=()

run_as_root() {
  if [[ "$(id -u)" -eq 0 ]]; then
    "$@"
  else
    command -v sudo >/dev/null 2>&1 || miaomiao_linux_die "sudo is required outside a root container."
    sudo "$@"
  fi
}

install_dependencies() {
  if command -v dnf >/dev/null 2>&1; then
    run_as_root dnf -y install rpm-build rpmdevtools curl unzip tar rsync git file ca-certificates
    if [[ "$TARGET_SET" != standard ]]; then
      run_as_root dnf -y install glibc-devel kernel-headers libatomic libicu
    fi
  elif command -v apt-get >/dev/null 2>&1; then
    [[ "$TARGET_SET" == standard ]] || \
      miaomiao_linux_die "Non-standard RPM architectures require a native RPM build host."
    run_as_root apt-get update
    run_as_root apt-get -y install rpm curl unzip tar rsync git file ca-certificates
  else
    miaomiao_linux_die "dnf or apt-get is required."
  fi
  miaomiao_require_dotnet
}

write_spec() {
  local specfile="$1" rpm_arch="$2"
  cat > "$specfile" <<'SPEC'
%global debug_package %{nil}
%undefine _debuginfo_subpackages
%undefine _debugsource_packages
%global __requires_exclude ^liblttng-ust\.so\..*$

Name:           miaomiao
Version:        __VERSION__
Release:        1%{?dist}
Summary:        Miaomiao desktop network client
License:        GPL-3.0-only
URL:            https://github.com/rmomo5285-droid/Miaomiao-Desktop
BugURL:         https://github.com/rmomo5285-droid/Miaomiao-Desktop/issues
ExclusiveArch:  __ARCH__
Source0:        __PKGROOT__.tar.gz

Requires:       cairo, pango, openssl, mesa-libEGL, mesa-libGL
Requires:       glibc >= 2.35
Requires:       fontconfig, desktop-file-utils, xdg-utils, coreutils, bash, freetype

%description
Miaomiao is a cross-platform desktop client with Xray and sing-box runtime support.

%prep
%setup -q -n __PKGROOT__

%build

%install
install -dm0755 %{buildroot}/opt/miaomiao
cp -a app/. %{buildroot}/opt/miaomiao/
chmod 0755 %{buildroot}/opt/miaomiao/Miaomiao

install -dm0755 %{buildroot}%{_bindir}
install -m0755 /dev/stdin %{buildroot}%{_bindir}/miaomiao <<'EOF'
#!/usr/bin/bash
set -euo pipefail
cd /opt/miaomiao
exec /opt/miaomiao/Miaomiao "$@"
EOF

install -dm0755 %{buildroot}%{_datadir}/applications
install -m0644 /dev/stdin %{buildroot}%{_datadir}/applications/miaomiao.desktop <<'EOF'
[Desktop Entry]
Type=Application
Name=Miaomiao
Comment=Miaomiao secure network client
Exec=miaomiao
Icon=miaomiao
Terminal=false
Categories=Network;
EOF

install -dm0755 %{buildroot}%{_datadir}/icons/hicolor/256x256/apps
install -m0644 miaomiao.png %{buildroot}%{_datadir}/icons/hicolor/256x256/apps/miaomiao.png
install -dm0755 %{buildroot}%{_datadir}/licenses/miaomiao
install -m0644 LICENSE %{buildroot}%{_datadir}/licenses/miaomiao/LICENSE
install -m0644 THIRD_PARTY_NOTICES.md %{buildroot}%{_datadir}/licenses/miaomiao/THIRD_PARTY_NOTICES.md
cp -a licenses %{buildroot}%{_datadir}/licenses/miaomiao/licenses

%post
/usr/bin/update-desktop-database %{_datadir}/applications >/dev/null 2>&1 || true
/usr/bin/gtk-update-icon-cache -f %{_datadir}/icons/hicolor >/dev/null 2>&1 || true

%postun
/usr/bin/update-desktop-database %{_datadir}/applications >/dev/null 2>&1 || true
/usr/bin/gtk-update-icon-cache -f %{_datadir}/icons/hicolor >/dev/null 2>&1 || true

%files
%{_bindir}/miaomiao
/opt/miaomiao
%{_datadir}/applications/miaomiao.desktop
%{_datadir}/icons/hicolor/256x256/apps/miaomiao.png
%{_datadir}/licenses/miaomiao
SPEC
  sed -i "s/__VERSION__/${MIAOMIAO_VERSION}/g; s/__ARCH__/${rpm_arch}/g; s/__PKGROOT__/${PKGROOT}/g" "$specfile"
}

package_target() {
  local short="$1" rid="$2" rpm_arch="$3"
  local publish_dir workdir specfile source_dir icon_candidate file
  miaomiao_publish "$rid"
  publish_dir="$MIAOMIAO_PUBLISH_DIR"
  workdir="$(mktemp -d)"
  mkdir -p "$workdir/$PKGROOT/app"
  cp -a "$publish_dir/." "$workdir/$PKGROOT/app/"
  miaomiao_stage_pinned_core_bundle "$rid" "$workdir/$PKGROOT/app"
  miaomiao_copy_license "$workdir/$PKGROOT"
  icon_candidate="$MIAOMIAO_PROJECT_DIR/v2rayN.png"
  [[ -f "$icon_candidate" ]] || miaomiao_linux_die "Application icon is missing: $icon_candidate"
  install -m 0644 "$icon_candidate" "$workdir/$PKGROOT/miaomiao.png"

  mkdir -p "$RPM_TOPDIR/BUILD" "$RPM_TOPDIR/BUILDROOT" "$RPM_TOPDIR/RPMS" \
    "$RPM_TOPDIR/SOURCES" "$RPM_TOPDIR/SPECS" "$RPM_TOPDIR/SRPMS"
  source_dir="$RPM_TOPDIR/SOURCES"
  specfile="$RPM_TOPDIR/SPECS/miaomiao.spec"
  mkdir -p "$source_dir" "$RPM_TOPDIR/SPECS"
  tar -C "$workdir" -czf "$source_dir/$PKGROOT.tar.gz" "$PKGROOT"
  write_spec "$specfile" "$rpm_arch"
  rpmbuild -ba "$specfile" --target "$rpm_arch"

  for file in "$RPM_TOPDIR/RPMS/$rpm_arch/miaomiao-${MIAOMIAO_VERSION}-1"*.rpm; do
    [[ -e "$file" ]] || continue
    BUILT_PACKAGES+=("$file")
    echo "[Miaomiao packaging] Built $short package: $file"
  done
  rm -rf "$workdir"
}

main() {
  local short rid deb_arch rpm_arch
  local -a targets=() metadata=()
  miaomiao_parse_linux_args "$@"
  miaomiao_validate_target_host "$TARGET_SET"
  miaomiao_prepare_checkout
  install_dependencies
  mapfile -t targets < <(miaomiao_select_targets "$TARGET_SET")
  for short in "${targets[@]}"; do
    mapfile -t metadata < <(miaomiao_target_metadata "$short")
    rid="${metadata[0]}"; deb_arch="${metadata[1]}"; rpm_arch="${metadata[2]}"
    package_target "$short" "$rid" "$rpm_arch"
  done
  [[ "${#BUILT_PACKAGES[@]}" -gt 0 ]] || miaomiao_linux_die "No RPM package was produced."
  printf '[Miaomiao packaging] Output: %s\n' "${BUILT_PACKAGES[@]}"
}

main "$@"
