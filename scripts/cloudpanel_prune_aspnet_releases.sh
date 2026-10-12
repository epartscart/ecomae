#!/usr/bin/env bash
# Keep the newest N ASP.NET publish trees. Never touch PHP, MySQL, or current.
# Safe to re-run. Does not invent a send. Does not enable nginx cutover.
set -euo pipefail

ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
KEEP="${ECOMAE_KEEP_RELEASES:-3}"
CURRENT="$(readlink -f "$ROOT/current" 2>/dev/null || true)"

[[ -n "$CURRENT" && -d "$CURRENT" ]] || { echo "RESULT=FAIL no_current"; exit 1; }
[[ "$KEEP" =~ ^[0-9]+$ ]] && [[ "$KEEP" -ge 2 ]] || { echo "RESULT=FAIL keep_must_be_ge_2"; exit 1; }

mapfile -t ALL < <(ls -1dt "$ROOT/releases/"* 2>/dev/null)
echo "current=$CURRENT keep=$KEEP total=${#ALL[@]}"

kept=0
removed=0
for dir in "${ALL[@]}"; do
  [[ -d "$dir" ]] || continue
  real="$(readlink -f "$dir")"
  if [[ "$real" == "$CURRENT" ]]; then
    echo "KEEP current $dir"
    kept=$((kept + 1))
    continue
  fi
  if [[ "$kept" -lt "$KEEP" ]]; then
    echo "KEEP $dir"
    kept=$((kept + 1))
    continue
  fi
  echo "REMOVE $dir"
  rm -rf --one-file-system "$dir"
  removed=$((removed + 1))
done

echo "RESULT=PASS PRUNE_ASPNET_RELEASES kept=$kept removed=$removed"
