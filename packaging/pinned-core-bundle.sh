#!/usr/bin/env bash

miaomiao_die() {
  echo "[Miaomiao packaging] $*" >&2
  return 1
}

miaomiao_repo_root() {
  local helper_dir
  helper_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
  cd "$helper_dir/.." && pwd
}

miaomiao_stage_pinned_core_bundle() {
  local target="$1"
  local output_root="$2"
  local repo_root lock_file row locked_target url expected_sha actual_sha temp_dir bundle_root entry
  local -a bundle_roots=()

  [[ -n "$output_root" && "$output_root" != "/" ]] || miaomiao_die "Refusing unsafe core output root." || return 1
  repo_root="$(miaomiao_repo_root)" || return 1
  lock_file="$repo_root/packaging/core-bundles.lock.tsv"
  [[ -f "$lock_file" ]] || miaomiao_die "Missing core lock file: $lock_file" || return 1

  row="$(awk -F '\t' -v wanted="$target" 'NR > 1 && $1 == wanted { print; exit }' "$lock_file")"
  [[ -n "$row" ]] || miaomiao_die "No core bundle lock entry for target '$target'." || return 1

  IFS=$'\t' read -r locked_target url expected_sha <<< "$row"
  [[ "$locked_target" == "$target" ]] || miaomiao_die "Core bundle lock entry does not match target '$target'." || return 1
  if [[ ! "$url" =~ ^https:// ]] || [[ ! "$expected_sha" =~ ^[0-9A-Fa-f]{64}$ ]]; then
    miaomiao_die "Core bundle '$target' is not pinned. Add an immutable HTTPS URL and SHA-256 to packaging/core-bundles.lock.tsv before releasing." || return 1
  fi
  case "$url" in
    *'/latest/'*|*'/refs/heads/'*|*'/heads/'*)
      miaomiao_die "Core bundle '$target' uses a mutable URL: $url" || return 1
      ;;
  esac

  command -v curl >/dev/null 2>&1 || miaomiao_die "curl is required." || return 1
  command -v unzip >/dev/null 2>&1 || miaomiao_die "unzip is required." || return 1

  temp_dir="$(mktemp -d)" || return 1
  if ! curl --fail --location --proto '=https' --tlsv1.2 --retry 3 --retry-all-errors \
    "$url" -o "$temp_dir/core-bundle.zip"; then
    rm -rf "$temp_dir"
    miaomiao_die "Failed to download pinned core bundle for '$target'."
    return 1
  fi

  if command -v sha256sum >/dev/null 2>&1; then
    actual_sha="$(sha256sum "$temp_dir/core-bundle.zip" | awk '{print $1}')"
  elif command -v shasum >/dev/null 2>&1; then
    actual_sha="$(shasum -a 256 "$temp_dir/core-bundle.zip" | awk '{print $1}')"
  else
    rm -rf "$temp_dir"
    miaomiao_die "No SHA-256 utility is available."
    return 1
  fi

  actual_sha="$(printf '%s' "$actual_sha" | tr '[:upper:]' '[:lower:]')"
  expected_sha="$(printf '%s' "$expected_sha" | tr '[:upper:]' '[:lower:]')"
  if [[ "$actual_sha" != "$expected_sha" ]]; then
    rm -rf "$temp_dir"
    miaomiao_die "SHA-256 mismatch for '$target': expected $expected_sha, got $actual_sha."
    return 1
  fi

  mkdir -p "$temp_dir/unpacked"
  while IFS= read -r entry; do
    case "$entry" in
      /*|../*|*/../*|*\\*)
        rm -rf "$temp_dir"
        miaomiao_die "Pinned core bundle for '$target' contains an unsafe path: $entry"
        return 1
        ;;
    esac
  done < <(unzip -Z1 "$temp_dir/core-bundle.zip")

  if ! unzip -q "$temp_dir/core-bundle.zip" -d "$temp_dir/unpacked"; then
    rm -rf "$temp_dir"
    miaomiao_die "Pinned core bundle for '$target' is not a valid ZIP archive."
    return 1
  fi

  if [[ -d "$temp_dir/unpacked/bin" ]]; then
    bundle_root="$temp_dir/unpacked"
  else
    while IFS= read -r entry; do
      bundle_roots+=("$entry")
    done < <(find "$temp_dir/unpacked" -mindepth 2 -maxdepth 2 -type d -name bin -print)
    if [[ "${#bundle_roots[@]}" -eq 1 ]]; then
      bundle_root="$(dirname "${bundle_roots[0]}")"
    fi
  fi

  if [[ -z "${bundle_root:-}" || ! -d "$bundle_root/bin" ]]; then
    rm -rf "$temp_dir"
    miaomiao_die "Pinned core bundle for '$target' must contain exactly one bin/ directory."
    return 1
  fi

  rm -rf "${output_root:?}/bin"
  mkdir -p "$output_root/bin/xray" "$output_root/bin/sing_box" "$output_root/bin/srss"

  case "$target" in
    windows-*)
      cp -a "$bundle_root/bin/xray/xray.exe" "$output_root/bin/xray/xray.exe"
      cp -a "$bundle_root/bin/xray/wintun.dll" "$output_root/bin/xray/wintun.dll"
      cp -a "$bundle_root/bin/sing_box/sing-box.exe" "$output_root/bin/sing_box/sing-box.exe"
      ;;
    *)
      cp -a "$bundle_root/bin/xray/xray" "$output_root/bin/xray/xray"
      cp -a "$bundle_root/bin/sing_box/sing-box" "$output_root/bin/sing_box/sing-box"
      ;;
  esac
  cp -a "$bundle_root/bin/geoip.dat" "$output_root/bin/geoip.dat"
  cp -a "$bundle_root/bin/geosite.dat" "$output_root/bin/geosite.dat"
  cp -a "$bundle_root/bin/srss/." "$output_root/bin/srss/"

  if [[ ! -f "$output_root/bin/sing_box/sing-box" \
    && ! -f "$output_root/bin/sing_box/sing-box.exe" ]]; then
    rm -rf "$temp_dir"
    miaomiao_die "Pinned core bundle for '$target' does not contain bin/sing_box/sing-box."
    return 1
  fi
  if [[ ! -f "$output_root/bin/xray/xray" \
    && ! -f "$output_root/bin/xray/xray.exe" ]]; then
    rm -rf "$temp_dir"
    miaomiao_die "Pinned core bundle for '$target' does not contain bin/xray/xray."
    return 1
  fi
  if find "$output_root/bin" -type f \( -iname '*mihomo*' -o -iname '*libcronet*' -o -iname '*enableloopback*' \) | grep -q .; then
    rm -rf "$temp_dir"
    miaomiao_die "The staged core bundle contains a non-allowlisted executable."
    return 1
  fi
  rm -rf "$temp_dir"
  echo "[Miaomiao packaging] Staged verified core bundle for $target."
}
