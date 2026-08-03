#!/usr/bin/env bash
# Exit 0 when EcomAE__SecretSuccession / ECOMAE_SECRET_SUCCESSION is non-empty.
# Never prints the secret value.
set -euo pipefail

ENV_FILE="${ECOMAE_PLATFORM_ENV:-/etc/ecomae-aspnet/platform.env}"
configured=0
source_label="unset"

if [[ -f "$ENV_FILE" ]]; then
  # shellcheck disable=SC1090
  set -a; source "$ENV_FILE"; set +a
fi

if [[ -n "${EcomAE__SecretSuccession:-}" ]]; then
  configured=1
  source_label="EcomAE__SecretSuccession"
elif [[ -n "${ECOMAE_SECRET_SUCCESSION:-}" ]]; then
  configured=1
  source_label="ECOMAE_SECRET_SUCCESSION"
elif [[ -n "${EcomAE_SecretSuccession:-}" ]]; then
  configured=1
  source_label="EcomAE_SecretSuccession"
fi

if [[ "$configured" -eq 1 ]]; then
  printf 'OK: SecretSuccession configured via %s (value not printed).\n' "$source_label"
  exit 0
fi

printf 'FAIL: SecretSuccession not set. Add EcomAE__SecretSuccession=<php secret_succession> to %s and redeploy.\n' "$ENV_FILE" >&2
printf 'Login bridge will fall back to PHP login until configured.\n' >&2
exit 1
