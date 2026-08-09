#!/usr/bin/env bash
# ONE paste for: https://www.epartscart.com/cp/login?error=tenant_db_unbound
#
# Live still fails after #975 merge because merge ≠ republish :5100, and prior
# binds used registry MySQL user which could not SEE docpart.users.
# This script: checkout main → BIND_DOCPART_STANDALONE (root+GRANT) →
# LIVE_PUBLISH_NOW → prove CP POST is NOT tenant_db_unbound.
#
# Paste as root (must paste RESULT= / PASTE_ME back — silent UI complete = FAIL):
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/fix-cp-login-unbound-now-7b3b/scripts/cloudpanel_FIX_EPARTSCART_CP_LOGIN_UNBOUND_NOW.sh'
#   TMP=/tmp/fix-epartscart-cp-login-unbound-now.sh
#   curl -fsSL "$URL" -o "$TMP"
#   grep -q FIX_EPARTSCART_CP_LOGIN_UNBOUND_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   export ECOMAE_BRANCH=main ECOMAE_SKIP_LIFEOS_MP4=YES
#   bash "$TMP" 2>&1 | tee /root/fix-epartscart-cp-login-unbound-now.log
#   grep -E 'RESULT=|PASTE_ME_|GATE_|BOUND_|POST_LOGIN|SHA=|GRANT_|EMAIL_HIT|discovered_|ERROR|FAIL' /root/fix-epartscart-cp-login-unbound-now.log | tail -120
set -euo pipefail

printf '======== FIX_EPARTSCART_CP_LOGIN_UNBOUND_NOW ========\n'
printf 'HOST=%s DATE_UTC=%s UID=%s\n' \
  "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$(id -u)"
die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" -eq 0 ]] || die "must_run_as_root_on_CloudPanel"

BRANCH="${ECOMAE_BRANCH:-main}"
export ECOMAE_BRANCH="$BRANCH" ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"
DIAG_EMAIL="${ECOMAE_DIAG_EMAIL:-taxofin2025@gmail.com}"

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  [[ -n "$d" && -d "$d/.git" ]] && REPO="$d" && break
done
[[ -n "$REPO" ]] || die "repo_not_found — clone epartscart/ecomae under /opt or /root"

cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$BRANCH" || die "git_fetch_failed $BRANCH"
git checkout -f "$BRANCH" || die "git_checkout_failed"
git reset --hard "origin/$BRANCH" || die "git_reset_failed"
SHA="$(git rev-parse --short HEAD)"
FULL="$(git rev-parse HEAD)"
printf 'REPO=%s SHA=%s FULL=%s BRANCH=%s\n' "$REPO" "$SHA" "$FULL" "$BRANCH"

# Prefer STANDALONE first (root discover + GRANT). Must contain mysql_root_socket.
STANDALONE="$REPO/scripts/cloudpanel_EPARTSCART_BIND_DOCPART_STANDALONE.sh"
[[ -f "$STANDALONE" ]] || die "missing_BIND_DOCPART_STANDALONE"
grep -q 'mysql_root_socket' "$STANDALONE" || die "stale_STANDALONE_missing_root_discover — pull main/#975+"
chmod +x "$STANDALONE"
printf '\n---- BIND_DOCPART_STANDALONE (root discover + GRANT) ----\n'
set +e
ECOMAE_DIAG_EMAIL="$DIAG_EMAIL" bash "$STANDALONE" 2>&1 | tee /root/fix-cp-login-bind-standalone.log
BIND_RC=${PIPESTATUS[0]}
set -e
printf 'bind_standalone_exit=%s\n' "$BIND_RC"
grep -E 'RESULT=|BOUND_|RESOLVER_|POST_LOGIN|GATE_|GRANT_|EMAIL_HIT|discovered_|PASTE_ME_' \
  /root/fix-cp-login-bind-standalone.log | tail -50 || true
[[ "$BIND_RC" -eq 0 ]] || die "bind_standalone_failed — send /root/fix-cp-login-bind-standalone.log"

PUBLISH="$REPO/scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh"
[[ -f "$PUBLISH" ]] || die "missing_LIVE_PUBLISH_NOW"
chmod +x "$PUBLISH"
printf '\n---- LIVE_PUBLISH_NOW (republish :5100 — #975 db_pass + docpart fallback) ----\n'
set +e
ECOMAE_BRANCH="$BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES bash "$PUBLISH" 2>&1 | tee /root/fix-cp-login-live-publish.log
PUB_RC=${PIPESTATUS[0]}
set -e
printf 'live_publish_exit=%s\n' "$PUB_RC"
grep -E 'RESULT=|SHA=|FINAL_GATE_|GATE_|ERROR|nuclear_exit|resolved_shop' \
  /root/fix-cp-login-live-publish.log | tail -40 || true
[[ "$PUB_RC" -eq 0 ]] || die "live_publish_failed — send /root/fix-cp-login-live-publish.log"

printf '\n---- public prove (CP unbound gate) ----\n'
fail=0

REDIR="$(curl -sS -o /dev/null -w '%{redirect_url}' --max-time 25 -k \
  -X POST 'https://www.epartscart.com/cp/login' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'surface=cp' \
  --data-urlencode 'redirect=/cp' \
  --data-urlencode 'contact_type=email' \
  --data-urlencode "contact=${DIAG_EMAIL}" \
  --data-urlencode 'password=__cp_unbound_probe_wrong__' || true)"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
if [[ "$REDIR" == *'tenant_db_unbound'* ]]; then
  printf 'GATE_BAD login_still_tenant_db_unbound\n'
  fail=$((fail + 1))
else
  printf 'GATE_OK login_not_tenant_db_unbound\n'
fi

B="$(mktemp)"
BC="$(curl -sS -o "$B" -w '%{http_code}' --max-time 30 -k \
  'https://www.epartscart.com/storefront/search-bunches?article=OC47' || echo 000)"
if [[ "$BC" == "200" ]] && ! grep -q 'Tenant shop database is not bound' "$B"; then
  printf 'GATE_OK bunches BOUND code=%s\n' "$BC"
  BOUND_BUNCHES=YES
else
  printf 'GATE_BAD bunches code=%s\n' "$BC"
  head -c 220 "$B"; echo
  BOUND_BUNCHES=NO
  fail=$((fail + 1))
fi
rm -f "$B"

# Local Host-header prove (bypasses CDN cache)
LB="$(mktemp)"
LC="$(curl -sS -o "$LB" -w '%{http_code}' --max-time 25 \
  -H 'Host: www.epartscart.com' \
  'http://127.0.0.1:5100/storefront/search-bunches?article=OC90' || echo 000)"
if [[ "$LC" == "200" ]] && ! grep -q 'Tenant shop database is not bound' "$LB"; then
  printf 'GATE_OK local_5100_bunches BOUND\n'
else
  printf 'GATE_BAD local_5100_bunches code=%s\n' "$LC"
  head -c 180 "$LB"; echo
  fail=$((fail + 1))
fi
rm -f "$LB"

printf '======== PASTE_ME_BEGIN ========\n'
printf 'SHA=%s BRANCH=%s\n' "$SHA" "$BRANCH"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
printf 'BOUND_BUNCHES=%s\n' "$BOUND_BUNCHES"
printf 'fail=%s\n' "$fail"
printf '======== PASTE_ME_END ========\n'

[[ "$fail" -eq 0 ]] || die "public_gates_failed=$fail — send /root/fix-epartscart-cp-login-unbound-now.log"
printf 'RESULT=PASS FIX_EPARTSCART_CP_LOGIN_UNBOUND_NOW SHA=%s BOUND_BUNCHES=YES\n' "$SHA"
exit 0
