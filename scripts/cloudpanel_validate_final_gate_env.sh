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

cookie_hint=""
if [[ -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
  if [[ -r "${ECOMAE_ADMIN_COOKIE_JAR}" ]]; then
    status_cookie="PRESENT_JAR"
  else
    status_cookie="BAD_FORMAT"
    cookie_hint="cookie jar path not readable"
  fi
elif [[ -n "${ECOMAE_ADMIN_COOKIE_HEADER:-}" ]]; then
  hdr="${ECOMAE_ADMIN_COOKIE_HEADER}"
  # Strip accidental wrapping quotes from env file values.
  if [[ "${hdr:0:1}" == "'" && "${hdr: -1}" == "'" ]]; then
    hdr="${hdr:1:${#hdr}-2}"
  elif [[ "${hdr:0:1}" == '"' && "${hdr: -1}" == '"' ]]; then
    hdr="${hdr:1:${#hdr}-2}"
  fi
  ECOMAE_ADMIN_COOKIE_HEADER="$hdr"
  export ECOMAE_ADMIN_COOKIE_HEADER
  if [[ "$hdr" == *"admin_session="* && "$hdr" == *"admin_u_id="* ]]; then
    # admin_u_id should be numeric after =
    if printf '%s' "$hdr" | grep -Eq 'admin_u_id=[0-9]+'; then
      status_cookie="PRESENT"
    else
      status_cookie="BAD_FORMAT"
      cookie_hint="admin_u_id must be digits (admin_u_id=123)"
    fi
  else
    status_cookie="BAD_FORMAT"
    has_session=0
    has_uid=0
    [[ "$hdr" == *"admin_session="* ]] && has_session=1
    [[ "$hdr" == *"admin_u_id="* ]] && has_uid=1
    cookie_hint="need both admin_session= (has=${has_session}) and admin_u_id= (has=${has_uid}); length=${#hdr}"
  fi
fi

# Optional customer cookie for storefront digest promotion (not required for ReadyToRemovePhp).
status_customer="MISSING"
customer_hint=""
if [[ -n "${ECOMAE_CUSTOMER_COOKIE_JAR:-}" ]]; then
  if [[ -r "${ECOMAE_CUSTOMER_COOKIE_JAR}" ]]; then
    status_customer="PRESENT_JAR"
  else
    status_customer="BAD_FORMAT"
    customer_hint="customer cookie jar path not readable"
  fi
elif [[ -n "${ECOMAE_CUSTOMER_COOKIE_HEADER:-}" ]]; then
  chdr="${ECOMAE_CUSTOMER_COOKIE_HEADER}"
  if [[ "${chdr:0:1}" == "'" && "${chdr: -1}" == "'" ]]; then
    chdr="${chdr:1:${#chdr}-2}"
  elif [[ "${chdr:0:1}" == '"' && "${chdr: -1}" == '"' ]]; then
    chdr="${chdr:1:${#chdr}-2}"
  fi
  ECOMAE_CUSTOMER_COOKIE_HEADER="$chdr"
  export ECOMAE_CUSTOMER_COOKIE_HEADER
  if [[ "$chdr" == *"session="* && "$chdr" == *"u_id="* ]]; then
    if printf '%s' "$chdr" | grep -Eq '(^|[; ])u_id=[0-9]+'; then
      status_customer="PRESENT"
    else
      status_customer="BAD_FORMAT"
      customer_hint="u_id must be digits (u_id=123); do not use admin_u_id here"
    fi
  else
    status_customer="BAD_FORMAT"
    customer_hint="need both session= and u_id=<digits> (customer cookies, not admin_session)"
  fi
fi

printf 'ECOMAE_PRICE_LOOKUP_API_KEY: %s (expect prefix epc_pricepro_)\n' "$status_price"
printf 'ECOMAE_CATALOG_API_KEY: %s (expect prefix epc_catalog_)\n' "$status_catalog"
printf 'ECOMAE_ADMIN_COOKIE_HEADER/JAR: %s (expect admin_session=...; admin_u_id=<digits>)\n' "$status_cookie"
if [[ -n "${cookie_hint:-}" ]]; then
  printf '  cookie detail: %s\n' "$cookie_hint"
fi
printf 'ECOMAE_CUSTOMER_COOKIE_HEADER/JAR: %s (optional; expect session=...; u_id=<digits>)\n' "$status_customer"
if [[ -n "${customer_hint:-}" ]]; then
  printf '  customer cookie detail: %s\n' "$customer_hint"
fi
printf 'Never prints secret values. Does not remove PHP.\n'
printf 'Next if anything MISSING/BAD_FORMAT: bash scripts/cloudpanel_prepare_smoke_secrets.sh\n'

for s in "$status_price" "$status_catalog" "$status_cookie" "$status_customer"; do
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
  if [[ "${ECOMAE_SMOKE_REQUIRE_CUSTOMER:-0}" == "1" ]]; then
    if [[ "$status_customer" != "PRESENT" && "$status_customer" != "PRESENT_JAR" ]]; then
      exit 1
    fi
  fi
  if [[ "$bad" -ne 0 ]]; then
    exit 1
  fi
fi

exit 0
