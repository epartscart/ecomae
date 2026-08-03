#!/usr/bin/env bash
set -euo pipefail

if [[ "${RUN_SURFACE_DIGEST_SMOKE:-0}" != "1" ]]; then
  echo "WARN surface digest exact-route smoke skipped; set RUN_SURFACE_DIGEST_SMOKE=1 with ECOMAE_ASPNET_BASE_URL and admin cookie jar."
  exit 0
fi

: "${ECOMAE_ASPNET_BASE_URL:?ECOMAE_ASPNET_BASE_URL is required}"
COOKIE_JAR="${ECOMAE_ADMIN_COOKIE_JAR:-}"
COOKIE_HEADER="${ECOMAE_ADMIN_COOKIE_HEADER:-}"
OUT_DIR="${ECOMAE_SMOKE_OUT_DIR:-/tmp}"
OUT_FILE="${OUT_DIR}/ecomae-aspnet-surface-digests.json"
# Default: digest routes must return authenticated HTTP 200 (401 no longer counts as pass).
REQUIRE_DIGEST_200="${ECOMAE_REQUIRE_AUTHENTICATED_DIGEST_200:-1}"
mkdir -p "$OUT_DIR"

if [[ -z "$COOKIE_JAR" && -z "$COOKIE_HEADER" ]]; then
  echo "FAIL provide ECOMAE_ADMIN_COOKIE_JAR or ECOMAE_ADMIN_COOKIE_HEADER for admin session digests"
  exit 1
fi

auth_args=()
if [[ -n "$COOKIE_JAR" ]]; then
  auth_args+=(-b "$COOKIE_JAR")
else
  auth_args+=(-H "Cookie: ${COOKIE_HEADER}")
fi

routes=(
  "/migration/php-decommission-readiness"
  "/migration/zero-php-completion"
  "/migration/presentation-parity"
  "/migration/live-surface-links"
  "/cp/dashboard-summary"
  "/cp/tenants?limit=5"
  "/cp/users?limit=5"
  "/cp/groups?limit=5"
  "/cp/modules?limit=5"
  "/cp/menus?limit=5"
  "/cp/pages?limit=5"
  "/cp/currencies?limit=5"
  "/cp/api-clients?limit=5"
  "/cp/config-items?limit=5"
  "/cp/admin-sessions?limit=5"
  "/cp/storages?limit=5"
  "/erp/dashboard-summary"
  "/erp/accounts-summary"
  "/erp/suppliers?limit=5"
  "/erp/purchases?limit=5"
  "/erp/cash-accounts?limit=5"
  "/erp/cash-entries?limit=5"
  "/erp/coa-accounts?limit=5"
  "/erp/warehouses?limit=5"
  "/erp/sales-orders?limit=5"
  "/erp/purchase-orders?limit=5"
  "/erp/inventory-stock"
  "/erp/invoices?limit=5"
  "/erp/gl-journals?limit=5"
  "/bos/fleet-summary"
  "/bos/tenants?limit=5"
  "/bos/fleet-health"
  "/bos/fleet-readiness"
  "/bos/audit-log?limit=5"
)

pass=0
fail=0
digest_200=0
results_tmp="$(mktemp)"
: >"$results_tmp"

for route in "${routes[@]}"; do
  tmp="$(mktemp)"
  status="$(curl -sS -o "$tmp" -w '%{http_code}' "${auth_args[@]}" \
    "${ECOMAE_ASPNET_BASE_URL}${route}" || true)"
  bytes="$(wc -c <"$tmp" | tr -d ' ')"
  marker=""
  if [[ "$status" != "200" ]]; then
    marker="$(python3 - "$tmp" <<'PY'
import json, sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
try:
    doc = json.loads(raw)
    if isinstance(doc, dict):
        if isinstance(doc.get("error"), dict):
            print(str(doc["error"].get("code") or doc["error"])[:120])
        elif "message" in doc:
            print(str(doc.get("message"))[:120])
        else:
            print(raw[:120].replace("\n", " "))
    else:
        print(raw[:120].replace("\n", " "))
except Exception:
    print(raw[:120].replace("\n", " "))
PY
)"
  fi
  rm -f "$tmp"

  if [[ "$route" == /migration/* ]]; then
    if [[ "$status" == "200" ]]; then
      echo "PASS ${route} HTTP $status"
      pass=$((pass + 1))
    else
      echo "FAIL ${route} returned HTTP $status marker=${marker}"
      fail=$((fail + 1))
    fi
  else
    if [[ "$status" == "200" ]]; then
      echo "PASS ${route} HTTP $status"
      pass=$((pass + 1))
      digest_200=$((digest_200 + 1))
    elif [[ "$REQUIRE_DIGEST_200" != "1" && "$status" == "401" ]]; then
      echo "PASS ${route} HTTP $status (legacy allow-401 mode)"
      pass=$((pass + 1))
    else
      echo "FAIL ${route} returned HTTP $status (authenticated digest 200 required) marker=${marker}"
      fail=$((fail + 1))
    fi
  fi
  # marker may be empty; keep TSV columns stable
  printf '%s\t%s\t%s\t%s\n' "$route" "$status" "$bytes" "${marker//$'\t'/ }" >>"$results_tmp"
done

python3 - "$OUT_FILE" "$results_tmp" "$digest_200" "$fail" <<'PY'
import json, sys
out, src, digest_200, fail = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
routes = []
with open(src, encoding="utf-8") as fh:
    for line in fh:
        parts = line.rstrip("\n").split("\t")
        route, status, bytes_ = parts[0], parts[1], parts[2]
        marker = parts[3] if len(parts) > 3 else ""
        entry = {"route": route, "status": int(status), "bytes": int(bytes_)}
        if marker:
            entry["errorMarker"] = marker
        routes.append(entry)
ok = fail == 0 and digest_200 > 0
payload = {
    "ok": ok,
    "surface": "migration-smoke",
    "authenticatedDigest200Count": digest_200,
    "routes": routes,
    "note": "ok=true only when at least one CP/ERP/BOS digest returns HTTP 200 and no route failed",
}
with open(out, "w", encoding="utf-8") as fh:
    json.dump(payload, fh, indent=2)
    fh.write("\n")
PY
rm -f "$results_tmp"

cp -f "$OUT_FILE" "${OUT_DIR}/surface-digests-aspnet.json"
echo "Artifact: ${OUT_DIR}/surface-digests-aspnet.json"
echo "Passed: $pass  Failed: $fail  AuthenticatedDigest200: $digest_200"
if [[ "$fail" -gt 0 || "$digest_200" -lt 1 ]]; then
  exit 1
fi
exit 0
