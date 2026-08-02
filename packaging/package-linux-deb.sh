#!/usr/bin/env bash
set -euo pipefail

TARGET_SET="${1:?target set is required}"
shift
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=linux-build-common.sh
source "$SCRIPT_DIR/linux-build-common.sh"

OUTPUT_DIR="${HOME}/debbuild"
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
  export DEBIAN_FRONTEND=noninteractive
  command -v apt-get >/dev/null 2>&1 || miaomiao_linux_die "apt-get is required."
  run_as_root apt-get update
  run_as_root apt-get -y install \
    curl unzip tar rsync ca-certificates git dpkg-dev fakeroot file \
    desktop-file-utils xdg-utils gcc make pkg-config libicu-dev libssl-dev \
    libfontconfig1 libfreetype6 zlib1g

  miaomiao_require_dotnet
  mkdir -p "$OUTPUT_DIR"
}

write_launcher() {
  local stage="$1"
  install -m 0755 /dev/stdin "$stage/usr/bin/miaomiao" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
cd /opt/miaomiao
exec /opt/miaomiao/Miaomiao "$@"
EOF
}

write_desktop_file() {
  local stage="$1"
  install -m 0644 /dev/stdin "$stage/usr/share/applications/miaomiao.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=Miaomiao
Comment=Miaomiao secure network client
Exec=miaomiao
Icon=miaomiao
Terminal=false
Categories=Network;
EOF
}

write_maintainer_scripts() {
  local dir="$1"
  install -m 0755 /dev/stdin "$dir/postinst" <<'EOF'
#!/bin/sh
set -e
update-desktop-database /usr/share/applications >/dev/null 2>&1 || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -f /usr/share/icons/hicolor >/dev/null 2>&1 || true
exit 0
EOF
  install -m 0755 /dev/stdin "$dir/postrm" <<'EOF'
#!/bin/sh
set -e
update-desktop-database /usr/share/applications >/dev/null 2>&1 || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -f /usr/share/icons/hicolor >/dev/null 2>&1 || true
exit 0
EOF
}

package_target() {
  local short="$1" rid="$2" deb_arch="$3"
  local publish_dir workdir stage debian_dir project_icon multiarch
  local shlibs_depends extra_depends final_depends output
  local -a elf_files=()

  miaomiao_publish "$rid"
  publish_dir="$MIAOMIAO_PUBLISH_DIR"
  workdir="$(mktemp -d)"
  stage="$workdir/miaomiao_${MIAOMIAO_VERSION}_${deb_arch}"
  debian_dir="$stage/DEBIAN"
  mkdir -p "$stage/opt/miaomiao" "$stage/usr/bin" "$stage/usr/share/applications" \
    "$stage/usr/share/icons/hicolor/256x256/apps" "$stage/usr/share/doc/miaomiao" "$debian_dir"
  cp -a "$publish_dir/." "$stage/opt/miaomiao/"
  miaomiao_stage_pinned_core_bundle "$rid" "$stage/opt/miaomiao"
  miaomiao_copy_license "$stage/usr/share/doc/miaomiao"

  project_icon="$MIAOMIAO_PROJECT_DIR/v2rayN.png"
  [[ -f "$project_icon" ]] || miaomiao_linux_die "Application icon is missing: $project_icon"
  install -m 0644 "$project_icon" "$stage/usr/share/icons/hicolor/256x256/apps/miaomiao.png"
  write_launcher "$stage"
  write_desktop_file "$stage"
  write_maintainer_scripts "$debian_dir"
  chmod 0755 "$stage/opt/miaomiao/Miaomiao"

  multiarch="$(dpkg-architecture -a"$deb_arch" -qDEB_HOST_MULTIARCH)"
  mapfile -t elf_files < <(find "$stage/opt/miaomiao" -type f \( -name '*.so*' -o -perm -111 \) ! -name 'libcoreclrtraceptprovider.so')
  mkdir -p "$workdir/debian"
  cat > "$workdir/debian/control" <<EOF
Source: miaomiao
Section: net
Priority: optional
Maintainer: Miaomiao Release <noreply@github.com>
Standards-Version: 4.7.0

Package: miaomiao
Architecture: ${deb_arch}
Description: Miaomiao desktop network client
EOF
  : > "$debian_dir/substvars"
  if [[ "${#elf_files[@]}" -gt 0 ]]; then
    (
      cd "$workdir"
      dpkg-shlibdeps -l"$stage/opt/miaomiao" -l"/lib/$multiarch" -l"/usr/lib/$multiarch" \
        -T"$debian_dir/substvars" "${elf_files[@]}"
    ) >/dev/null 2>&1 || true
  fi
  shlibs_depends="$(sed -n 's/^shlibs:Depends=//p' "$debian_dir/substvars" | head -n1 || true)"
  extra_depends="libc6 (>= 2.35), fontconfig, desktop-file-utils, xdg-utils, coreutils, bash, libfreetype6"
  final_depends="$extra_depends"
  [[ -z "$shlibs_depends" ]] || final_depends="$shlibs_depends, $extra_depends"

  cat > "$debian_dir/control" <<EOF
Package: miaomiao
Version: ${MIAOMIAO_VERSION}
Architecture: ${deb_arch}
Maintainer: Miaomiao Release <noreply@github.com>
Homepage: https://github.com/rmomo5285-droid/Miaomiao-Desktop
Section: net
Priority: optional
Depends: ${final_depends}
Description: Miaomiao desktop network client
 A cross-platform desktop client with Xray and sing-box runtime support.
EOF

  output="$OUTPUT_DIR/miaomiao_${MIAOMIAO_VERSION}_${deb_arch}.deb"
  dpkg-deb --root-owner-group --build "$stage" "$output"
  BUILT_PACKAGES+=("$output")
  rm -rf "$workdir"
  echo "[Miaomiao packaging] Built $short package: $output"
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
    package_target "$short" "$rid" "$deb_arch"
  done
  printf '[Miaomiao packaging] Output: %s\n' "${BUILT_PACKAGES[@]}"
}

main "$@"
