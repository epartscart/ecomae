#!/usr/bin/env bash
# Validate final-gate smoke env WITHOUT printing secret values.
# Exit 0 always when run standalone (report-only). Capture may treat BAD_FORMAT as soft fail.
set -euo pipefail

ECOMAE_ASPNET_ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
ENV_FILE="${ECOMAE_ASPNET_ENV_DIR}/platform.env"
STRICT="${ECOMAE_SMOKE_ENV_STRICT:-0}"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
  printf 'Env file: %s (values redacted)\n' "$ENV_FILE"
else
  printf 'Env file: MISSING (%s)\n' "$ENV_FILE"
fi

ECOMAE_PRICE_LOOKUP_API_KEY="${ECOMAE_PRICE_LOOKUP_API_KEY:-${PRICE_LOOKUP_API_KEY:-}}"
ECOMAE_CATALOG_API_KEY="${ECOMAE_CATALOG_API_KEY:-${CATALOG_API_KEY:-}}"

status_price="MISSING"
status_catalog="MISSING"
status_cookie="MISSING"
bad=0

classify_key() {
  local value="$1"
  local prefix="$2"
  if [[ -z "$value" ]]; then
    printf 'MISSING'
    return
  fi
  if [[ "$value" == "${prefix}"* && ${#value} -ge $((${#prefix} + 8)) ]]; then
    printf 'PRESENT'
    return
  fi
  printf 'BAD_FORMAT'
}

status_price="$(classify_key "$ECOMAE_PRICE_LOOKUP_API_KEY" "epc_pricepro_")"
status_catalog="$(classify_key "$ECOMAE_CATALOG_API_KEY" "epc_catalog_")"

if [[ -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
  if [[ -r "${ECOMAE_ADMIN_COOKIE_JAR}" ]]; then
    status_cookie="PRESENT_JAR"
  else
    status_cookie="BAD_FORMAT"
  fi
elif [[ -n "${ECOMAE_ADMIN_COOKIE_HEADER:-}" ]]; then
  hdr="${ECOMAE_ADMIN_COOKIE_HEADER}"
  if [[ "$hdr" == *"admin_session="* && "$hdr" == *"admin_u_id="* ]]; then
    # admin_u_id should be numeric after =
    if printf '%s' "$hdr" | grep -Eq 'admin_u_id=[0-9]+'; then
      status_cookie="PRESENT"
    else
      status_cookie="BAD_FORMAT"
    fi
  else
    status_cookie="BAD_FORMAT"
  fi
fi

printf 'ECOMAE_PRICE_LOOKUP_API_KEY: %s (expect prefix epc_pricepro_)\n' "$status_price"
printf 'ECOMAE_CATALOG_API_KEY: %s (expect prefix epc_catalog_)\n' "$status_catalog"
printf 'ECOMAE_ADMIN_COOKIE_HEADER/JAR: %s (expect admin_session=...; admin_u_id=<digits>)\n' "$status_cookie"
printf 'Never prints secret values. Does not remove PHP.\n'

for s in "$status_price" "$status_catalog" "$status_cookie"; do
  if [[ "$s" == "BAD_FORMAT" ]]; then
    bad=1
  fi
done

if [[ "$STRICT" == "1" ]]; then
  if [[ "$status_price" != "PRESENT" || "$status_catalog" != "PRESENT" ]]; then
    exit 1
  fi
  if [[ "$status_cookie" != "PRESENT" && "$status_cookie" != "PRESENT_JAR" ]]; then
    exit 1
  fi
  if [[ "$bad" -ne 0 ]]; then
    exit 1
  fi
fi

exit 0
