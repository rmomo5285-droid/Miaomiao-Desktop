#!/usr/bin/env bash

MIAOMIAO_VERSION_ARG=""
MIAOMIAO_ARCH_OVERRIDE=""
MIAOMIAO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
MIAOMIAO_PROJECT="$MIAOMIAO_ROOT/v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj"
MIAOMIAO_PROJECT_DIR="$(dirname "$MIAOMIAO_PROJECT")"
MIAOMIAO_HELPER_PROJECT="$MIAOMIAO_ROOT/v2rayN/AmazTool/AmazTool.csproj"
MIAOMIAO_VERSION=""
MIAOMIAO_COMMIT=""
MIAOMIAO_SOURCE_REPOSITORY="${GITHUB_SERVER_URL:-https://github.com}/${GITHUB_REPOSITORY:-rmomo5285-droid/Miaomiao-Desktop}"

# shellcheck source=pinned-core-bundle.sh
source "$MIAOMIAO_ROOT/packaging/pinned-core-bundle.sh"

miaomiao_linux_die() {
  echo "[Miaomiao packaging] $*" >&2
  exit 1
}

miaomiao_parse_linux_args() {
  local first="${1:-}"

  if [[ -n "$first" && "$first" != --* ]]; then
    MIAOMIAO_VERSION_ARG="$first"
    shift || true
  fi

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --arch)
        [[ $# -ge 2 ]] || miaomiao_linux_die "--arch requires a value."
        MIAOMIAO_ARCH_OVERRIDE="$2"
        shift 2
        ;;
      *)
        miaomiao_linux_die "Unsupported argument '$1'. Runtime assets are selected only through packaging/core-bundles.lock.tsv."
        ;;
    esac
  done
}

miaomiao_prepare_checkout() {
  local expected=""

  cd "$MIAOMIAO_ROOT"
  [[ -f "$MIAOMIAO_PROJECT" ]] || miaomiao_linux_die "Desktop project not found: $MIAOMIAO_PROJECT"
  git rev-parse --git-dir >/dev/null 2>&1 || miaomiao_linux_die "Packaging must run from a Git checkout."

  MIAOMIAO_COMMIT="$(git rev-parse HEAD)"
  expected="${GITHUB_SHA:-}"
  if [[ -n "$expected" && "$MIAOMIAO_COMMIT" != "$expected" ]]; then
    miaomiao_linux_die "Checkout mismatch: GITHUB_SHA=$expected, HEAD=$MIAOMIAO_COMMIT."
  fi

  if [[ -n "$MIAOMIAO_VERSION_ARG" ]]; then
    MIAOMIAO_VERSION="${MIAOMIAO_VERSION_ARG#v}"
  else
    miaomiao_linux_die "A stable release version such as v1.2.3 is required."
  fi

  [[ "$MIAOMIAO_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || \
    miaomiao_linux_die "Invalid package version '$MIAOMIAO_VERSION'. Use a stable value such as v1.2.3."

  echo "[Miaomiao packaging] Building current checkout $MIAOMIAO_COMMIT as $MIAOMIAO_VERSION."
}

miaomiao_host_arch() {
  uname -m
}

miaomiao_validate_target_host() {
  local target_set="$1"
  local host
  host="$(miaomiao_host_arch)"

  case "$target_set:$host" in
    standard:x86_64|standard:aarch64|riscv64:riscv64|loongarch64:loongarch64) ;;
    *) miaomiao_linux_die "Target set '$target_set' is not supported on host '$host'." ;;
  esac
}

miaomiao_select_targets() {
  local target_set="$1"
  local host
  host="$(miaomiao_host_arch)"

  case "$target_set" in
    riscv64) printf '%s\n' riscv64 ;;
    loongarch64) printf '%s\n' loongarch64 ;;
    standard)
      case "$MIAOMIAO_ARCH_OVERRIDE" in
        all) printf '%s\n' x64 arm64 ;;
        x64|amd64) printf '%s\n' x64 ;;
        arm64|aarch64) printf '%s\n' arm64 ;;
        "")
          case "$host" in
            x86_64) printf '%s\n' x64 ;;
            aarch64) printf '%s\n' arm64 ;;
          esac
          ;;
        *) miaomiao_linux_die "Unknown architecture '$MIAOMIAO_ARCH_OVERRIDE'. Use x64, arm64, or all." ;;
      esac
      ;;
    *) miaomiao_linux_die "Unknown target set '$target_set'." ;;
  esac
}

miaomiao_target_metadata() {
  case "$1" in
    x64) printf '%s\n%s\n%s\n' linux-x64 amd64 x86_64 ;;
    arm64) printf '%s\n%s\n%s\n' linux-arm64 arm64 aarch64 ;;
    riscv64) printf '%s\n%s\n%s\n' linux-riscv64 riscv64 riscv64 ;;
    loongarch64) printf '%s\n%s\n%s\n' linux-loongarch64 loong64 loongarch64 ;;
    *) return 1 ;;
  esac
}

miaomiao_require_dotnet() {
  local sdk_version
  command -v dotnet >/dev/null 2>&1 || \
    miaomiao_linux_die ".NET SDK is missing. CI must install the pinned SDK before packaging."
  sdk_version="$(dotnet --version)"
  [[ "$sdk_version" == "10.0.100" ]] || \
    miaomiao_linux_die "Expected .NET SDK 10.0.100, got $sdk_version."
}

miaomiao_publish() {
  local rid="$1"
  MIAOMIAO_PUBLISH_DIR="$MIAOMIAO_PROJECT_DIR/bin/Release/net10.0/$rid/publish"

  rm -rf "$MIAOMIAO_PUBLISH_DIR"
  dotnet restore "$MIAOMIAO_PROJECT" -r "$rid"
  dotnet publish "$MIAOMIAO_PROJECT" -c Release -r "$rid" \
    -p:PublishSingleFile=false -p:SelfContained=true \
    -p:Version="$MIAOMIAO_VERSION" -o "$MIAOMIAO_PUBLISH_DIR"
  [[ -f "$MIAOMIAO_PUBLISH_DIR/Miaomiao" ]] || \
    miaomiao_linux_die "Published executable is missing: $MIAOMIAO_PUBLISH_DIR/Miaomiao"
  dotnet publish "$MIAOMIAO_HELPER_PROJECT" -c Release -r "$rid" \
    -p:SelfContained=true -p:PublishTrimmed=true \
    -p:Version="$MIAOMIAO_VERSION" -p:DebugType=None -p:DebugSymbols=false \
    -o "$MIAOMIAO_PUBLISH_DIR"
  [[ -f "$MIAOMIAO_PUBLISH_DIR/MiaomiaoHelper" ]] || \
    miaomiao_linux_die "Published helper is missing: $MIAOMIAO_PUBLISH_DIR/MiaomiaoHelper"
  chmod 0755 "$MIAOMIAO_PUBLISH_DIR/Miaomiao" "$MIAOMIAO_PUBLISH_DIR/MiaomiaoHelper"
  install -m 0644 "$MIAOMIAO_ROOT/LICENSE" "$MIAOMIAO_PUBLISH_DIR/LICENSE"
  install -m 0644 "$MIAOMIAO_ROOT/packaging/core-bundles.lock.tsv" \
    "$MIAOMIAO_PUBLISH_DIR/CORE-BUNDLES.lock.tsv"
  install -m 0644 "$MIAOMIAO_ROOT/THIRD_PARTY_NOTICES.md" \
    "$MIAOMIAO_PUBLISH_DIR/THIRD_PARTY_NOTICES.md"
  cp -a "$MIAOMIAO_ROOT/packaging/licenses" "$MIAOMIAO_PUBLISH_DIR/licenses"
  printf '%s\n' "$MIAOMIAO_COMMIT" > "$MIAOMIAO_PUBLISH_DIR/SOURCE-COMMIT"
  printf '%s/tree/%s\n' "$MIAOMIAO_SOURCE_REPOSITORY" "$MIAOMIAO_COMMIT" \
    > "$MIAOMIAO_PUBLISH_DIR/SOURCE-URL"
}

miaomiao_copy_license() {
  local destination="$1"
  [[ -f "$MIAOMIAO_ROOT/LICENSE" ]] || miaomiao_linux_die "Repository LICENSE is missing."
  install -m 0644 "$MIAOMIAO_ROOT/LICENSE" "$destination/LICENSE"
  install -m 0644 "$MIAOMIAO_ROOT/THIRD_PARTY_NOTICES.md" "$destination/THIRD_PARTY_NOTICES.md"
  cp -a "$MIAOMIAO_ROOT/packaging/licenses" "$destination/licenses"
}
