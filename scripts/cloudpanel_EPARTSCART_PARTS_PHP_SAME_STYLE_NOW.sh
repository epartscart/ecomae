#!/usr/bin/env bash
# Force-live publish ePartsCart so PHP same-style parts UI reaches public :5100.
#
# Do NOT run from ~ as `bash scripts/...` — that path only exists inside the git repo.
# CloudPanel root paste (after #986 on main):
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_EPARTSCART_PARTS_PHP_SAME_STYLE_NOW.sh'
#   TMP=/tmp/epartscart-parts-php-same-style-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=main
#   bash "$TMP" 2>&1 | tee /root/epartscart-parts-php-same-style.log
#   grep -E 'RESULT=|GATE_|SHA=|HOST=' /root/epartscart-parts-php-same-style.log | tail -80
#
# Silent "External action completed" without RESULT=PASS paste-back = FAIL.
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
PUBLIC_BASE="${ECOMAE_PUBLIC_BASE:-https://www.epartscart.com}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART PARTS PHP SAME-STYLE FORCE LIVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo unknown)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "ECOMAE_BRANCH=${ECOMAE_BRANCH}"
note "Expect: epc-brand-picker-table + epc-part-type-split + ?v=20260811y"

REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
if [[ -z "$REPO" ]]; then
  mkdir -p /opt
  git clone "$ECOMAE_GIT_URL" /opt/ecomae-aspnet-source
  REPO=/opt/ecomae-aspnet-source
fi

cd "$REPO"
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
note "REPO=${REPO} SHA=${SHA}"

if ! grep -q 'epc-brand-picker-table' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  die "StorefrontSearchApp missing epc-brand-picker-table (wrong SHA / branch)"
fi
if ! grep -q 'epc-part-type-split' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  die "StorefrontSearchApp missing epc-part-type-split"
fi
if [[ ! -f scripts/cloudpanel_EPARTSCART_PARTS_PHP_SAME_STYLE_PROVE.sh ]]; then
  die "prove script missing in repo"
fi

# 1) Republish platform binary from this branch.
if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  note ""
  note "---- LIVE_PUBLISH ----"
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}" \
    bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 | tee /root/epartscart-parts-same-style-live-publish.log | tail -80
  LP_RC=${PIPESTATUS[0]}
  set -e
  note "live_publish_exit=${LP_RC}"
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  note ""
  note "---- FORCE_LIVE ----"
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/epartscart-parts-same-style-force-live.log | tail -80
  LP_RC=${PIPESTATUS[0]}
  set -e
  note "force_live_exit=${LP_RC}"
else
  die "no LIVE_PUBLISH / FORCE_LIVE helper in repo"
fi

# 2) Prove public picker + CHPU use PHP same-style markup.
note ""
note "---- prove public picker + CHPU ----"
set +e
bash scripts/cloudpanel_EPARTSCART_PARTS_PHP_SAME_STYLE_PROVE.sh 2>&1 | tee /root/epartscart-parts-php-same-style-prove.log
PROVE_RC=${PIPESTATUS[0]}
set -e
grep -E 'RESULT=|GATE_' /root/epartscart-parts-php-same-style-prove.log | tail -40 || true

if [[ "$PROVE_RC" -ne 0 ]]; then
  die "prove SHA=${SHA} (public still stale or markup missing)"
fi

note ""
note "RESULT=PASS PARTS_PHP_SAME_STYLE=YES SHA=${SHA} PUBLIC=${PUBLIC_BASE}"
exit 0
