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

aspnet_status="$(curl -sS -o "$ASPNET_OUT" -w '%{http_code}' \
  -H "X-API-Key: ${ECOMAE_PRICE_LOOKUP_API_KEY}" \
  "${ECOMAE_ASPNET_BASE_URL}${ROUTE}")"
if [[ "$aspnet_status" != "200" ]]; then
  echo "FAIL ASP.NET exact price lookup returned HTTP $aspnet_status"
  exit 1
fi

if [[ -n "${ECOMAE_PHP_BASE_URL:-}" ]]; then
  php_out="${OUT_DIR}/ecomae-php-price-lookup.json"
  php_status="$(curl -sS -o "$php_out" -w '%{http_code}' \
    -H "X-API-Key: ${ECOMAE_PRICE_LOOKUP_API_KEY}" \
    "${ECOMAE_PHP_BASE_URL}${ROUTE}")"
  if [[ "$php_status" != "200" ]]; then
    echo "FAIL PHP fallback exact price lookup returned HTTP $php_status"
    exit 1
  fi
fi

cp -f "$ASPNET_OUT" "${OUT_DIR}/price-lookup-aspnet.json"
echo "PASS price lookup exact-route smoke completed for ${ROUTE}; broad /api cutover was not required."
echo "Artifact: ${OUT_DIR}/price-lookup-aspnet.json"
