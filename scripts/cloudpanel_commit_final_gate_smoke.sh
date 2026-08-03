#!/usr/bin/env bash
# On CloudPanel after capture: commit only real staging-smoke / parity artifacts and push a PR branch.
# Never invents JSON. Never writes RELEASE_OWNER_APPROVAL.md. Never removes PHP.
# Preserves an existing unpushed smoke commit (does not reset away a failed push).
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

validate_smoke_files() {
  local root="$1"
  python3 - "$root" <<'PY'
import json, sys
from pathlib import Path
root = Path(sys.argv[1])

price = json.loads((root / "price-lookup-aspnet.json").read_text(encoding="utf-8"))
if isinstance(price.get("error"), dict) and price["error"].get("code") in {
    "missing_api_key", "unauthorized", "invalid_api_key"
}:
    raise SystemExit("FAIL price-lookup-aspnet.json looks unauthenticated")
if price.get("ok") is False:
    raise SystemExit("FAIL price-lookup-aspnet.json ok=false")
print("OK price lookup smoke JSON")

catalog = json.loads((root / "catalog-status-aspnet.json").read_text(encoding="utf-8"))
if catalog.get("ok") is False or isinstance(catalog.get("error"), dict):
    raise SystemExit("FAIL catalog-status-aspnet.json has error envelope")
for key in ("connected", "counts", "source"):
    if key not in catalog:
        raise SystemExit(f"FAIL catalog-status-aspnet.json missing {key}")
print("OK catalog status smoke JSON")

doc = json.loads((root / "surface-digests-aspnet.json").read_text(encoding="utf-8"))
if doc.get("ok") is not True:
    raise SystemExit("FAIL surface-digests-aspnet.json: ok must be true")
routes = doc.get("routes") or []
digest_200 = [
    r for r in routes
    if isinstance(r, dict)
    and int(r.get("status") or 0) == 200
    and not str(r.get("route") or "").startswith("/migration/")
]
if not digest_200:
    raise SystemExit("FAIL surface-digests-aspnet.json: need at least one non-migration digest HTTP 200")
print(f"OK surface smoke: {len(digest_200)} authenticated digest 200 route(s)")

sf = root / "storefront-digests-aspnet.json"
if sf.exists() and sf.stat().st_size > 0:
    sdoc = json.loads(sf.read_text(encoding="utf-8"))
    if sdoc.get("ok") is not True:
        raise SystemExit("FAIL storefront-digests-aspnet.json: ok must be true when present")
    sroutes = sdoc.get("routes") or []
    sf_200 = [
        r for r in sroutes
        if isinstance(r, dict) and int(r.get("status") or 0) == 200
    ]
    if not sf_200:
        raise SystemExit("FAIL storefront-digests-aspnet.json: need at least one digest HTTP 200")
    print(f"OK storefront smoke (optional): {len(sf_200)} customer digest 200 route(s)")
else:
    print("SKIP storefront-digests-aspnet.json (optional — set ECOMAE_CUSTOMER_COOKIE_* to capture)")
PY
}

smoke_files_present() {
  local f
  for f in "${required[@]}"; do
    [[ -s "$f" ]] || return 1
  done
  return 0
}

head_has_smoke() {
  git cat-file -e "HEAD:docs/migration/evidence/decommission/staging-smoke/price-lookup-aspnet.json" 2>/dev/null \
    && git cat-file -e "HEAD:docs/migration/evidence/decommission/staging-smoke/catalog-status-aspnet.json" 2>/dev/null \
    && git cat-file -e "HEAD:docs/migration/evidence/decommission/staging-smoke/surface-digests-aspnet.json" 2>/dev/null
}

print_push_recovery() {
  printf '\nPush failed (GitHub auth). Smoke commit is still local — do NOT reset the branch.\n' >&2
  printf 'Option 1 — auth then push (preferred):\n' >&2
  printf '  # PAT with repo scope, or: gh auth login\n' >&2
  printf '  gh auth login --with-token <<<\"$GH_TOKEN\"   # if using gh\n' >&2
  printf '  gh auth setup-git\n' >&2
  printf '  cd %s && git checkout %s && git push -u origin %s\n' "$REPO" "$BRANCH" "$BRANCH" >&2
  printf 'Option 2 — one-shot HTTPS with PAT (token not stored):\n' >&2
  printf '  cd %s && git push -u \"https://x-access-token:${GH_TOKEN}@github.com/epartscart/ecomae.git\" %s\n' "$REPO" "$BRANCH" >&2
  printf 'Option 3 — export bundle for a machine that already has auth:\n' >&2
  printf '  bash scripts/cloudpanel_export_final_gate_smoke_bundle.sh\n' >&2
  printf 'Do NOT invent RELEASE_OWNER_APPROVAL.md. Do NOT remove PHP.\n' >&2
}

push_branch() {
  if command -v gh >/dev/null 2>&1; then
    gh auth setup-git >/dev/null 2>&1 || true
  fi
  set +e
  git push -u origin "$BRANCH"
  local rc=$?
  set -e
  if [[ "$rc" -ne 0 ]]; then
    print_push_recovery
    if [[ -x "$REPO/scripts/cloudpanel_export_final_gate_smoke_bundle.sh" ]]; then
      bash "$REPO/scripts/cloudpanel_export_final_gate_smoke_bundle.sh" || true
    fi
    return "$rc"
  fi
  printf '\nPushed branch %s. Open a PR into main, then add RELEASE_OWNER_APPROVAL.md only after human approval.\n' "$BRANCH"
  printf 'Do NOT remove PHP from this script.\n'
  return 0
}

# --- Prefer re-push of existing unpushed smoke commit (failed auth recovery) ---
git fetch origin main

if git rev-parse --verify "$BRANCH" >/dev/null 2>&1; then
  git checkout "$BRANCH"
  if head_has_smoke; then
    # Materialize HEAD smoke into worktree for validation helper paths.
    git checkout HEAD -- \
      docs/migration/evidence/decommission/staging-smoke \
      docs/migration/evidence/decommission/public-probes \
      docs/migration/evidence/decommission/parity-samples 2>/dev/null || true
    if smoke_files_present && validate_smoke_files "$SMOKE"; then
      printf 'Found existing smoke commit on %s (%s) — pushing without reset.\n' \
        "$BRANCH" "$(git rev-parse --short HEAD)"
      if ! git merge-base --is-ancestor origin/main HEAD; then
        printf 'WARN: %s is not based on current origin/main; rebasing onto origin/main.\n' "$BRANCH"
        git rebase origin/main
      fi
      push_branch
      exit $?
    fi
  fi
fi

# --- Fresh commit from working-tree capture artifacts ---
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
  printf 'If you already committed and only push failed:\n' >&2
  printf '  git checkout %s && git push -u origin %s\n' "$BRANCH" "$BRANCH" >&2
  printf '  # or: bash scripts/cloudpanel_export_final_gate_smoke_bundle.sh\n' >&2
  exit 1
fi

validate_smoke_files "$SMOKE"

# Preserve capture artifacts across branch reset (checkout -B replaces worktree).
PRESERVE="$(mktemp -d /tmp/ecomae-smoke-preserve.XXXXXX)"
cleanup_preserve() { rm -rf "$PRESERVE"; }
trap cleanup_preserve EXIT
mkdir -p "$PRESERVE/decommission"
for sub in staging-smoke public-probes parity-samples; do
  src="$REPO/docs/migration/evidence/decommission/$sub"
  if [[ -d "$src" ]]; then
    cp -a "$src" "$PRESERVE/decommission/"
  fi
done

git checkout -B "$BRANCH" origin/main
cp -a "$PRESERVE/decommission/." "$REPO/docs/migration/evidence/decommission/"

git add \
  docs/migration/evidence/decommission/staging-smoke \
  docs/migration/evidence/decommission/public-probes \
  docs/migration/evidence/decommission/parity-samples

if git diff --cached --quiet; then
  printf 'Nothing new to commit under decommission evidence.\n'
  if head_has_smoke; then
    push_branch
    exit $?
  fi
  exit 0
fi

storefront_note=""
if [[ -s "$SMOKE/storefront-digests-aspnet.json" ]]; then
  storefront_note=" Includes optional storefront customer digests."
fi

git -c user.name="${GIT_AUTHOR_NAME:-ecomae-cloudpanel}" \
    -c user.email="${GIT_AUTHOR_EMAIL:-ops@ecomae.local}" \
    commit -m "$(cat <<EOF
Attach CloudPanel final-gate staging smoke artifacts

Authenticated price/catalog/surface smoke only.${storefront_note} PHP remains authoritative.
EOF
)"

push_branch
exit $?
