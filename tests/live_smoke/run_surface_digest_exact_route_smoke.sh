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
  "/erp/dashboard-summary"
  "/bos/fleet-summary"
  "/erp/coa-accounts?limit=5"
  "/erp/purchase-orders?limit=5"
  "/cp/currencies?limit=5"
  "/bos/audit-log?limit=5"
)

pass=0
fail=0
results_tmp="$(mktemp)"
: >"$results_tmp"

for route in "${routes[@]}"; do
  tmp="$(mktemp)"
  status="$(curl -sS -o "$tmp" -w '%{http_code}' "${auth_args[@]}" \
    "${ECOMAE_ASPNET_BASE_URL}${route}" || true)"
  bytes="$(wc -c <"$tmp" | tr -d ' ')"
  rm -f "$tmp"

  if [[ "$route" == /migration/* ]]; then
    if [[ "$status" == "200" ]]; then
      echo "PASS ${route} HTTP $status"
      pass=$((pass + 1))
    else
      echo "FAIL ${route} returned HTTP $status"
      fail=$((fail + 1))
    fi
  else
    case "$status" in
      200|401)
        echo "PASS ${route} HTTP $status"
        pass=$((pass + 1))
        ;;
      *)
        echo "FAIL ${route} returned HTTP $status"
        fail=$((fail + 1))
        ;;
    esac
  fi
  printf '%s\t%s\t%s\n' "$route" "$status" "$bytes" >>"$results_tmp"
done

python3 - "$OUT_FILE" "$results_tmp" <<'PY'
import json, sys
out, src = sys.argv[1], sys.argv[2]
routes = []
with open(src, encoding="utf-8") as fh:
    for line in fh:
        route, status, bytes_ = line.rstrip("\n").split("\t")
        routes.append({"route": route, "status": int(status), "bytes": int(bytes_)})
payload = {"ok": True, "surface": "migration-smoke", "routes": routes}
with open(out, "w", encoding="utf-8") as fh:
    json.dump(payload, fh, indent=2)
    fh.write("\n")
PY
rm -f "$results_tmp"

cp -f "$OUT_FILE" "${OUT_DIR}/surface-digests-aspnet.json"
echo "Artifact: ${OUT_DIR}/surface-digests-aspnet.json"
echo "Passed: $pass  Failed: $fail"
exit $(( fail > 0 ? 1 : 0 ))
