#!/usr/bin/env bash
# Paste-safe CloudPanel redeploy after a merged PR.
# Does NOT assume /var/www/ecomae exists.
#
# Usage (as root on the server):
#   bash scripts/cloudpanel_find_and_redeploy.sh
# Or from anywhere after clone:
#   curl is not required — copy this file or run from an existing checkout.
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
ECOMAE_ASPNET_RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
ECOMAE_ASPNET_ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
CANDIDATES=("${ECOMAE_REPO:-}" /root/ecomae /opt/ecomae-aspnet-source /opt/ecomae)

printf '== CloudPanel find + redeploy (main) ==\n'
printf 'Note: /var/www/ecomae is NOT a required path.\n'
printf 'Release root: %s\n' "$ECOMAE_ASPNET_RELEASE_ROOT"
printf 'Env dir:      %s\n' "$ECOMAE_ASPNET_ENV_DIR"

find_repo() {
  local candidate found
  for candidate in "${CANDIDATES[@]}"; do
    if [[ -n "$candidate" && -f "$candidate/scripts/cloudpanel_production_deploy_foundation.sh" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  found="$(find /var/www /opt /root -maxdepth 6 -type f -path '*/scripts/cloudpanel_production_deploy_foundation.sh' -print -quit 2>/dev/null || true)"
  if [[ -n "$found" ]]; then
    printf '%s\n' "${found%/scripts/cloudpanel_production_deploy_foundation.sh}"
    return 0
  fi
  return 1
}

REPO=""
if REPO="$(find_repo)"; then
  printf 'Found repo: %s\n' "$REPO"
else
  printf 'Repo not found. Cloning to /opt/ecomae-aspnet-source ...\n'
  mkdir -p /opt
  if [[ ! -d /opt/ecomae-aspnet-source/.git ]]; then
    git clone "$ECOMAE_GIT_URL" /opt/ecomae-aspnet-source
  fi
  REPO="/opt/ecomae-aspnet-source"
fi

cd "$REPO"
pwd
git fetch origin "$ECOMAE_BRANCH"
git checkout "$ECOMAE_BRANCH"
git pull --ff-only origin "$ECOMAE_BRANCH"

if [[ ! -f "${ECOMAE_ASPNET_ENV_DIR}/platform.env" ]]; then
  mkdir -p "$ECOMAE_ASPNET_ENV_DIR" "$ECOMAE_ASPNET_RELEASE_ROOT/releases"
  install -m 0600 deploy/aspnet/platform.env.example "${ECOMAE_ASPNET_ENV_DIR}/platform.env"
  printf '\nCreated %s/platform.env — edit DB values, then re-run:\n' "$ECOMAE_ASPNET_ENV_DIR"
  printf '  nano %s/platform.env\n' "$ECOMAE_ASPNET_ENV_DIR"
  printf '  # Ctrl+O Enter, Ctrl+X\n'
  printf '  bash scripts/cloudpanel_continue_after_env.sh\n'
  exit 2
fi

if grep -Eq 'User=<db_user>|Password=<db_password>|<db_user>|<db_password>' "${ECOMAE_ASPNET_ENV_DIR}/platform.env"; then
  printf 'ERROR: platform.env still has placeholders. Edit it first:\n' >&2
  printf '  nano %s/platform.env\n' "$ECOMAE_ASPNET_ENV_DIR" >&2
  printf 'Then: bash scripts/cloudpanel_continue_after_env.sh\n' >&2
  exit 2
fi

sudo ECOMAE_BRANCH="$ECOMAE_BRANCH" \
ECOMAE_RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-1}" \
ECOMAE_INSTALL_DIAGNOSTICS_NGINX="${ECOMAE_INSTALL_DIAGNOSTICS_NGINX:-0}" \
ECOMAE_ASPNET_RELEASE_ROOT="$ECOMAE_ASPNET_RELEASE_ROOT" \
bash scripts/cloudpanel_production_deploy_foundation.sh

printf '\nVerify:\n'
printf '  curl -i http://127.0.0.1:5100/health\n'
printf '  systemctl status ecomae-platform.service --no-pager\n'
printf 'PHP remains fallback. Do not enable broad nginx cutover.\n'
