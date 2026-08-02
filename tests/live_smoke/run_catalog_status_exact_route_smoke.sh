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

aspnet_status="$(curl -sS -o "$ASPNET_OUT" -w '%{http_code}' \
  -H "X-API-Key: ${ECOMAE_CATALOG_API_KEY}" \
  "${ECOMAE_ASPNET_BASE_URL}${ROUTE}")"
if [[ "$aspnet_status" != "200" ]]; then
  echo "FAIL ASP.NET exact catalog status returned HTTP $aspnet_status"
  exit 1
fi

if [[ -n "${ECOMAE_PHP_BASE_URL:-}" ]]; then
  php_out="${OUT_DIR}/ecomae-php-catalog-status.json"
  php_status="$(curl -sS -o "$php_out" -w '%{http_code}' \
    -H "X-API-Key: ${ECOMAE_CATALOG_API_KEY}" \
    "${ECOMAE_PHP_BASE_URL}${ROUTE}")"
  if [[ "$php_status" != "200" ]]; then
    echo "FAIL PHP fallback exact catalog status returned HTTP $php_status"
    exit 1
  fi
fi

echo "PASS catalog status exact-route smoke completed for ${ROUTE}; broad /api cutover was not required."
echo "Artifact: ${ASPNET_OUT}"
