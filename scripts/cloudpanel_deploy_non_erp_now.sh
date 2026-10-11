#!/usr/bin/env bash
# ONE CloudPanel paste: publish completed non-ERP (logout, Super yawer login,
# shop-domain mail) on top of current main, remap Super admin, sync
# SecretSuccession, restart ASP.NET. Never prints hashes or cookie values.
#
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/non-erp-production-5dbd/scripts/cloudpanel_deploy_non_erp_now.sh)"
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

BRANCH="${ECOMAE_BRANCH:-cursor/non-erp-production-5dbd}"
GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
REPO="${ECOMAE_REPO:-/opt/ecomae-aspnet-source}"
ASPNET_URL="${ECOMAE_ASPNET_URL:-http://127.0.0.1:5100}"
RAW="https://raw.githubusercontent.com/epartscart/ecomae/${BRANCH}"

printf '======== NON-ERP PRODUCTION DEPLOY ========\n'
printf 'HOST=%s DATE_UTC=%s\n' "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf 'BRANCH=%s REPO=%s\n' "$BRANCH" "$REPO"

mkdir -p "$(dirname "$REPO")"
if [[ ! -d "$REPO/.git" ]]; then
  git clone "$GIT_URL" "$REPO"
fi
cd "$REPO"
git remote set-url origin "$GIT_URL" || true
git fetch origin "$BRANCH"
git checkout -f "$BRANCH"
git reset --hard "origin/$BRANCH"
printf 'HEAD=%s %s\n' "$(git rev-parse --short HEAD)" "$(git log -1 --pretty=%s)"

test -f scripts/cloudpanel_find_and_redeploy.sh
test -f scripts/cloudpanel_enable_super_operator_yawer.sh

printf '\n---- publish ASP.NET (%s) ----\n' "$BRANCH"
ECOMAE_BRANCH="$BRANCH" \
ECOMAE_REPO="$REPO" \
ECOMAE_EMERGENCY_PUBLISH="${ECOMAE_EMERGENCY_PUBLISH:-1}" \
  bash scripts/cloudpanel_find_and_redeploy.sh

if [[ -f scripts/wait_for_aspnet_health.sh ]]; then
  bash scripts/wait_for_aspnet_health.sh || true
fi

printf '\n---- remap Super operator + SecretSuccession ----\n'
ECOMAE_CONFIRM_REMAP_SUPER_OPERATOR=YES bash scripts/cloudpanel_remap_super_operator_yawer.sh
if [[ -f scripts/cloudpanel_sync_secret_succession_from_php.sh ]]; then
  ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES \
  ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
    bash scripts/cloudpanel_sync_secret_succession_from_php.sh
else
  systemctl restart ecomae-platform.service
fi

if [[ -f scripts/wait_for_aspnet_health.sh ]]; then
  bash scripts/wait_for_aspnet_health.sh || true
fi

printf '\n---- probe health + logout (cookie values redacted) ----\n'
curl -sS -o /dev/null -w 'health %{http_code}\n' --max-time 15 "${ASPNET_URL}/health" || true
curl -sS -D - -o /dev/null --max-redirs 0 --max-time 15 \
  -H 'Host: www.ecomae.com' \
  -H 'X-Forwarded-Proto: https' \
  -H 'Cookie: admin_session=probe-token; admin_u_id=1' \
  "${ASPNET_URL}/erp/logout" 2>/dev/null \
  | awk 'BEGIN{IGNORECASE=1} /^HTTP\// {print} /^Location:/ {print} /^X-EcomAE-Logout:/ {print} /^Set-Cookie:/ { gsub(/=[^;]*/, "=***"); print }' \
  || true

printf '\nRESULT=OK non-ERP binary published from %s\n' "$BRANCH"
printf 'Sign in: https://www.ecomae.com/erp/login as yawer@ecomae.com\n'
printf 'Password: the same one previously used for ecomaedxb@gmail.com\n'
printf 'Logout check: https://www.ecomae.com/erp/logout then refresh /erp — must stay on /erp/login\n'
printf 'Do not use a tenant host for Super admin.\n'
printf 'PHP remains fallback. This script does not cut over nginx.\n'
