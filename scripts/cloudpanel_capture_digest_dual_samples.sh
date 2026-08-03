#!/usr/bin/env bash
# Capture authenticated ASP.NET digest JSON samples for dual-sample parity.
# Writes docs/migration/evidence/surface-parity/samples/aspnet-*.json
#
# When ECOMAE_PHP_DIGEST_BASE_URL is unset (typical after exact-route shadows),
# compare uses migration/ contract goldens as the left baseline (contract-only)
# against live aspnet-*.json. Do not invent live PHP JSON after shadows land.
#
# Requires admin cookie (CP/ERP/BOS digests):
#   set -a; source /etc/ecomae-aspnet/platform.env; set +a
#   bash scripts/cloudpanel_capture_digest_dual_samples.sh
#
# Then compare (auto unless ECOMAE_DIGEST_DUAL_COMPARE=0):
#   python3 scripts/compare_digest_dual_samples.py
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${ECOMAE_DIGEST_SAMPLES_DIR:-$ROOT/docs/migration/evidence/surface-parity/samples}"
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

# stem -> path (matches compare_digest_dual_samples.py contracts; full surface+storefront allowlist)
declare -A ROUTES=(
  [cp-dashboard-summary]="/cp/dashboard-summary"
  [cp-tenants]="/cp/tenants?limit=5"
  [cp-users]="/cp/users?limit=5"
  [cp-groups]="/cp/groups?limit=5"
  [cp-modules]="/cp/modules?limit=5"
  [cp-menus]="/cp/menus?limit=5"
  [cp-pages]="/cp/pages?limit=5"
  [cp-currencies]="/cp/currencies?limit=5"
  [cp-api-clients]="/cp/api-clients?limit=5"
  [cp-config-items]="/cp/config-items?limit=5"
  [cp-admin-sessions]="/cp/admin-sessions?limit=5"
  [cp-storages]="/cp/storages?limit=5"
  [cp-orders-digest]="/cp/orders-digest?limit=5"
  [erp-dashboard-summary]="/erp/dashboard-summary"
  [erp-accounts-summary]="/erp/accounts-summary"
  [erp-suppliers]="/erp/suppliers?limit=5"
  [erp-purchases]="/erp/purchases?limit=5"
  [erp-cash-accounts]="/erp/cash-accounts?limit=5"
  [erp-cash-entries]="/erp/cash-entries?limit=5"
  [erp-coa-accounts]="/erp/coa-accounts?limit=5"
  [erp-warehouses]="/erp/warehouses?limit=5"
  [erp-sales-orders]="/erp/sales-orders?limit=5"
  [erp-purchase-orders]="/erp/purchase-orders?limit=5"
  [erp-inventory-stock]="/erp/inventory-stock"
  [erp-invoices]="/erp/invoices?limit=5"
  [erp-gl-journals]="/erp/gl-journals?limit=5"
  [bos-fleet-summary]="/bos/fleet-summary"
  [bos-tenants]="/bos/tenants?limit=5"
  [bos-fleet-health]="/bos/fleet-health"
  [bos-fleet-readiness]="/bos/fleet-readiness"
  [bos-audit-log]="/bos/audit-log?limit=5"
  [storefront-account-summary]="/storefront/account-summary"
  [storefront-orders]="/storefront/orders?limit=5"
  [storefront-garage]="/storefront/garage?limit=5"
  [storefront-profile]="/storefront/profile"
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
  fi
done

# Remove previously seeded migration php-* baselines so compare uses migration/ + contract-only.
removed=0
for seeded in "$OUT"/php-*.json; do
  [[ -f "$seeded" ]] || continue
  if python3 - "$seeded" <<'PY'
import json, sys
from pathlib import Path
doc = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
sys.exit(0 if isinstance(doc, dict) and doc.get("dualSampleBaseline") == "migration-contract-golden" else 1)
PY
  then
    rm -f "$seeded"
    removed=$((removed + 1))
    printf 'CLEAN seeded baseline %s (compare will use migration/)\n' "$(basename "$seeded")"
  fi
done

printf '\nCaptured ASP.NET samples under %s (ok=%s fails=%s cleaned_seeded=%s)\n' "$OUT" "$ok" "$fail" "$removed"
printf 'Compare uses migration/ goldens as left baseline (contract-only) when PHP JSON is not public.\n'
printf 'Next: python3 scripts/compare_digest_dual_samples.py --samples-dir %s\n' "$OUT"
if [[ "$fail" -gt 0 ]]; then
  exit 1
fi

if [[ "$RUN_COMPARE" == "1" ]]; then
  printf '\n-- Running compare_digest_dual_samples.py --\n'
  python3 "$ROOT/scripts/compare_digest_dual_samples.py" --samples-dir "$OUT"
fi
