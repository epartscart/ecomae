#!/usr/bin/env bash
# One CloudPanel paste: remap Super admin ecomaedxb@gmail.com → yawer@ecomae.com
# (same password hash) in the platform registry users table, sync SecretSuccession
# from PHP, restart ASP.NET. Never touches a shop db_name. Never prints hashes.
#
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/erp-logout-everywhere-5dbd/scripts/cloudpanel_enable_super_operator_yawer.sh)"
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

BRANCH="${ECOMAE_BRANCH:-cursor/erp-logout-everywhere-5dbd}"
REPO="${ECOMAE_REPO:-/opt/ecomae-aspnet-source}"
RAW="https://raw.githubusercontent.com/epartscart/ecomae/${BRANCH}"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

printf '== Enable Super operator yawer@ecomae.com ==\n'
printf 'This uses the platform registry users table — not a shop db_name.\n'

curl -fsSL "${RAW}/scripts/cloudpanel_remap_super_operator_yawer.sh" -o "$TMP/remap.sh"
chmod +x "$TMP/remap.sh"

printf '\n-- diagnose (no write) --\n'
ECOMAE_CONFIRM_REMAP_SUPER_OPERATOR=0 bash "$TMP/remap.sh" || true

printf '\n-- remap email, keep password hash --\n'
ECOMAE_CONFIRM_REMAP_SUPER_OPERATOR=YES bash "$TMP/remap.sh"

if [[ -f "$REPO/scripts/cloudpanel_sync_secret_succession_from_php.sh" ]]; then
  printf '\n-- sync SecretSuccession from PHP + restart --\n'
  ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES \
  ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
    bash "$REPO/scripts/cloudpanel_sync_secret_succession_from_php.sh"
else
  printf '\nWARN: %s missing — restarting ASP.NET only.\n' "$REPO/scripts/cloudpanel_sync_secret_succession_from_php.sh"
  if grep -qE '^EcomAE__SecretSuccession=.+' /etc/ecomae-aspnet/platform.env 2>/dev/null; then
    systemctl restart ecomae-platform.service
    printf 'Restarted ecomae-platform.service. SecretSuccession was already set.\n'
  else
    printf 'ERROR: SecretSuccession is empty. Fetch the repo and run:\n' >&2
    printf '  ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES ECOMAE_CONFIRM_RESTART_PLATFORM=YES \\\n' >&2
    printf '    bash /opt/ecomae-aspnet-source/scripts/cloudpanel_sync_secret_succession_from_php.sh\n' >&2
    exit 1
  fi
fi

printf '\nRESULT=OK Sign in on https://www.ecomae.com/erp/login as yawer@ecomae.com\n'
printf 'with the same password previously used for ecomaedxb@gmail.com.\n'
printf 'Do not use a tenant host (epartscart.com, taxofinca.com, …).\n'
