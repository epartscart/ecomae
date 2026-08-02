#!/usr/bin/env bash
set -euo pipefail

if [[ "${RUN_PRICE_LOOKUP_SMOKE:-0}" != "1" ]]; then
  echo "WARN price lookup exact-route smoke skipped; set RUN_PRICE_LOOKUP_SMOKE=1 with ECOMAE_ASPNET_BASE_URL and optional ECOMAE_PHP_BASE_URL."
  exit 0
fi

: "${ECOMAE_ASPNET_BASE_URL:?ECOMAE_ASPNET_BASE_URL is required}"
BRAND="${PRICE_LOOKUP_BRAND:-TOYOTA}"
ARTICLE="${PRICE_LOOKUP_ARTICLE:-04465-0K020}"
ROUTE="/api/v1/price/lookup?brand=${BRAND}&article=${ARTICLE}"

aspnet_status="$(curl -sS -o /tmp/ecomae-aspnet-price-lookup.json -w '%{http_code}' "${ECOMAE_ASPNET_BASE_URL}${ROUTE}")"
if [[ "$aspnet_status" != "200" ]]; then
  echo "FAIL ASP.NET exact price lookup returned HTTP $aspnet_status"
  exit 1
fi

if [[ -n "${ECOMAE_PHP_BASE_URL:-}" ]]; then
  php_status="$(curl -sS -o /tmp/ecomae-php-price-lookup.json -w '%{http_code}' "${ECOMAE_PHP_BASE_URL}${ROUTE}")"
  if [[ "$php_status" != "200" ]]; then
    echo "FAIL PHP fallback exact price lookup returned HTTP $php_status"
    exit 1
  fi
fi

echo "PASS price lookup exact-route smoke completed for ${ROUTE}; broad /api cutover was not required."
