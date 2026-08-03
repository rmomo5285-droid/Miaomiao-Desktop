#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
source_png="$repo_root/branding/orange-icon.png"
source_ico="$repo_root/branding/orange-icon.ico"
desktop="$repo_root/v2rayN/v2rayN.Desktop"
assets="$desktop/Assets"
work=$(mktemp -d)
trap 'rm -rf -- "$work"' EXIT

command -v convert >/dev/null
command -v png2icns >/dev/null
test -f "$source_png"
test -f "$source_ico"

convert "$source_png" -resize 256x256 -strip "$desktop/v2rayN.png"
cp "$source_ico" "$assets/v2rayN.ico"

badge_colors=('#D14343' '#2F9E5B' '#767C85' '#3774BD')
for i in 1 2 3 4; do
  color=${badge_colors[$((i - 1))]}
  layers=()
  for size in 16 24 32 48 64 128 256; do
    layer="$work/notify-${i}-${size}.png"
    badge=$((size * 15 / 64))
    inset=$((size * 3 / 64))
    convert -size "${size}x${size}" xc:none \
      \( "$source_png" -resize "$((size * 88 / 100))x$((size * 88 / 100))" \) \
      -gravity center -composite \
      -fill "$color" -stroke white -strokewidth "$((size / 32 + 1))" \
      -draw "circle $((size - inset - badge)),$((size - inset - badge)) $((size - inset)),$((size - inset - badge))" \
      -strip "$layer"
    layers+=("$layer")
  done
  convert "${layers[@]}" "$assets/NotifyIcon${i}.ico"
done

icns_layers=()
for size in 16 32 48 128 256 512; do
  layer="$work/orange-${size}.png"
  convert "$source_png" -resize "${size}x${size}" -strip "$layer"
  icns_layers+=("$layer")
done
png2icns "$desktop/v2rayN.icns" "${icns_layers[@]}"
