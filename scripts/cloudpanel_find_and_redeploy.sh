#!/usr/bin/env bash
# Paste-safe CloudPanel redeploy after a merged PR.
# Does NOT assume /var/www/ecomae exists.
#
# If this file itself is missing on the server, your checkout is stale.
# Run first (paste-safe):
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_bootstrap_from_github.sh)"
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
ECOMAE_ASPNET_RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
ECOMAE_ASPNET_ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
CANDIDATES=("${ECOMAE_REPO:-}" /root/ecomae /opt/ecomae-aspnet-source /opt/ecomae)

printf '== CloudPanel find + redeploy (%s) ==\n' "$ECOMAE_BRANCH"
printf 'Note: /var/www/ecomae is NOT a required path.\n'
printf 'Release root: %s\n' "$ECOMAE_ASPNET_RELEASE_ROOT"
printf 'Env dir:      %s\n' "$ECOMAE_ASPNET_ENV_DIR"
if [[ "$ECOMAE_BRANCH" == "main" ]]; then
  printf 'NOTE: PR #603 ensure→issue tooling is on main. After deploy:\n'
  printf '      ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n'
  printf '      ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
fi

find_repo() {
  local candidate found
  for candidate in "${CANDIDATES[@]}"; do
    if [[ -n "$candidate" && -d "$candidate/.git" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  for candidate in "${CANDIDATES[@]}"; do
    if [[ -n "$candidate" && -f "$candidate/scripts/cloudpanel_production_deploy_foundation.sh" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  found="$(find /var/www /opt /root -maxdepth 6 -type d -name '.git' -path '*/ecomae*/.git' -print -quit 2>/dev/null || true)"
  if [[ -n "$found" ]]; then
    printf '%s\n' "$(dirname "$found")"
    return 0
  fi
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
  rm -rf /opt/ecomae-aspnet-source
  git clone "$ECOMAE_GIT_URL" /opt/ecomae-aspnet-source
  REPO="/opt/ecomae-aspnet-source"
fi

cd "$REPO"
pwd
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"

if [[ ! -f scripts/cloudpanel_production_deploy_foundation.sh ]]; then
  printf 'ERROR: scripts/cloudpanel_production_deploy_foundation.sh still missing after git reset.\n' >&2
  printf 'Run bootstrap instead:\n' >&2
  printf '  bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_bootstrap_from_github.sh)"\n' >&2
  exit 1
fi

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

# Default emergency publish ON — foundation/unit gates have left live :5100 on stale
# binaries after merged PRs (#877/#878). Set ECOMAE_EMERGENCY_PUBLISH=0 to force gates.
sudo ECOMAE_BRANCH="$ECOMAE_BRANCH" \
ECOMAE_RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-1}" \
ECOMAE_EMERGENCY_PUBLISH="${ECOMAE_EMERGENCY_PUBLISH:-1}" \
ECOMAE_INSTALL_DIAGNOSTICS_NGINX="${ECOMAE_INSTALL_DIAGNOSTICS_NGINX:-0}" \
ECOMAE_ASPNET_RELEASE_ROOT="$ECOMAE_ASPNET_RELEASE_ROOT" \
ECOMAE_ASPNET_ENV_DIR="$ECOMAE_ASPNET_ENV_DIR" \
bash scripts/cloudpanel_production_deploy_foundation.sh

printf '\nVerify:\n'
printf '  bash scripts/wait_for_aspnet_health.sh\n'
printf '  curl -i http://127.0.0.1:5100/health\n'
printf '  systemctl status ecomae-platform.service --no-pager\n'
printf 'Then capture (needs real keys/cookie in /etc/ecomae-aspnet/platform.env):\n'
printf '  source /etc/ecomae-aspnet/platform.env\n'
printf '  bash scripts/cloudpanel_validate_final_gate_env.sh\n'
printf '  bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'
printf 'PHP remains fallback. Do not enable broad nginx cutover.\n'
