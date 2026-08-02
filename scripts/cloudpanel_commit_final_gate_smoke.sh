#!/usr/bin/env bash
# On CloudPanel after capture: commit only real staging-smoke / parity artifacts and push a PR branch.
# Never invents JSON. Never writes RELEASE_OWNER_APPROVAL.md. Never removes PHP.
set -euo pipefail

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
BRANCH="${ECOMAE_SMOKE_BRANCH:-cursor/final-gate-staging-smoke-7b3b}"

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
  printf 'ERROR: ecomae repo not found. Run cloudpanel_find_and_redeploy.sh first.\n' >&2
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
    printf 'MISSING %s\n' "$f"
    missing=1
  else
    printf 'FOUND   %s (%s bytes)\n' "$f" "$(wc -c <"$f" | tr -d ' ')"
  fi
done

if [[ "$missing" -ne 0 ]]; then
  printf '\nRefuse to commit: run capture with real keys/cookies first:\n' >&2
  printf '  source /etc/ecomae-aspnet/platform.env\n' >&2
  printf '  bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n' >&2
  exit 1
fi

# Soft validate surface smoke ok + authenticated 200 digests without inventing data.
python3 - "$SMOKE/surface-digests-aspnet.json" <<'PY'
import json, sys
path = sys.argv[1]
with open(path, encoding="utf-8") as fh:
    doc = json.load(fh)
if not doc.get("ok") is True:
    raise SystemExit(f"FAIL {path}: ok must be true")
routes = doc.get("routes") or []
digest_200 = [
    r for r in routes
    if isinstance(r, dict)
    and int(r.get("status") or 0) == 200
    and not str(r.get("route") or "").startswith("/migration/")
]
if not digest_200:
    raise SystemExit(f"FAIL {path}: need at least one non-migration digest HTTP 200")
print(f"OK surface smoke: {len(digest_200)} authenticated digest 200 route(s)")
PY

git fetch origin main
git checkout -B "$BRANCH" origin/main
git add \
  docs/migration/evidence/decommission/staging-smoke \
  docs/migration/evidence/decommission/public-probes \
  docs/migration/evidence/decommission/parity-samples

if git diff --cached --quiet; then
  printf 'Nothing new to commit under decommission evidence.\n'
  exit 0
fi

git -c user.name="${GIT_AUTHOR_NAME:-ecomae-cloudpanel}" \
    -c user.email="${GIT_AUTHOR_EMAIL:-ops@ecomae.local}" \
    commit -m "$(cat <<'EOF'
Attach CloudPanel final-gate staging smoke artifacts

Authenticated price/catalog/surface smoke only. PHP remains authoritative.
EOF
)"

git push -u origin "$BRANCH"
printf '\nPushed branch %s. Open a PR into main, then add RELEASE_OWNER_APPROVAL.md only after human approval.\n' "$BRANCH"
printf 'Do NOT remove PHP from this script.\n'
