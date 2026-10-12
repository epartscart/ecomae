#!/usr/bin/env bash
# GATED_DEPLOY_MAIN — CloudPanel publish with the health gate ON.
# PHP stays fallback. No nginx cutover. No PHP removal. No isolation emergency publish.
# After PASS, writes /etc/ecomae-aspnet/production.lock.json so later publishes
# cannot roll back to an older SHA or switch CP/ERP/BOS back to PHP-primary.
#
# Paste as root:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/reg-kyc-erp-cover-5dbd/scripts/cloudpanel_deploy_main_gated.sh'
#   TMP=/tmp/cloudpanel-deploy-main-gated.sh
#   curl -fsSL "$URL" -o "$TMP"
#   test -s "$TMP" || { echo RESULT=FAIL empty_download; exit 1; }
#   grep -q GATED_DEPLOY_MAIN "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   grep -q refused_emergency_publish "$TMP" || { echo RESULT=FAIL stale_download; exit 1; }
#   grep -q production.lock.json "$TMP" || { echo RESULT=FAIL stale_lock_helper; exit 1; }
#   export ECOMAE_BRANCH=cursor/reg-kyc-erp-cover-5dbd ECOMAE_EMERGENCY_PUBLISH=0
#   bash "$TMP" 2>&1 | tee /root/ecomae-gated-deploy.log
#   grep -E 'RESULT=|SHA=|GATE_|HEALTH_|ERROR|FAIL|HOST=' /root/ecomae-gated-deploy.log | tail -80
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/reg-kyc-erp-cover-5dbd}"
ECOMAE_ASPNET_RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
ECOMAE_ASPNET_ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"

die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }

printf '======== GATED_DEPLOY_MAIN ========\n'
printf 'HOST=%s\n' "$(hostname -f 2>/dev/null || hostname || echo unknown)"
printf 'DATE_UTC=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf 'UID=%s USER=%s\n' "$(id -u)" "$(id -un)"
printf 'ECOMAE_BRANCH=%s\n' "$ECOMAE_BRANCH"

[[ "$(id -u)" -eq 0 ]] || die "must run as root on CloudPanel"

if [[ "${ECOMAE_EMERGENCY_PUBLISH:-0}" == "1" ]]; then
  die "refused_emergency_publish — this script requires ECOMAE_EMERGENCY_PUBLISH=0 (health gate stays on)"
fi
if [[ "${ECOMAE_CONFIRM_PHP_DECOMMISSION:-}" == "YES" ]]; then
  die "refused_php_decommission — PHP stays fallback"
fi
if [[ "$ECOMAE_BRANCH" == *isolation* ]]; then
  die "refused_isolation_branch — isolation merge only via gated main, never emergency-publish"
fi

export ECOMAE_EMERGENCY_PUBLISH=0
export ECOMAE_RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-1}"
export ECOMAE_RUN_NGINX_RELOAD=0
export ECOMAE_INSTALL_DIAGNOSTICS_NGINX=0
export ECOMAE_ENABLE_PRICE_LOOKUP_SHADOW=0
unset ECOMAE_CONFIRM_PHP_DECOMMISSION || true

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then
    REPO="$d"
    break
  fi
done
if [[ -z "$REPO" ]]; then
  printf 'Repo not found. Cloning to /opt/ecomae-aspnet-source ...\n'
  mkdir -p /opt
  rm -rf /opt/ecomae-aspnet-source
  git clone "$ECOMAE_GIT_URL" /opt/ecomae-aspnet-source
  REPO=/opt/ecomae-aspnet-source
fi

cd "$REPO"
printf 'REPO=%s\n' "$REPO"
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH" || die "git fetch origin $ECOMAE_BRANCH failed"
INCOMING="$(git rev-parse "origin/$ECOMAE_BRANCH")"
if [[ -f scripts/cloudpanel_production_lock.sh ]]; then
  bash scripts/cloudpanel_production_lock.sh check "$INCOMING" "$REPO" \
    || die "production_lock — incoming SHA is older than the locked production SHA"
fi
git checkout -f "$ECOMAE_BRANCH" || die "git checkout failed"
git reset --hard "origin/$ECOMAE_BRANCH" || die "git reset failed"
SHA="$(git rev-parse --short HEAD)"
FULL="$(git rev-parse HEAD)"
printf 'SHA=%s FULL=%s\n' "$SHA" "$FULL"
[[ -f scripts/cloudpanel_production_lock.sh ]] || die "production lock helper missing after checkout"

[[ -f scripts/cloudpanel_production_deploy_foundation.sh ]] || die "production deploy script missing after checkout"
[[ -f "${ECOMAE_ASPNET_ENV_DIR}/platform.env" ]] || die "missing ${ECOMAE_ASPNET_ENV_DIR}/platform.env"
if grep -Eq 'User=<db_user>|Password=<db_password>|<db_user>|<db_password>' "${ECOMAE_ASPNET_ENV_DIR}/platform.env"; then
  die "platform.env still has placeholders"
fi

printf 'GATE_HEALTH=ON GATE_CUTOVER=OFF GATE_PHP_REMOVAL=OFF GATE_ISOLATION=OFF GATE_PRODUCTION_LOCK=ON GATE_PHP_PRIMARY=BLOCKED\n'
sudo ECOMAE_BRANCH="$ECOMAE_BRANCH" \
  ECOMAE_RUN_SYSTEMD="$ECOMAE_RUN_SYSTEMD" \
  ECOMAE_EMERGENCY_PUBLISH=0 \
  ECOMAE_INSTALL_DIAGNOSTICS_NGINX=0 \
  ECOMAE_RUN_NGINX_RELOAD=0 \
  ECOMAE_ENABLE_PRICE_LOOKUP_SHADOW=0 \
  ECOMAE_ASPNET_RELEASE_ROOT="$ECOMAE_ASPNET_RELEASE_ROOT" \
  ECOMAE_ASPNET_ENV_DIR="$ECOMAE_ASPNET_ENV_DIR" \
  bash scripts/cloudpanel_production_deploy_foundation.sh

ECOMAE_EXPECTED_RELEASE_SHA="$FULL" \
  ECOMAE_HEALTH_WAIT_SECONDS="${ECOMAE_HEALTH_WAIT_SECONDS:-90}" \
  bash scripts/wait_for_aspnet_health.sh || die "health_gate — /health or /ready or RELEASE_SHA mismatch"

LIVE_SHA="$(tr -d '[:space:]' < "${ECOMAE_ASPNET_RELEASE_ROOT}/current/platform/RELEASE_SHA" 2>/dev/null || true)"
[[ "$LIVE_SHA" == "$FULL" ]] || die "release_sha_mismatch expected=$FULL live=${LIVE_SHA:-missing}"

HEALTH_CODE="$(curl -sS -m 8 -o /tmp/ecomae-gated-health.json -w '%{http_code}' http://127.0.0.1:5100/health || true)"
READY_CODE="$(curl -sS -m 8 -o /tmp/ecomae-gated-ready.json -w '%{http_code}' http://127.0.0.1:5100/ready || true)"
RELEASE_CODE="$(curl -sS -m 8 -o /tmp/ecomae-gated-release.json -w '%{http_code}' http://127.0.0.1:5100/migration/release || true)"
printf 'HEALTH_LOOPBACK=%s READY_LOOPBACK=%s RELEASE_LOOPBACK=%s\n' "$HEALTH_CODE" "$READY_CODE" "$RELEASE_CODE"
[[ "$HEALTH_CODE" == "200" && "$READY_CODE" == "200" && "$RELEASE_CODE" == "200" ]] || die "loopback_not_200"

bash scripts/cloudpanel_production_lock.sh write "$FULL" "$ECOMAE_BRANCH" \
  || die "production_lock_write_failed"
printf 'PHP remains fallback. Do not enable broad nginx cutover. Do not remove PHP.\n'
printf 'PRODUCTION_LOCK written. Later publishes cannot roll back to an older SHA.\n'
printf 'PHP-primary restore is blocked. PHP file delete stays REFUSE until ReadyToRemovePhp=true.\n'
printf 'TEST_MATRIX=\n'
printf '  platform_erp        https://www.ecomae.com/erp\n'
printf '  hybrid_cp           https://www.epartscart.com/cp\n'
printf '  hybrid_erp          https://www.epartscart.com/erp\n'
printf '  jewellery_industry  https://www.thejewellerytrend.com/erp\n'
printf '  construction_ind    https://construction.ecomae.com/erp\n'
printf '  jewellery_showcase  https://jewellery.ecomae.com/erp\n'
printf '  demo_industry_hub   https://industries.ecomae.com/erp\n'
printf '  erp_only_login      https://www.ecomae.com/cp/client-erp/asap/\n'
printf '  erp_only_shell      https://www.ecomae.com/erp\n'
printf '  finance_pl          https://www.ecomae.com/erp/report-center-app?tab=pl\n'
printf '  finance_bs          https://www.ecomae.com/erp/report-center-app?tab=balance_sheet\n'
printf '  finance_coa         https://www.ecomae.com/erp/coa-accounts-app\n'
if [[ -f scripts/cloudpanel_onboard_four_erp_only.sh ]]; then
  printf 'ONBOARD_FOUR_ERP_ONLY after publish (idempotent)...\n'
  ECOMAE_CONFIRM_ONBOARD_ERP_ONLY=YES bash scripts/cloudpanel_onboard_four_erp_only.sh \
    || printf 'WARN onboard_four_erp_only failed — create ASAP/INDJ/FITO/REAL from /cp after Super CP login\n'
fi
if [[ -f scripts/cloudpanel_grant_erp_only_shop_dbs.sh ]]; then
  printf 'GRANT_ERP_ONLY_SHOP_DBS after publish (CREATE + registry GRANT)...\n'
  ECOMAE_CONFIRM_GRANT_ERP_ONLY=YES bash scripts/cloudpanel_grant_erp_only_shop_dbs.sh \
    || printf 'WARN grant_erp_only_shop_dbs failed — Super CP password reset needs CloudPanel CREATE/GRANT\n'
fi
if [[ -f scripts/cloudpanel_prune_aspnet_releases.sh ]]; then
  printf 'PRUNE_ASPNET_RELEASES keep=3 (never current, never PHP)...\n'
  bash scripts/cloudpanel_prune_aspnet_releases.sh \
    || printf 'WARN prune_aspnet_releases failed — disk cleanup skipped\n'
fi
printf 'RESULT=PASS GATED_DEPLOY_MAIN SHA=%s\n' "$SHA"
