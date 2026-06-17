#!/usr/bin/env bash
# Собирает self-contained single-file exe для Pmx2Vrm CLI.
#
# Использование:
#   ./build.sh
#   ./build.sh --runtime linux-x64 --output dist
#   ./build.sh --framework-dependent      # требует установленный .NET на целевой машине
set -euo pipefail

CONFIGURATION="Release"
RUNTIME="linux-x64"
OUTPUT="publish"
SELF_CONTAINED="true"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration) CONFIGURATION="$2"; shift 2 ;;
    -r|--runtime)       RUNTIME="$2";       shift 2 ;;
    -o|--output)        OUTPUT="$2";        shift 2 ;;
    --framework-dependent) SELF_CONTAINED="false"; shift ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT/src/Pmx2Vrm.Cli/Pmx2Vrm.Cli.csproj"
OUT_DIR="$ROOT/$OUTPUT"

echo "Publishing Pmx2Vrm.Cli ($CONFIGURATION / $RUNTIME / self-contained=$SELF_CONTAINED)..."

dotnet publish "$PROJECT" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained "$SELF_CONTAINED" \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$OUT_DIR"

EXE="$(find "$OUT_DIR" -maxdepth 1 -type f -name 'Pmx2Vrm.Cli*' ! -name '*.pdb' | head -n 1)"
echo ""
echo "Built: $EXE"
