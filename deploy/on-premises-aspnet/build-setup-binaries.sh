#!/usr/bin/env bash
# Publish Windows setup.exe and copy it into the on-premises + hybrid packs.
# Linux customers use the ecomae-setup script in the same folder.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJ="$ROOT/deploy/on-premises-aspnet/setup/EcomAE.OnPremSetup.csproj"
OUT="${1:-$ROOT/deploy/on-premises-aspnet/.setup-publish}"
mkdir -p "$OUT"
export PATH="${PATH}:/home/ubuntu/.dotnet:/usr/share/dotnet:/root/.dotnet"
dotnet publish "$PROJ" -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:InvariantGlobalization=true \
  -o "$OUT/win"
EXE="$(find "$OUT/win" -name 'EcomAE.OnPremSetup.exe' -o -name 'setup.exe' | head -1)"
if [[ -z "$EXE" || ! -f "$EXE" ]]; then
  echo "setup.exe publish failed" >&2
  exit 1
fi
cp -f "$EXE" "$ROOT/deploy/on-premises-aspnet/setup.exe"
cp -f "$EXE" "$ROOT/deploy/hybrid/setup.exe"
chmod 644 "$ROOT/deploy/on-premises-aspnet/setup.exe" "$ROOT/deploy/hybrid/setup.exe"
ls -l "$ROOT/deploy/on-premises-aspnet/setup.exe" "$ROOT/deploy/hybrid/setup.exe"
