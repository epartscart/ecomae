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

# Nuclear: if published DLL lacks the public-join marker, publish again from THIS checkout.
RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
CURRENT="$(readlink -f "$RELEASE_ROOT/current" 2>/dev/null || true)"
DLL="$CURRENT/platform/EcomAE.Platform.dll"
printf 'current_release=%s\n' "${CURRENT:-missing}"
# Marker lives in compiled middleware (.NET stores literals as UTF-16LE).
DLL_MARKER='X-EcomAE-LifeOs-Join'
dll_has_marker() {
  local f="$1"
  [[ -f "$f" ]] || return 1
  python3 - "$f" "$DLL_MARKER" <<'PY'
import sys
from pathlib import Path
data = Path(sys.argv[1]).read_bytes()
needle = sys.argv[2].encode("utf-16le")
sys.exit(0 if needle in data else 1)
PY
}

if ! dll_has_marker "$DLL"; then
  printf 'WARN: published DLL missing %s — nuclear republish from %s\n' "$DLL_MARKER" "$SHA" >&2
  STAMP="$(date -u +%Y%m%d%H%M%S)"
  RELEASE_DIR="$RELEASE_ROOT/releases/join-separate-$STAMP"
  mkdir -p "$RELEASE_DIR/platform"
  export DOTNET_ROOT="${DOTNET_ROOT:-/usr/share/dotnet}"
  export PATH="$DOTNET_ROOT:$PATH"
  if ! command -v dotnet >/dev/null 2>&1; then
    export DOTNET_ROOT="${HOME:-/root}/.dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
  fi
  dotnet publish aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj \
    -c Release -o "$RELEASE_DIR/platform" --nologo
  if ! dll_has_marker "$RELEASE_DIR/platform/EcomAE.Platform.dll"; then
    printf 'ERROR: nuclear publish still missing %s — wrong source tree\n' "$DLL_MARKER" >&2
    exit 1
  fi
  chown -R www-data:www-data "$RELEASE_DIR" || true
  ln -sfn "$RELEASE_DIR" "$RELEASE_ROOT/current"
  chown -h www-data:www-data "$RELEASE_ROOT/current" 2>/dev/null || true
  CURRENT="$(readlink -f "$RELEASE_ROOT/current")"
  DLL="$CURRENT/platform/EcomAE.Platform.dll"
  printf 'nuclear_current=%s\n' "$CURRENT"
fi

if ! dll_has_marker "$DLL"; then
  printf 'ERROR: %s still lacks %s\n' "$DLL" "$DLL_MARKER" >&2
  exit 1
fi
printf 'PASS dll-marker %s in %s\n' "$DLL_MARKER" "$DLL"

systemctl stop ecomae-platform.service || true
sleep 1
fuser -k 5100/tcp 2>/dev/null || true
pkill -f 'EcomAE.Platform.dll' 2>/dev/null || true
sleep 1
systemctl start ecomae-platform.service
sleep 6
systemctl is-active --quiet ecomae-platform.service || {
  printf 'ERROR: ecomae-platform.service not active\n' >&2
  journalctl -u ecomae-platform.service -n 60 --no-pager >&2 || true
  exit 1
}

printf '\n== LOCAL origin prove (must pass before public) ==\n'
LOCAL_HDR="$(mktemp)"
LOCAL_CODE="$(curl -4 -sS -D "$LOCAL_HDR" -o /tmp/epc-local-join.html -w '%{http_code}' --max-time 20 \
  "http://127.0.0.1:5100/lifeos/join?epc_prove=$(date +%s)" || echo 000)"
LOCAL_LOC="$(grep -i '^location:' "$LOCAL_HDR" | tr -d '\r' | awk '{print $2}' | head -1 || true)"
LOCAL_JOIN_HDR="$(grep -i '^x-ecomae-lifeos-join:' "$LOCAL_HDR" | tr -d '\r' | awk '{print $2}' | head -1 || true)"
printf 'local_join http=%s location=%s x-ecomae-lifeos-join=%s\n' "$LOCAL_CODE" "${LOCAL_LOC:-none}" "${LOCAL_JOIN_HDR:-none}"
if [[ "$LOCAL_CODE" != "200" ]] || grep -qi 'login' <<<"${LOCAL_LOC:-}"; then
  printf 'ERROR: local :5100/lifeos/join still gated — not publishing to Cloudflare yet\n' >&2
  head -30 "$LOCAL_HDR" >&2 || true
  rm -f "$LOCAL_HDR"
  exit 1
fi
if [[ "${LOCAL_JOIN_HDR:-}" != "public" ]]; then
  printf 'ERROR: missing X-EcomAE-LifeOs-Join: public on local join (stale binary)\n' >&2
  rm -f "$LOCAL_HDR"
  exit 1
fi
if ! grep -Fq 'join without signing' /tmp/epc-local-join.html; then
  printf 'ERROR: local join HTML missing public copy\n' >&2
  rm -f "$LOCAL_HDR"
  exit 1
fi
rm -f "$LOCAL_HDR"
printf 'PASS local-join public\n'

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
join_hdr_val="$(curl -4 -sS -D- -o /dev/null --connect-timeout 20 -A 'Mozilla/5.0' \
  "${LIFEOS_BASE}/lifeos/join?epc_prove=$(date +%s)" | grep -i '^x-ecomae-lifeos-join:' | tr -d '\r' | awk '{print $2}' | head -1 || true)"
if [[ "$code" != "200" ]]; then
  printf 'FAIL join-page http=%s location=%s (still gated?)\n' "$code" "${loc:-none}"
  fail=1
elif [[ -n "$loc" ]] && grep -qi 'login' <<<"$loc"; then
  printf 'FAIL join-page still redirects to login: %s\n' "$loc"
  fail=1
elif [[ "${join_hdr_val:-}" != "public" ]]; then
  printf 'FAIL join-page missing X-EcomAE-LifeOs-Join: public (got %s)\n' "${join_hdr_val:-none}"
  fail=1
else
  printf 'PASS join-page http=200 no-login-redirect join=public\n'
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
