#!/usr/bin/env bash
# Pack real CloudPanel final-gate evidence (smoke + probes + parity samples) for offline push.
# Use when `git push` fails auth on the server. Never invents JSON. Never removes PHP.
set -euo pipefail

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
OUT_DIR="${ECOMAE_SMOKE_BUNDLE_DIR:-/tmp}"
STAMP="$(date -u +%Y%m%d%H%M%S)"

find_repo() {
  local candidate
  for candidate in "${CANDIDATES[@]}"; do
    if [[ -n "$candidate" && -d "$candidate/.git" && -f "$candidate/scripts/cloudpanel_capture_final_gate_artifacts.sh" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  return 1
}

REPO="$(find_repo || true)"
if [[ -z "$REPO" ]]; then
  printf 'ERROR: ecomae repo not found.\n' >&2
  exit 1
fi
cd "$REPO"

SMOKE="$REPO/docs/migration/evidence/decommission/staging-smoke"
required=(
  "$SMOKE/price-lookup-aspnet.json"
  "$SMOKE/catalog-status-aspnet.json"
  "$SMOKE/surface-digests-aspnet.json"
)
missing=0
for f in "${required[@]}"; do
  if [[ ! -s "$f" ]]; then
    printf 'MISSING %s\n' "$f" >&2
    missing=1
  else
    printf 'FOUND   %s (%s bytes)\n' "$f" "$(wc -c <"$f" | tr -d ' ')"
  fi
done
if [[ "$missing" -ne 0 ]]; then
  printf 'Refuse to export: capture smoke artifacts first.\n' >&2
  exit 1
fi

BUNDLE_ROOT="$OUT_DIR/ecomae-final-gate-smoke-${STAMP}"
mkdir -p "$BUNDLE_ROOT"
# Copy only decommission evidence paths (no secrets / no platform.env).
mkdir -p "$BUNDLE_ROOT/docs/migration/evidence/decommission"
for sub in staging-smoke public-probes parity-samples; do
  src="$REPO/docs/migration/evidence/decommission/$sub"
  if [[ -d "$src" ]]; then
    cp -a "$src" "$BUNDLE_ROOT/docs/migration/evidence/decommission/"
  fi
done

# If an unpushed smoke commit exists, also emit a single-commit patch + bundle.
BRANCH="${ECOMAE_SMOKE_BRANCH:-cursor/final-gate-staging-smoke-7b3b}"
if git rev-parse --verify "$BRANCH" >/dev/null 2>&1; then
  git format-patch -1 "$BRANCH" -o "$BUNDLE_ROOT" >/dev/null 2>&1 || true
  if git rev-parse --verify origin/main >/dev/null 2>&1; then
    git bundle create "$BUNDLE_ROOT/final-gate-staging-smoke.bundle" "origin/main..$BRANCH" 2>/dev/null || true
  fi
  printf '%s\n' "$(git rev-parse "$BRANCH")" >"$BUNDLE_ROOT/BRANCH_TIP_SHA.txt"
  printf '%s\n' "$BRANCH" >"$BUNDLE_ROOT/BRANCH_NAME.txt"
fi

ARCHIVE="${BUNDLE_ROOT}.tar.gz"
tar -C "$(dirname "$BUNDLE_ROOT")" -czf "$ARCHIVE" "$(basename "$BUNDLE_ROOT")"
printf '\nOK exported %s\n' "$ARCHIVE"
printf 'Contents: staging-smoke + public-probes + parity-samples (no secrets).\n'
printf 'On a machine with GitHub auth:\n'
printf '  tar -xzf %s\n' "$ARCHIVE"
printf '  # Option A — apply files onto a PR branch from main:\n'
printf '  git fetch origin main && git checkout -B %s origin/main\n' "$BRANCH"
printf '  cp -a %s/docs/migration/evidence/decommission/* docs/migration/evidence/decommission/\n' "$(basename "$BUNDLE_ROOT")"
printf '  git add docs/migration/evidence/decommission && git commit -m "Attach CloudPanel final-gate staging smoke artifacts" && git push -u origin %s\n' "$BRANCH"
printf '  # Option B — if bundle present:\n'
printf '  git fetch %s/final-gate-staging-smoke.bundle %s:%s && git push -u origin %s\n' \
  "$(basename "$BUNDLE_ROOT")" "$BRANCH" "$BRANCH" "$BRANCH"
printf 'Do NOT remove PHP. Do NOT invent RELEASE_OWNER_APPROVAL.md.\n'
