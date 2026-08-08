#!/usr/bin/env bash
# Focused CloudPanel paste: bind ePartsCart shop db_name + status=live, restore
# php-reference, diagnose CP login. Always dumps portal rows; refuses silent PASS.
#
# Paste as root (after #972 merge — use main):
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_EPARTSCART_BIND_SHOP_DB_NOW.sh'
#   TMP=/tmp/epartscart-bind-shop-db-now.sh
#   curl -fsSL "$URL" -o "$TMP"
#   grep -q BIND_SHOP_DB_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   export ECOMAE_BRANCH=main
#   bash "$TMP" 2>&1 | tee /root/epartscart-bind-shop-db-now.log
#   grep -E 'RESULT=|BOUND_|CP_LOGIN_DIAG|GATE_|ERROR|SHA=|RESOLVER_|resolved_shop|DUMP|email_scan|POST_LOGIN|status=' /root/epartscart-bind-shop-db-now.log | tail -120
set -euo pipefail

printf '======== EPARTSCART BIND_SHOP_DB_NOW ========\n'
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
export ECOMAE_BRANCH
die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" -eq 0 ]] || die "must run as root"
printf 'HOST=%s DATE_UTC=%s\n' "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  [[ -n "$d" && -d "$d/.git" ]] && REPO="$d" && break
done
[[ -n "$REPO" ]] || die "repo not found"

cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$ECOMAE_BRANCH" || die "git fetch failed"
git checkout -f "$ECOMAE_BRANCH" || die "checkout failed"
git reset --hard "origin/$ECOMAE_BRANCH" || die "reset failed"
SHA="$(git rev-parse --short HEAD)"
printf 'SHA=%s BRANCH=%s\n' "$SHA" "$ECOMAE_BRANCH"

grep -q "status='live'" scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh \
  || die "status=live bind missing — wrong tree (need post-#972 status fix)"
grep -q 'email_scan' scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh \
  || die "email_scan discovery missing — wrong tree"
chmod +x scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh \
  scripts/cloudpanel_restore_php_reference_serving.sh \
  scripts/cloudpanel_diagnose_cp_login_user.sh 2>/dev/null || true

# Default shop schema for ePartsCart = shared docpart (PHP portal parity).
# Override with ECOMAE_EPARTSCART_SHOP_DB only when dedicated.
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
printf 'ECOMAE_EPARTSCART_SHOP_DB=%s\n' "$ECOMAE_EPARTSCART_SHOP_DB"

printf '\n---- [1] bind shop db_name + status=live ----\n'
set +e
ECOMAE_CONFIRM_FIX_EPARTSCART_PORTAL_TENANT_DB=YES \
ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
ECOMAE_DIAG_EMAIL="${ECOMAE_DIAG_EMAIL:-taxofin2025@gmail.com}" \
ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  bash scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh 2>&1 | tee /root/epartscart-bind-shop-inner.log
BIND_RC=${PIPESTATUS[0]}
set -e
printf 'portal_bind_exit=%s\n' "$BIND_RC"
grep -E 'RESULT=|RESOLVER_|resolved_shop|discovered_|email_scan|DUMP_|status' /root/epartscart-bind-shop-inner.log | tail -60 || true
[[ "$BIND_RC" -eq 0 ]] || die "portal bind failed — paste DUMP/email_scan lines; or set ECOMAE_EPARTSCART_SHOP_DB="

printf '\n---- [2] restore php-reference ----\n'
set +e
ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES \
  bash scripts/cloudpanel_restore_php_reference_serving.sh 2>&1 | tee -a /root/epartscart-bind-shop-inner.log | tail -40
set -e

sleep 2
fail=0

printf '\n---- [3] public gates ----\n'
B="$(mktemp)"
BC="$(curl -sS -o "$B" -w '%{http_code}' --max-time 30 -k \
  'https://www.epartscart.com/storefront/search-bunches?article=OC90' || echo 000)"
if [[ "$BC" == "200" ]] && ! grep -q 'Tenant shop database is not bound' "$B"; then
  printf 'GATE_OK  %s search-bunches BOUND\n' "$BC"
  printf 'BOUND_BUNCHES=YES\n'
else
  printf 'GATE_BAD %s search-bunches still unbound\n' "$BC"
  head -c 240 "$B"; echo
  printf 'BOUND_BUNCHES=NO\n'
  fail=$((fail + 1))
fi
rm -f "$B"

REDIR="$(curl -sS -o /dev/null -w '%{redirect_url}' --max-time 25 -k \
  -X POST 'https://www.epartscart.com/cp/login' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'surface=cp' \
  --data-urlencode 'redirect=/cp' \
  --data-urlencode 'contact_type=email' \
  --data-urlencode 'contact=taxofin2025@gmail.com' \
  --data-urlencode 'password=__wrong_probe__' || true)"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
if [[ "$REDIR" == *'tenant_db_unbound'* ]]; then
  printf 'GATE_BAD login still tenant_db_unbound\n'
  fail=$((fail + 1))
else
  printf 'GATE_OK  login no longer tenant_db_unbound (got %s)\n' "$REDIR"
fi

PR="$(mktemp)"
PC="$(curl -sS -o "$PR" -w '%{http_code}' --max-time 30 -k \
  'https://www.epartscart.com/php-reference/en/users/registration' || echo 000)"
if [[ "$PC" == "200" ]] && ! grep -q 'Archive paused' "$PR"; then
  printf 'GATE_OK  %s php-reference registration\n' "$PC"
else
  printf 'GATE_BAD %s php-reference still paused/failed\n' "$PC"
  head -c 120 "$PR"; echo
  fail=$((fail + 1))
fi
rm -f "$PR"

printf '\n---- [4] CP login diagnose ----\n'
set +e
ECOMAE_DIAG_EMAIL="${ECOMAE_DIAG_EMAIL:-taxofin2025@gmail.com}" \
ECOMAE_DIAG_HOST="${ECOMAE_DIAG_HOST:-www.epartscart.com}" \
  bash scripts/cloudpanel_diagnose_cp_login_user.sh 2>&1 | tee /root/epartscart-cp-login-diag.log | sed 's/^/CP_LOGIN_DIAG /'
DIAG_RC=${PIPESTATUS[0]}
set -e
printf 'cp_login_diag_exit=%s\n' "$DIAG_RC"

if [[ "$fail" -gt 0 ]]; then
  die "gates_failed=$fail diag_rc=$DIAG_RC SHA=$SHA — paste full grep of /root/epartscart-bind-shop-db-now.log"
fi
printf 'RESULT=PASS BIND_SHOP_DB_NOW SHA=%s diag_rc=%s\n' "$SHA" "$DIAG_RC"
exit 0
