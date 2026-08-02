#!/usr/bin/env bash
set -euo pipefail

if [[ "${RUN_PRICE_LOOKUP_SMOKE:-0}" != "1" ]]; then
  echo "WARN price lookup exact-route smoke skipped; set RUN_PRICE_LOOKUP_SMOKE=1 with ECOMAE_ASPNET_BASE_URL, ECOMAE_PRICE_LOOKUP_API_KEY, and optional ECOMAE_PHP_BASE_URL."
  exit 0
fi

: "${ECOMAE_ASPNET_BASE_URL:?ECOMAE_ASPNET_BASE_URL is required}"
: "${ECOMAE_PRICE_LOOKUP_API_KEY:?ECOMAE_PRICE_LOOKUP_API_KEY is required (epc_pricepro_...)}"
BRAND="${PRICE_LOOKUP_BRAND:-TOYOTA}"
ARTICLE="${PRICE_LOOKUP_ARTICLE:-04465-0K020}"
ROUTE="/api/v1/price/lookup?brand=${BRAND}&article=${ARTICLE}"
OUT_DIR="${ECOMAE_SMOKE_OUT_DIR:-/tmp}"
ASPNET_OUT="${OUT_DIR}/ecomae-aspnet-price-lookup.json"
mkdir -p "$OUT_DIR"

redact_body() {
  python3 - "$1" <<'PY'
import json, sys
path = sys.argv[1]
try:
    raw = open(path, encoding="utf-8", errors="replace").read()
    doc = json.loads(raw)
    if isinstance(doc, dict) and isinstance(doc.get("error"), dict):
        print(f"error.code={doc['error'].get('code')!r} message={doc['error'].get('message')!r}"[:220])
    else:
        print(raw[:200].replace("\n", " "))
except Exception:
    print(open(path, encoding="utf-8", errors="replace").read()[:200].replace("\n", " "))
PY
}

aspnet_status="$(curl -sS -o "$ASPNET_OUT" -w '%{http_code}' \
  -H "X-API-Key: ${ECOMAE_PRICE_LOOKUP_API_KEY}" \
  "${ECOMAE_ASPNET_BASE_URL}${ROUTE}" || true)"
if [[ "$aspnet_status" != "200" ]]; then
  echo "FAIL ASP.NET exact price lookup returned HTTP $aspnet_status"
  echo "  body(redacted): $(redact_body "$ASPNET_OUT")"
  exit 1
fi

python3 - "$ASPNET_OUT" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
if isinstance(doc.get("error"), dict):
    raise SystemExit(f"FAIL price lookup body has error: {doc['error'].get('code')}")
# Accept either offers envelope or status+offers PHP-shaped payload.
if "offers" not in doc and "status" not in doc:
    raise SystemExit("FAIL price lookup JSON missing offers/status fields")
print("OK price lookup JSON shape")
PY

if [[ -n "${ECOMAE_PHP_BASE_URL:-}" ]]; then
  php_out="${OUT_DIR}/ecomae-php-price-lookup.json"
  php_status="$(curl -sS -o "$php_out" -w '%{http_code}' \
    -H "X-API-Key: ${ECOMAE_PRICE_LOOKUP_API_KEY}" \
    "${ECOMAE_PHP_BASE_URL}${ROUTE}" || true)"
  if [[ "$php_status" != "200" ]]; then
    echo "FAIL PHP fallback exact price lookup returned HTTP $php_status"
    echo "  body(redacted): $(redact_body "$php_out")"
    exit 1
  fi
fi

cp -f "$ASPNET_OUT" "${OUT_DIR}/price-lookup-aspnet.json"
echo "PASS price lookup exact-route smoke completed for ${ROUTE}; broad /api cutover was not required."
echo "Artifact: ${OUT_DIR}/price-lookup-aspnet.json"
