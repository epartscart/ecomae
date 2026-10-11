#!/usr/bin/env bash
# ONE CloudPanel paste: publish Super/tenant CP chrome isolation +
# industry.ecomae.com directory alias. Does not cut over nginx, does not
# delete PHP, does not print secrets.
#
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/cp-erp-chrome-isolation-5dbd/scripts/cloudpanel_deploy_cp_erp_chrome_isolation_now.sh)"
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

BRANCH="${ECOMAE_BRANCH:-cursor/cp-erp-chrome-isolation-5dbd}"
GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
REPO="${ECOMAE_REPO:-/opt/ecomae-aspnet-source}"
ASPNET_URL="${ECOMAE_ASPNET_URL:-http://127.0.0.1:5100}"

printf '======== CP/ERP CHROME ISOLATION DEPLOY ========\n'
printf 'HOST=%s DATE_UTC=%s\n' "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf 'BRANCH=%s REPO=%s\n' "$BRANCH" "$REPO"
printf 'PHP remains fallback. readyForPhpRemoval stays false.\n'

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
test -f aspnet/src/EcomAE.Platform/Presentation/SurfaceChromePolicy.cs
grep -q 'industry.ecomae.com' aspnet/src/EcomAE.Platform/Presentation/EcomaeIndustryShowcaseSnapshots.cs
grep -q 'ShowErpSwitcher' aspnet/src/EcomAE.Platform/Presentation/SurfaceChromePolicy.cs
grep -q 'id="epc_boc_topnav"' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpSuperCpBocTopnav.razor
printf 'OK source markers (isolation + industry alias) present\n'

dump_platform_journal() {
  printf '\n---- journalctl ecomae-platform (last 80; secrets redacted) ----\n'
  journalctl -u ecomae-platform.service -n 80 --no-pager 2>/dev/null \
    | sed -E 's/([Pp]assword|[Ss]ecret|[Tt]oken|[Pp]wd)=[^[:space:]]*/\1=***/g' || true
}
trap dump_platform_journal ERR

printf '\n---- publish ASP.NET (%s) ----\n' "$BRANCH"
# First publish of this binary can JIT past the old 60s prove window; /health
# used to run tenant-DB middleware and 500 → automatic rollback to main.
export ECOMAE_HEALTH_WAIT_SECONDS="${ECOMAE_HEALTH_WAIT_SECONDS:-180}"
export ECOMAE_HEALTH_POLL_SECONDS="${ECOMAE_HEALTH_POLL_SECONDS:-3}"
ECOMAE_BRANCH="$BRANCH" \
ECOMAE_REPO="$REPO" \
ECOMAE_EMERGENCY_PUBLISH="${ECOMAE_EMERGENCY_PUBLISH:-1}" \
  bash scripts/cloudpanel_find_and_redeploy.sh

if [[ -f scripts/cloudpanel_sync_secret_succession_from_php.sh ]]; then
  ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES \
  ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
    bash scripts/cloudpanel_sync_secret_succession_from_php.sh || systemctl restart ecomae-platform.service
else
  systemctl restart ecomae-platform.service
fi

if [[ -f scripts/wait_for_aspnet_health.sh ]]; then
  bash scripts/wait_for_aspnet_health.sh || true
fi

printf '\n---- prove published source + :5100 health ----\n'
curl -sS -o /dev/null -w 'health %{http_code}\n' --max-time 15 "${ASPNET_URL}/health" || true
DLL="$(find /var/www/ecomae-aspnet -name 'EcomAE.Platform.dll' 2>/dev/null | head -n 1 || true)"
if [[ -n "$DLL" ]] && command -v strings >/dev/null 2>&1; then
  if strings "$DLL" | grep -q 'SurfaceChromePolicy'; then
    printf 'PASS  published DLL has SurfaceChromePolicy\n'
  else
    printf 'WARN  SurfaceChromePolicy string not visible in DLL (still check Super /cp/app)\n'
  fi
  if strings "$DLL" | grep -q 'industry.ecomae.com'; then
    printf 'PASS  published DLL has industry.ecomae.com alias\n'
  else
    printf 'WARN  industry.ecomae.com string not visible in DLL\n'
  fi
fi

printf '\nRESULT=OK isolation branch published from %s\n' "$BRANCH"
printf 'After Cloudflare cache: hard-refresh\n'
printf '  https://www.ecomae.com/cp/app   (BOC only; ERP should be one switcher)\n'
printf '  https://www.ecomae.com/cp/tenants-app   (BOC full-width)\n'
printf 'Industry pages stay 525 until origin TLS is fixed:\n'
printf '  https://industry.ecomae.com/\n'
printf '  https://beauty.ecomae.com/skincare-facials\n'
printf 'PHP remains fallback. This script does not cut over nginx.\n'
