#!/usr/bin/env bash
# ONE paste for finish-pending ePartsCart + LifeOS P0.
# Prior "External action completed" acks left live stale:
#   CP POST → tenant_db_unbound, bunches unbound, clients-board 302→login.
# This wrapper FAILS unless public gates prove green. Paste PASTE_ME_* + RESULT=.
#
# As root on CloudPanel:
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/finish-pending-epartscart-lifeos-7b3b/scripts/cloudpanel_FINISH_PENDING_EPARTSCART_LIFEOS_NOW.sh'
#   TMP=/tmp/finish-pending-epartscart-lifeos-now.sh
#   curl -fsSL "$URL" -o "$TMP"
#   grep -q FINISH_PENDING_EPARTSCART_LIFEOS_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   bash "$TMP" 2>&1 | tee /root/finish-pending-epartscart-lifeos-now.log
#   grep -E 'RESULT=|PASTE_ME_|GATE_|BOUND_|POST_LOGIN|SHA=|clients-board|ERROR' /root/finish-pending-epartscart-lifeos-now.log | tail -120
set -euo pipefail

printf '======== FINISH_PENDING_EPARTSCART_LIFEOS_NOW ========\n'
printf 'HOST=%s DATE_UTC=%s UID=%s\n' \
  "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$(id -u)"
die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" -eq 0 ]] || die "must_run_as_root_on_CloudPanel"

BRANCH="${ECOMAE_BRANCH:-cursor/finish-pending-epartscart-lifeos-7b3b}"
export ECOMAE_BRANCH="$BRANCH" ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

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

# Prefer STANDALONE bind first (no dependency on nuclear succeeding for SQL).
STANDALONE="$REPO/scripts/cloudpanel_EPARTSCART_BIND_DOCPART_STANDALONE.sh"
[[ -f "$STANDALONE" ]] || die "missing_BIND_DOCPART_STANDALONE"
chmod +x "$STANDALONE"
printf '\n---- BIND_DOCPART_STANDALONE ----\n'
set +e
bash "$STANDALONE" 2>&1 | tee /root/finish-pending-bind-standalone.log
BIND_RC=${PIPESTATUS[0]}
set -e
printf 'bind_standalone_exit=%s\n' "$BIND_RC"
grep -E 'RESULT=|BOUND_|RESOLVER_|POST_LOGIN|GATE_|PASTE_ME_' /root/finish-pending-bind-standalone.log | tail -40 || true
[[ "$BIND_RC" -eq 0 ]] || die "bind_standalone_failed — send /root/finish-pending-bind-standalone.log"

PUBLISH="$REPO/scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh"
[[ -f "$PUBLISH" ]] || die "missing_LIVE_PUBLISH_NOW"
chmod +x "$PUBLISH"
printf '\n---- LIVE_PUBLISH_NOW (republish :5100 with docpart fallback + LifeOS fix) ----\n'
set +e
ECOMAE_BRANCH="$BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES bash "$PUBLISH" 2>&1 | tee /root/finish-pending-live-publish.log
PUB_RC=${PIPESTATUS[0]}
set -e
printf 'live_publish_exit=%s\n' "$PUB_RC"
grep -E 'RESULT=|SHA=|FINAL_GATE_|GATE_|ERROR|nuclear_exit' /root/finish-pending-live-publish.log | tail -40 || true
[[ "$PUB_RC" -eq 0 ]] || die "live_publish_failed — send /root/finish-pending-live-publish.log"

printf '\n---- public prove (agent-parity) ----\n'
fail=0

# CP login must not be tenant_db_unbound
REDIR="$(curl -sS -o /dev/null -w '%{redirect_url}' --max-time 25 -k \
  -X POST 'https://www.epartscart.com/cp/login' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'surface=cp' \
  --data-urlencode 'redirect=/cp' \
  --data-urlencode 'contact_type=email' \
  --data-urlencode 'contact=taxofin2025@gmail.com' \
  --data-urlencode 'password=__finish_pending_wrong__' || true)"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
if [[ "$REDIR" == *'tenant_db_unbound'* ]]; then
  printf 'GATE_BAD login_tenant_db_unbound\n'
  fail=$((fail + 1))
else
  printf 'GATE_OK login_not_tenant_db_unbound\n'
fi

# Bunches bound
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

# LifeOS clients-board public (200, not 302→login)
CB_HDR="$(mktemp)"
CB_CODE="$(curl -sS -D "$CB_HDR" -o /dev/null -w '%{http_code}' --max-time 25 -k \
  'https://lifeos.ecomae.com/lifeos/clients-board' || echo 000)"
CB_LOC="$(grep -i '^location:' "$CB_HDR" | head -1 | tr -d '\r' || true)"
if [[ "$CB_CODE" == "200" ]] && ! grep -qi 'lifeos/login' <<<"$CB_LOC"; then
  printf 'GATE_OK clients-board code=%s\n' "$CB_CODE"
  CLIENTS_BOARD=PUBLIC
else
  printf 'GATE_BAD clients-board code=%s loc=%s\n' "$CB_CODE" "$CB_LOC"
  CLIENTS_BOARD=STILL_GATED
  fail=$((fail + 1))
fi
rm -f "$CB_HDR"

CPJ="$(curl -sS -o /dev/null -w '%{http_code}' --max-time 25 -k \
  'https://lifeos.ecomae.com/lifeos/clients/cp' || echo 000)"
if [[ "$CPJ" == "200" ]]; then
  printf 'GATE_OK clients/cp code=%s\n' "$CPJ"
else
  printf 'GATE_BAD clients/cp code=%s\n' "$CPJ"
  fail=$((fail + 1))
fi

printf '======== PASTE_ME_BEGIN ========\n'
printf 'SHA=%s BRANCH=%s\n' "$SHA" "$BRANCH"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
printf 'BOUND_BUNCHES=%s\n' "$BOUND_BUNCHES"
printf 'CLIENTS_BOARD=%s code=%s\n' "$CLIENTS_BOARD" "$CB_CODE"
printf 'CLIENTS_CP_JSON=%s\n' "$CPJ"
printf 'fail=%s\n' "$fail"
printf '======== PASTE_ME_END ========\n'

[[ "$fail" -eq 0 ]] || die "public_gates_failed=$fail — live still stale; send /root/finish-pending-epartscart-lifeos-now.log"
printf 'RESULT=PASS FINISH_PENDING_EPARTSCART_LIFEOS_NOW SHA=%s BOUND_BUNCHES=YES CLIENTS_BOARD=PUBLIC\n' "$SHA"
exit 0
