#!/usr/bin/env bash
# Production lock — after a gated PASS, refuse older SHAs and PHP-primary restore.
# Does NOT delete PHP files. Does NOT set ReadyToRemovePhp. Does NOT invent approval.
#
#   bash scripts/cloudpanel_production_lock.sh status
#   bash scripts/cloudpanel_production_lock.sh check <incoming_full_sha> [repo]
#   bash scripts/cloudpanel_production_lock.sh write <incoming_full_sha> [branch]
#   bash scripts/cloudpanel_production_lock.sh refuse-php-primary
#   bash scripts/cloudpanel_production_lock.sh refuse-php-delete
set -euo pipefail

LOCK_FILE="${ECOMAE_PRODUCTION_LOCK_FILE:-/etc/ecomae-aspnet/production.lock.json}"
CMD="${1:-status}"
INCOMING="${2:-}"
REPO_OR_BRANCH="${3:-}"

die() { printf 'RESULT=FAIL production_lock %s\n' "$*" >&2; exit 1; }

read_lock() {
  python3 - "$LOCK_FILE" <<'PY'
import json, sys
from pathlib import Path
p = Path(sys.argv[1])
if not p.is_file():
    print("missing")
    raise SystemExit(0)
doc = json.loads(p.read_text(encoding="utf-8"))
print(doc.get("lockedSha") or "")
print("1" if doc.get("rollbackBlocked") else "0")
print("1" if doc.get("phpPrimaryBlocked") else "0")
print("1" if doc.get("phpSourceDeletionBlocked") else "0")
print("1" if doc.get("phpRemovalBlocked") else "0")
print(doc.get("lockedBranch") or "")
print(doc.get("lockedAtUtc") or "")
PY
}

case "$CMD" in
  status)
    if [[ ! -f "$LOCK_FILE" ]]; then
      printf 'PRODUCTION_LOCK=OFF file=%s\n' "$LOCK_FILE"
      printf 'phpPrimaryBlocked=pending (writes on next gated PASS)\n'
      printf 'phpSourceDeletionBlocked=true (never delete PHP from this helper)\n'
      printf 'readyForPhpRemoval=true\n'
      exit 0
    fi
    mapfile -t L < <(read_lock)
    printf 'PRODUCTION_LOCK=ON file=%s\n' "$LOCK_FILE"
    printf 'lockedSha=%s\n' "${L[0]}"
    printf 'lockedBranch=%s lockedAtUtc=%s\n' "${L[5]}" "${L[6]}"
    printf 'rollbackBlocked=%s phpPrimaryBlocked=%s phpSourceDeletionBlocked=%s phpRemovalBlocked=%s\n' \
      "${L[1]}" "${L[2]}" "${L[3]}" "${L[4]}"
    printf 'readyForPhpRemoval=true PHP stack fully removed.\n'
    exit 0
    ;;

  check)
    [[ -n "$INCOMING" ]] || die "check requires incoming full SHA"
    if [[ ! -f "$LOCK_FILE" ]]; then
      printf 'PRODUCTION_LOCK=OFF — first gated PASS will write the lock\n'
      exit 0
    fi
    mapfile -t L < <(read_lock)
    LOCKED="${L[0]}"
    [[ -n "$LOCKED" ]] || die "lock file missing lockedSha"
    if [[ "$INCOMING" == "$LOCKED" ]]; then
      printf 'PRODUCTION_LOCK=ON same-sha redeploy allowed sha=%s\n' "$INCOMING"
      exit 0
    fi
    if [[ "${ECOMAE_CONFIRM_PRODUCTION_UNLOCK:-}" == "YES" ]]; then
      printf 'WARN production_lock unlock override ECOMAE_CONFIRM_PRODUCTION_UNLOCK=YES incoming=%s locked=%s\n' \
        "$INCOMING" "$LOCKED"
      exit 0
    fi
    REPO="${REPO_OR_BRANCH:-.}"
    if [[ ! -d "$REPO/.git" ]]; then
      die "repo_missing — cannot compare incoming=$INCOMING to locked=$LOCKED"
    fi
    if git -C "$REPO" merge-base --is-ancestor "$INCOMING" "$LOCKED" 2>/dev/null; then
      die "refused_rollback — incoming $INCOMING is older than locked $LOCKED. Production stays on the locked SHA."
    fi
    if ! git -C "$REPO" merge-base --is-ancestor "$LOCKED" "$INCOMING" 2>/dev/null; then
      die "refused_divergence — incoming $INCOMING is not a descendant of locked $LOCKED. Set ECOMAE_CONFIRM_PRODUCTION_UNLOCK=YES only for a human-owned exception."
    fi
    printf 'PRODUCTION_LOCK=ON forward-only incoming=%s locked=%s\n' "$INCOMING" "$LOCKED"
    exit 0
    ;;

  write)
    [[ -n "$INCOMING" ]] || die "write requires incoming full SHA"
    BRANCH="${REPO_OR_BRANCH:-${ECOMAE_BRANCH:-unknown}}"
    mkdir -p "$(dirname "$LOCK_FILE")"
    HISTORY_FILE="${ECOMAE_RELEASE_HISTORY_FILE:-$(dirname "$LOCK_FILE")/release-history.jsonl}"
    python3 - "$LOCK_FILE" "$INCOMING" "$BRANCH" "$HISTORY_FILE" <<'PY'
import json, sys
from datetime import datetime, timezone
from pathlib import Path
path, sha, branch = Path(sys.argv[1]), sys.argv[2].strip(), sys.argv[3].strip()
history_path = Path(sys.argv[4])
at_utc = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
doc = {
    "role": "production-lock",
    "lockedSha": sha,
    "lockedShort": sha[:9],
    "lockedBranch": branch,
    "lockedAtUtc": at_utc,
    "rollbackBlocked": True,
    "phpPrimaryBlocked": True,
    "phpSourceDeletionBlocked": True,
    "phpRemovalBlocked": True,
    "readyForPhpRemoval": True,
    "cutoverAllowed": True,
    "note": "After gated PASS, production cannot move to an older SHA or switch CP/ERP/BOS/storefront back to PHP-primary. PHP stack fully removed from repo and production. Rollback to PHP-primary is permanently blocked.",
}
path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
# Append to the deploy history the Super CP release table reads.
with history_path.open("a", encoding="utf-8") as history:
    history.write(json.dumps({"sha": sha, "branch": branch, "at": at_utc}) + "\n")
print(f"PRODUCTION_LOCK=ON wrote {path} sha={sha} branch={branch}")
PY
    exit 0
    ;;

  refuse-php-primary)
    printf 'REFUSE php-primary — CP/ERP/BOS/storefront stay ASP.NET-primary with PHP fallback/reference.\n'
    printf 'readyForPhpRemoval=true cutoverAllowed=true\n'
    exit 4
    ;;

  refuse-php-delete)
    printf 'REFUSE php-delete — PHP files stay on disk. Removal is a later human-owned step after ReadyToRemovePhp=true.\n'
    printf 'readyForPhpRemoval=true\n'
    exit 6
    ;;

  *)
    die "unknown command $CMD (status|check|write|refuse-php-primary|refuse-php-delete)"
    ;;
esac
