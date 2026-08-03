#!/usr/bin/env bash
# Capture authenticated ASP.NET digest JSON samples for dual-sample parity.
# Writes docs/migration/evidence/surface-parity/samples/aspnet-*.json
#
# When ECOMAE_PHP_DIGEST_BASE_URL is unset (typical after exact-route shadows),
# seeds php-*.json from migration/ contract goldens so compare can run
# contract-only baseline pairs (migration left + live ASP.NET right).
#
# Requires admin cookie (CP/ERP/BOS digests):
#   set -a; source /etc/ecomae-aspnet/platform.env; set +a
#   bash scripts/cloudpanel_capture_digest_dual_samples.sh
#
# Then compare:
#   python3 scripts/compare_digest_dual_samples.py
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${ECOMAE_DIGEST_SAMPLES_DIR:-$ROOT/docs/migration/evidence/surface-parity/samples}"
MIG="$OUT/migration"
ASPNET_BASE="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"
PHP_BASE="${ECOMAE_PHP_DIGEST_BASE_URL:-}"
COOKIE="${ECOMAE_ADMIN_COOKIE_HEADER:-}"
RUN_COMPARE="${ECOMAE_DIGEST_DUAL_COMPARE:-1}"

if [[ -z "$COOKIE" ]]; then
  printf 'ERROR: set ECOMAE_ADMIN_COOKIE_HEADER (admin session cookie)\n' >&2
  printf 'Hint: ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n' >&2
  printf '        bash scripts/cloudpanel_issue_smoke_credentials.sh\n' >&2
  exit 2
fi

mkdir -p "$OUT"

# stem -> path (matches compare_digest_dual_samples.py contracts)
declare -A ROUTES=(
  [cp-dashboard-summary]="/cp/dashboard-summary"
  [erp-dashboard-summary]="/erp/dashboard-summary"
  [erp-accounts-summary]="/erp/accounts-summary"
  [bos-fleet-summary]="/bos/fleet-summary"
  [bos-fleet-health]="/bos/fleet-health"
  [bos-fleet-readiness]="/bos/fleet-readiness"
  [erp-inventory-stock]="/erp/inventory-stock"
  [cp-config-items]="/cp/config-items?limit=5"
  [cp-admin-sessions]="/cp/admin-sessions?limit=5"
  [cp-storages]="/cp/storages?limit=5"
  [erp-cash-accounts]="/erp/cash-accounts?limit=5"
  [erp-cash-entries]="/erp/cash-entries?limit=5"
  [bos-tenants]="/bos/tenants?limit=5"
)

capture() {
  local label="$1" base="$2" path="$3" out="$4"
  local code
  code="$(curl -sS -m 30 \
    -H "Cookie: $COOKIE" \
    -H 'Accept: application/json' \
    -A 'Mozilla/5.0 EcomAE-digest-dual-sample' \
    -o "$out" -w '%{http_code}' \
    "${base}${path}" || echo 000)"
  if [[ "$code" != "200" ]]; then
    printf 'FAIL %s %s HTTP %s\n' "$label" "$path" "$code" >&2
    head -c 160 "$out" >&2 || true
    printf '\n' >&2
    return 1
  fi
  if grep -qi '<!DOCTYPE\|<html' "$out" 2>/dev/null; then
    printf 'FAIL %s %s returned HTML\n' "$label" "$path" >&2
    return 1
  fi
  python3 - "$out" <<'PY'
import json, sys
from pathlib import Path
p = Path(sys.argv[1])
doc = json.loads(p.read_text(encoding="utf-8"))
p.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
PY
  printf 'OK %s %s -> %s\n' "$label" "$path" "$out"
}

ok=0
fail=0
seeded=0
for stem in "${!ROUTES[@]}"; do
  path="${ROUTES[$stem]}"
  if capture aspnet "$ASPNET_BASE" "$path" "$OUT/aspnet-${stem}.json"; then
    ok=$((ok + 1))
  else
    fail=$((fail + 1))
  fi
  if [[ -n "$PHP_BASE" ]]; then
    if capture php "$PHP_BASE" "$path" "$OUT/php-${stem}.json"; then
      ok=$((ok + 1))
    else
      printf 'WARN: PHP sample missing for %s\n' "$stem" >&2
    fi
  elif [[ -f "$MIG/${stem}.json" && ! -f "$OUT/php-${stem}.json" ]]; then
    # Seed contract baseline from migration golden (not a live PHP capture).
    python3 - "$MIG/${stem}.json" "$OUT/php-${stem}.json" <<'PY'
import json, sys
from pathlib import Path
src, dst = Path(sys.argv[1]), Path(sys.argv[2])
doc = json.loads(src.read_text(encoding="utf-8"))
if isinstance(doc, dict):
    doc["dualSampleBaseline"] = "migration-contract-golden"
    doc["note"] = (doc.get("note") or "") + " | seeded as php-* baseline after exact-route shadow (PHP JSON no longer public)."
dst.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
PY
    printf 'SEED php-%s.json from migration/%s.json (contract baseline)\n' "$stem" "$stem"
    seeded=$((seeded + 1))
  fi
done

printf '\nCaptured ASP.NET samples under %s (ok=%s fails=%s seeded_php_baselines=%s)\n' "$OUT" "$ok" "$fail" "$seeded"
printf 'Next: python3 scripts/compare_digest_dual_samples.py --samples-dir %s\n' "$OUT"
if [[ "$fail" -gt 0 ]]; then
  exit 1
fi

if [[ "$RUN_COMPARE" == "1" ]]; then
  printf '\n-- Running compare_digest_dual_samples.py --\n'
  python3 "$ROOT/scripts/compare_digest_dual_samples.py" --samples-dir "$OUT"
fi
