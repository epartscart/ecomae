#!/usr/bin/env bash
# Bulletproof CloudPanel entrypoint — download this file, then run it.
# Prior operator "completed" acks left live stale (register-app 404). This
# wrapper prints HOST/DATE loudly, refuses empty trees, and delegates to
# cloudpanel_EPARTSCART_JOURNEY_NUCLEAR.sh (hard GATE_OK / RESULT=PASS).
#
# Paste as root (pre-merge of #972: use the PR branch URL + ECOMAE_BRANCH below;
# after merge: URL .../main/... and ECOMAE_BRANCH=main):
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/cp-login-tenant-db-credentials-7b3b/scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh'
#   TMP=/tmp/epartscart-live-publish-now.sh
#   curl -fsSL "$URL" -o "$TMP"
#   test -s "$TMP" || { echo RESULT=FAIL empty_download; exit 1; }
#   grep -q LIVE_PUBLISH_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   export ECOMAE_BRANCH=cursor/cp-login-tenant-db-credentials-7b3b ECOMAE_SKIP_LIFEOS_MP4=YES
#   bash "$TMP" 2>&1 | tee /root/epartscart-live-publish-now.log
#   grep -E 'RESULT=|PREFLIGHT|GATE_|ERROR|SHA=|HOST=|CP_LOGIN_DIAG' /root/epartscart-live-publish-now.log | tail -120
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
export ECOMAE_BRANCH ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }

printf '======== EPARTSCART LIVE_PUBLISH_NOW ========\n'
printf 'HOST=%s\n' "$(hostname -f 2>/dev/null || hostname || echo unknown)"
printf 'DATE_UTC=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf 'UID=%s USER=%s\n' "$(id -u)" "$(id -un)"
printf 'ECOMAE_BRANCH=%s\n' "$ECOMAE_BRANCH"
[[ "$(id -u)" -eq 0 ]] || die "must run as root on CloudPanel"

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  [[ -n "$d" && -d "$d/.git" ]] && REPO="$d" && break
done
[[ -n "$REPO" ]] || die "repo not found under /opt or /root — clone epartscart/ecomae first"

cd "$REPO"
printf 'REPO=%s\n' "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$ECOMAE_BRANCH" || die "git fetch origin $ECOMAE_BRANCH failed"
git checkout -f "$ECOMAE_BRANCH" || die "git checkout failed"
git reset --hard "origin/$ECOMAE_BRANCH" || die "git reset failed"
SHA="$(git rev-parse --short HEAD)"
FULL="$(git rev-parse HEAD)"
printf 'SHA=%s FULL=%s\n' "$SHA" "$FULL"

NUCLEAR="$REPO/scripts/cloudpanel_EPARTSCART_JOURNEY_NUCLEAR.sh"
[[ -x "$NUCLEAR" || -f "$NUCLEAR" ]] || die "nuclear script missing after checkout"
chmod +x "$NUCLEAR" \
  "$REPO/scripts/cloudpanel_EPARTSCART_CUSTOMER_JOURNEY_RECOVER.sh" \
  "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh" \
  "$REPO/scripts/cloudpanel_restore_php_reference_serving.sh" \
  "$REPO/scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh" \
  "$REPO/scripts/cloudpanel_diagnose_cp_login_user.sh" \
  "$REPO/scripts/cloudpanel_sync_secret_succession_from_php.sh" 2>/dev/null || true

# Keep MD5 login parity with PHP when secret is missing/stale (non-fatal if confirm flags unset).
if [[ "${ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION:-YES}" == "YES" ]]; then
  printf '\n---- sync SecretSuccession from PHP ----\n'
  set +e
  ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
    bash "$REPO/scripts/cloudpanel_sync_secret_succession_from_php.sh" 2>&1 | tee /root/epartscart-secret-sync.log | tail -40
  set -e
fi

printf 'systemctl ecomae-platform: %s\n' "$(systemctl is-active ecomae-platform.service 2>/dev/null || echo unknown)"
ss -lntp 2>/dev/null | grep -E ':5100\b' || printf 'WARN: nothing listening on :5100 before publish\n'

printf '\n---- launching NUCLEAR ----\n'
set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES bash "$NUCLEAR"
RC=$?
set -e
printf 'nuclear_exit=%s\n' "$RC"

# Echo a compact public re-check for the operator paste-back.
printf '\n---- public recheck ----\n'
for u in \
  /storefront/register-app \
  /en/users/registration \
  /en/users/login \
  /cp/login \
  /storefront/search-bunches?article=OC90 \
  /php-reference/en/users/registration
do
  code="$(curl -sS -o /tmp/epc-pub-recheck -w '%{http_code}' --max-time 25 -k "https://www.epartscart.com$u" || echo 000)"
  printf 'RECHECK %s %s\n' "$code" "$u"
done

printf '\n---- CP login diagnose (taxofin2025@gmail.com @ www.epartscart.com) ----\n'
set +e
ECOMAE_DIAG_EMAIL="${ECOMAE_DIAG_EMAIL:-taxofin2025@gmail.com}" \
ECOMAE_DIAG_HOST="${ECOMAE_DIAG_HOST:-www.epartscart.com}" \
  bash "$REPO/scripts/cloudpanel_diagnose_cp_login_user.sh" 2>&1 | tee /root/epartscart-cp-login-diag.log | sed 's/^/CP_LOGIN_DIAG /'
DIAG_RC=${PIPESTATUS[0]}
set -e
printf 'cp_login_diag_exit=%s\n' "$DIAG_RC"

if [[ "$RC" -ne 0 ]]; then
  die "nuclear_exit=$RC — send /root/epartscart-live-publish-now.log (or journey-nuclear.log)"
fi
printf 'RESULT=PASS LIVE_PUBLISH_NOW SHA=%s cp_login_diag_exit=%s\n' "$SHA" "$DIAG_RC"
exit 0
