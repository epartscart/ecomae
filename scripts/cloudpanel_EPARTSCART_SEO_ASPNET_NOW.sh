#!/usr/bin/env bash
# Publish + prove ePartsCart ASP.NET SEO parity (PHP epc_seo_indexing).
#
# Do NOT run from ~ as `bash scripts/...`.
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/epartscart-seo-aspnet-parity-7b3b/scripts/cloudpanel_EPARTSCART_SEO_ASPNET_NOW.sh'
#   TMP=/tmp/epartscart-seo-aspnet-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/epartscart-seo-aspnet-parity-7b3b
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-seo-aspnet-now.log
#   grep -E 'RESULT=|GATE_|SHA=' /root/epartscart-seo-aspnet-now.log | tail -60
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/epartscart-seo-aspnet-parity-7b3b}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART SEO ASP.NET FORCE LIVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo unknown)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "ECOMAE_BRANCH=${ECOMAE_BRANCH}"

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

grep -q 'PartsChpuTitle' aspnet/src/EcomAE.Platform/Presentation/StorefrontPublicSeo.cs \
  || die "StorefrontPublicSeo missing PartsChpuTitle"
grep -q 'Body-stream public SEO' aspnet/src/EcomAE.Platform/Components/Shared/PhpSurfaceHead.razor \
  || die "PhpSurfaceHead missing body-stream public SEO"
grep -q 'PageOwnsRobotsMeta' aspnet/src/EcomAE.Platform/Components/App.razor \
  || die "App.razor missing PageOwnsRobotsMeta"
grep -q 'ASP.NET-primary storefront SEO' aspnet/src/EcomAE.Platform/Components/Pages/CpSeoApp.razor \
  || die "CpSeoApp missing ASP.NET SEO status"

if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" \
  ECOMAE_SKIP_LIFEOS_MP4=YES \
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  ECOMAE_SKIP_CP_LOGIN_DIAG=YES \
  ECOMAE_ALLOW_PHP_REFERENCE_503=YES \
  ECOMAE_SOFT_JOURNEY_HOLDOUTS=YES \
    bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 \
    | tee /root/epartscart-seo-live-publish.log | tail -80
  set +e
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 \
    | tee /root/epartscart-seo-force-live.log | tail -60
else
  die "no LIVE_PUBLISH / FORCE_LIVE script"
fi

note "---- SEO PROVE ----"
bash scripts/cloudpanel_EPARTSCART_SEO_ASPNET_PROVE.sh 2>&1 \
  | tee /root/epartscart-seo-aspnet-prove.log
PROVE_RC=${PIPESTATUS[0]}
[[ "$PROVE_RC" -eq 0 ]] || die "SEO prove failed — see /root/epartscart-seo-aspnet-prove.log"

note "RESULT=PASS EPARTSCART_SEO_ASPNET=YES SHA=${SHA}"
exit 0
