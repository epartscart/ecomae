#!/usr/bin/env bash
# Source-able helper: repair truncated ECOMAE_ADMIN_COOKIE_HEADER in the current shell.
# Older issuer writes were unquoted, so `source platform.env` kept only admin_session=
# and turned `; admin_u_id=N` into a separate shell assignment.
# Safe to source multiple times. Never prints secret values.
#
# Usage:
#   set -a; source /etc/ecomae-aspnet/platform.env; set +a
#   # shellcheck disable=SC1091
#   source scripts/cloudpanel_repair_smoke_cookie_env.sh

# Strip accidental wrapping quotes.
if [[ -n "${ECOMAE_ADMIN_COOKIE_HEADER:-}" ]]; then
  hdr="${ECOMAE_ADMIN_COOKIE_HEADER}"
  if [[ "${hdr:0:1}" == "'" && "${hdr: -1}" == "'" ]]; then
    hdr="${hdr:1:${#hdr}-2}"
  elif [[ "${hdr:0:1}" == '"' && "${hdr: -1}" == '"' ]]; then
    hdr="${hdr:1:${#hdr}-2}"
  fi
  ECOMAE_ADMIN_COOKIE_HEADER="$hdr"
  export ECOMAE_ADMIN_COOKIE_HEADER
fi

if [[ -n "${ECOMAE_ADMIN_COOKIE_HEADER:-}" && "${ECOMAE_ADMIN_COOKIE_HEADER}" != *"admin_u_id="* ]]; then
  repair_uid="${ECOMAE_ADMIN_U_ID:-${admin_u_id:-}}"
  if [[ "$repair_uid" =~ ^[0-9]+$ ]]; then
    ECOMAE_ADMIN_COOKIE_HEADER="${ECOMAE_ADMIN_COOKIE_HEADER}; admin_u_id=${repair_uid}"
    export ECOMAE_ADMIN_COOKIE_HEADER
  fi
fi

if [[ -n "${ECOMAE_CUSTOMER_COOKIE_HEADER:-}" ]]; then
  chdr="${ECOMAE_CUSTOMER_COOKIE_HEADER}"
  if [[ "${chdr:0:1}" == "'" && "${chdr: -1}" == "'" ]]; then
    chdr="${chdr:1:${#chdr}-2}"
  elif [[ "${chdr:0:1}" == '"' && "${chdr: -1}" == '"' ]]; then
    chdr="${chdr:1:${#chdr}-2}"
  fi
  ECOMAE_CUSTOMER_COOKIE_HEADER="$chdr"
  export ECOMAE_CUSTOMER_COOKIE_HEADER
fi
