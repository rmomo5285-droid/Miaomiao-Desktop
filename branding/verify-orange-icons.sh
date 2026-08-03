#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
branding="$repo_root/branding"
desktop="$repo_root/v2rayN/v2rayN.Desktop"
assets="$desktop/Assets"

expected_png_sha256=b6045af66e2e765643a50ac4871d388a9004e90dea93046696ac742ff8bf2e23
expected_ico_sha256=135f28a8573a980f1fcccfd518e8b847093d477d82b9a542cc1bd92ce4299d48
printf '%s  %s\n' "$expected_png_sha256" "$branding/orange-icon.png" | sha256sum --check --strict
printf '%s  %s\n' "$expected_ico_sha256" "$branding/orange-icon.ico" | sha256sum --check --strict
cmp "$branding/orange-icon.ico" "$assets/v2rayN.ico"
file "$desktop/v2rayN.png" | grep -Fq '256 x 256'
file "$desktop/v2rayN.icns" | grep -Fq 'Mac OS X icon'

for icon in "$assets"/NotifyIcon{1,2,3,4}.ico; do
  file "$icon" | grep -Fq 'MS Windows icon resource'
done

grep -Fq 'Icon="/Assets/v2rayN.ico"' "$desktop/App.axaml"
grep -Fq 'Icon="/Assets/v2rayN.ico"' "$desktop/Views/MainWindow.axaml"
