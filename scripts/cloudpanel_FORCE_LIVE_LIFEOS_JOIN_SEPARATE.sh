#!/usr/bin/env bash
# Republish :5100 and HARD-PROVE LifeOS join is public (separate from login).
#
# CloudPanel root paste:
#   ECOMAE_BRANCH=cursor/lifeos-join-login-separate-7b3b ECOMAE_SKIP_LIFEOS_MP4=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/lifeos-join-login-separate-7b3b/scripts/cloudpanel_FORCE_LIVE_LIFEOS_JOIN_SEPARATE.sh)" \
#     2>&1 | tee /root/force-live-lifeos-join-separate.log
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/lifeos-join-login-separate-7b3b}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

LIFEOS_BASE="${ECOMAE_LIFEOS_BASE:-https://lifeos.ecomae.com}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== FORCE LIVE LIFEOS JOIN SEPARATE (%s) ========\n' "$ECOMAE_BRANCH"
if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
if [[ -z "$REPO" ]]; then
  printf 'ERROR: ecomae git checkout not found under /opt or /root\n' >&2
  exit 1
fi

cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
printf 'REPO=%s SHA=%s\n' "$REPO" "$SHA"

# Guard: source tree must contain the public-join markers before publish.
if ! grep -Fq 'join without signing' aspnet/src/EcomAE.Platform/Components/Pages/LifeOsJoinApp.razor; then
  printf 'ERROR: checkout missing public-join copy — wrong branch?\n' >&2
  exit 1
fi
if ! awk '/IsPublicLifeOs/,/IsPersonalLifeOs/' \
      aspnet/src/EcomAE.Platform/Middleware/LifeOsPersonalAuthGateMiddleware.cs \
      | grep -Fq 'LifeOsJoin'; then
  printf 'ERROR: LifeOsJoin not in IsPublicLifeOs — refuse publish\n' >&2
  exit 1
fi
if awk '/IsPersonalLifeOs/,/^[[:space:]]*private static bool WantsJson/' \
      aspnet/src/EcomAE.Platform/Middleware/LifeOsPersonalAuthGateMiddleware.cs \
      | grep -Fq 'LifeOsJoin'; then
  printf 'ERROR: LifeOsJoin still listed under IsPersonalLifeOs — refuse publish\n' >&2
  exit 1
fi

if [[ ! -x scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh || true
fi

set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/force-live-lifeos-join-separate-inner.log
FORCE_RC=${PIPESTATUS[0]}
set -e
printf 'FORCE_LIVE_NOW exit=%s (storefront RESULT may WARN; join prove below is authoritative)\n' "$FORCE_RC"

systemctl restart ecomae-platform.service || true
sleep 5

printf '\n== LifeOS join/login separate hard prove ==\n'
fail=0
prove_code() {
  local name="$1" url="$2" want="$3"
  local code
  code="$(curl -4 -sS -o /tmp/epc-prove.body -w '%{http_code}' --connect-timeout 20 -A 'Mozilla/5.0' \
    -D /tmp/epc-prove.hdr "${url}?epc_prove=$(date +%s)" || echo 000)"
  if [[ "$code" != "$want" ]]; then
    printf 'FAIL %s http=%s want=%s url=%s\n' "$name" "$code" "$want" "$url"
    head -20 /tmp/epc-prove.hdr || true
    fail=1
    return 1
  fi
  printf 'PASS %s http=%s\n' "$name" "$code"
  return 0
}

prove_needle() {
  local name="$1" url="$2" needle="$3"
  local body code
  code="$(curl -4 -sS -L -o /tmp/epc-prove.body -w '%{http_code}' --connect-timeout 20 -A 'Mozilla/5.0' \
    "${url}?epc_prove=$(date +%s)" || echo 000)"
  body="$(cat /tmp/epc-prove.body 2>/dev/null || true)"
  if [[ "$code" != "200" ]]; then
    printf 'FAIL %s http=%s url=%s\n' "$name" "$code" "$url"
    fail=1
    return 1
  fi
  if ! grep -Fq "$needle" <<<"$body"; then
    printf 'FAIL %s missing %q\n' "$name" "$needle"
    fail=1
    return 1
  fi
  printf 'PASS %s\n' "$name"
}

# Must NOT redirect to login.
hdr="$(mktemp)"
code="$(curl -4 -sS -D "$hdr" -o /dev/null -w '%{http_code}' --connect-timeout 20 -A 'Mozilla/5.0' \
  "${LIFEOS_BASE}/lifeos/join?epc_prove=$(date +%s)" || echo 000)"
loc="$(grep -i '^location:' "$hdr" | tr -d '\r' | awk '{print $2}' | head -1 || true)"
rm -f "$hdr"
if [[ "$code" != "200" ]]; then
  printf 'FAIL join-page http=%s location=%s (still gated?)\n' "$code" "${loc:-none}"
  fail=1
elif [[ -n "$loc" ]] && grep -qi 'login' <<<"$loc"; then
  printf 'FAIL join-page still redirects to login: %s\n' "$loc"
  fail=1
else
  printf 'PASS join-page http=200 no-login-redirect\n'
fi

prove_needle join-copy "${LIFEOS_BASE}/lifeos/join" 'join without signing'
prove_needle login-copy "${LIFEOS_BASE}/lifeos/login" 'existing ecomae'
prove_needle login-join-link "${LIFEOS_BASE}/lifeos/login" 'Go to Join'

# Anonymous POST must create a client (not 401 lifeos_login_required).
post_body="$(mktemp)"
post_code="$(curl -4 -sS -o "$post_body" -w '%{http_code}' --connect-timeout 25 -A 'Mozilla/5.0' \
  -X POST "${LIFEOS_BASE}/lifeos/join?epc_prove=$(date +%s)" \
  -H 'content-type: application/json' -H 'accept: application/json' \
  -d '{"displayName":"ForceLiveProve","country":"United Arab Emirates","countryCode":"AE","joinSource":"force-live"}' \
  || echo 000)"
if [[ "$post_code" != "200" ]]; then
  printf 'FAIL join-post http=%s body=%s\n' "$post_code" "$(head -c 240 "$post_body")"
  fail=1
elif grep -Fq 'lifeos_login_required' "$post_body"; then
  printf 'FAIL join-post still login_required\n'
  fail=1
elif ! grep -Fq '"ok":true' "$post_body" && ! grep -Fq '"ok": true' "$post_body"; then
  printf 'FAIL join-post missing ok:true body=%s\n' "$(head -c 240 "$post_body")"
  fail=1
else
  printf 'PASS join-post anonymous 200\n'
fi
rm -f "$post_body"

# Local origin sanity (bypass CF).
prove_code local-join "http://127.0.0.1:5100/lifeos/join" "200" || true
if curl -4 -sS -D- -o /dev/null --max-time 10 http://127.0.0.1:5100/lifeos/join 2>/dev/null \
    | grep -qi 'location:.*login'; then
  printf 'FAIL local :5100/lifeos/join still redirects to login — publish did not pick this branch\n'
  fail=1
fi

if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL — join still gated behind login (SHA=%s)\n' "$SHA"
  printf 'Hints:\n'
  printf '  git -C %s rev-parse --short HEAD\n' "$REPO"
  printf '  systemctl status ecomae-platform.service --no-pager | head -20\n'
  printf '  curl -sI http://127.0.0.1:5100/lifeos/join | head -15\n'
  exit 1
fi

printf '\nRESULT=PASS — LifeOS join public / login separate (SHA=%s)\n' "$SHA"
printf 'Open: %s/lifeos/join  and  %s/lifeos/login\n' "$LIFEOS_BASE" "$LIFEOS_BASE"
exit 0
