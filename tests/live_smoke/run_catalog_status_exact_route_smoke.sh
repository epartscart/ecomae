#!/usr/bin/env bash
set -euo pipefail

if [[ "${RUN_CATALOG_STATUS_SMOKE:-0}" != "1" ]]; then
  echo "WARN catalog status exact-route smoke skipped; set RUN_CATALOG_STATUS_SMOKE=1 with ECOMAE_ASPNET_BASE_URL and ECOMAE_CATALOG_API_KEY."
  exit 0
fi

: "${ECOMAE_ASPNET_BASE_URL:?ECOMAE_ASPNET_BASE_URL is required}"
: "${ECOMAE_CATALOG_API_KEY:?ECOMAE_CATALOG_API_KEY is required (epc_catalog_...)}"
ROUTE="/api/v1/catalog/status"
OUT_DIR="${ECOMAE_SMOKE_OUT_DIR:-/tmp}"
ASPNET_OUT="${OUT_DIR}/ecomae-aspnet-catalog-status.json"
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
  -H "X-API-Key: ${ECOMAE_CATALOG_API_KEY}" \
  "${ECOMAE_ASPNET_BASE_URL}${ROUTE}" || true)"
if [[ "$aspnet_status" != "200" ]]; then
  echo "FAIL ASP.NET exact catalog status returned HTTP $aspnet_status"
  echo "  body(redacted): $(redact_body "$ASPNET_OUT")"
  exit 1
fi

python3 - "$ASPNET_OUT" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
if doc.get("ok") is False or isinstance(doc.get("error"), dict):
    raise SystemExit(f"FAIL catalog status error body: {doc.get('error')}")
for key in ("connected", "message", "status_code", "counts", "source"):
    if key not in doc:
        raise SystemExit(f"FAIL catalog status missing field {key}")
counts = doc["counts"]
for key in ("manufacturers", "models", "modifications", "brands", "vins"):
    if key not in counts:
        raise SystemExit(f"FAIL catalog status.counts missing {key}")
print("OK catalog status JSON shape")
PY

if [[ -n "${ECOMAE_PHP_BASE_URL:-}" ]]; then
  php_out="${OUT_DIR}/ecomae-php-catalog-status.json"
  php_status="$(curl -sS -o "$php_out" -w '%{http_code}' \
    -H "X-API-Key: ${ECOMAE_CATALOG_API_KEY}" \
    "${ECOMAE_PHP_BASE_URL}${ROUTE}" || true)"
  if [[ "$php_status" != "200" ]]; then
    echo "FAIL PHP fallback exact catalog status returned HTTP $php_status"
    echo "  body(redacted): $(redact_body "$php_out")"
    exit 1
  fi
fi

cp -f "$ASPNET_OUT" "${OUT_DIR}/catalog-status-aspnet.json"
echo "PASS catalog status exact-route smoke completed for ${ROUTE}; broad /api cutover was not required."
echo "Artifact: ${OUT_DIR}/catalog-status-aspnet.json"
