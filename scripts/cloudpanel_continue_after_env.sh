#!/usr/bin/env bash
# Continue CloudPanel deploy AFTER editing /etc/ecomae-aspnet/platform.env in nano.
# Usage (on the production server):
#   1) In nano: Ctrl+O, Enter, Ctrl+X
#   2) bash scripts/cloudpanel_continue_after_env.sh
set -euo pipefail

ENV_FILE="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}/platform.env"
REPO_CANDIDATES=("${ECOMAE_REPO:-}" /root/ecomae /opt/ecomae-aspnet-source /opt/ecomae)

printf '== Continue deploy after platform.env edit ==\n'
printf 'Env file: %s\n' "$ENV_FILE"

if [[ ! -f "$ENV_FILE" ]]; then
  printf 'Missing %s. Create it from deploy/aspnet/platform.env.example first.\n' "$ENV_FILE" >&2
  exit 1
fi

if grep -Eq 'User=<db_user>|Password=<db_password>|<db_user>|<db_password>' "$ENV_FILE"; then
  printf 'ERROR: %s still contains placeholders.\n' "$ENV_FILE" >&2
  printf 'Open it, replace DB values, save (Ctrl+O Enter), exit (Ctrl+X), then re-run this script.\n' >&2
  printf '  sudo nano %s\n' "$ENV_FILE" >&2
  exit 2
fi

if ! grep -Eq '^ConnectionStrings__TenantRegistry=.+' "$ENV_FILE"; then
  printf 'ERROR: ConnectionStrings__TenantRegistry is empty in %s\n' "$ENV_FILE" >&2
  exit 2
fi

chmod 600 "$ENV_FILE" || true
printf 'platform.env looks filled. Starting foundation deploy...\n'
printf 'Reminder: keep StorefrontAspNetEnabled=false, AdminAspNetEnabled=false, RequirePhpFallback=true until final-gate approval.\n'
printf 'After deploy: bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'

REPO=""
for candidate in "${REPO_CANDIDATES[@]}"; do
  if [[ -n "$candidate" && -f "$candidate/scripts/cloudpanel_production_deploy_foundation.sh" ]]; then
    REPO="$candidate"
    break
  fi
done
if [[ -z "$REPO" ]]; then
  found="$(find /var/www /opt /root -maxdepth 5 -type f -path '*/scripts/cloudpanel_production_deploy_foundation.sh' -print -quit 2>/dev/null || true)"
  REPO="${found%/scripts/cloudpanel_production_deploy_foundation.sh}"
fi
if [[ -z "$REPO" || ! -d "$REPO" ]]; then
  printf 'Repository not found under /root/ecomae, /opt/ecomae-aspnet-source, or /opt/ecomae.\n' >&2
  printf '/var/www/ecomae is NOT used — that path is usually missing.\n' >&2
  printf 'Paste-safe recovery (as root):\n' >&2
  printf '  mkdir -p /opt && cd /opt\n' >&2
  printf '  git clone https://github.com/epartscart/ecomae.git ecomae-aspnet-source\n' >&2
  printf '  cd /opt/ecomae-aspnet-source && git checkout main && git pull --ff-only\n' >&2
  printf '  bash scripts/cloudpanel_find_and_redeploy.sh\n' >&2
  exit 1
fi

cd "$REPO"
pwd
git status --short || true

sudo ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}" \
ECOMAE_RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-1}" \
ECOMAE_INSTALL_DIAGNOSTICS_NGINX="${ECOMAE_INSTALL_DIAGNOSTICS_NGINX:-0}" \
bash scripts/cloudpanel_production_deploy_foundation.sh

printf '\nVerify:\n'
printf '  curl -i http://127.0.0.1:5100/health\n'
printf '  systemctl status ecomae-platform.service --no-pager\n'
printf 'PHP remains fallback. Do not enable broad nginx cutover.\n'
