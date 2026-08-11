#!/usr/bin/env bash
# Force ePartsCart PHP CHPU part pages onto ASP.NET (same URL).
#
# Live defect (2026-08-11):
#   https://www.epartscart.com/en/parts/TOYOTA/1310154101
#   → full PHP nero (templates/nero, ajax_getProductsOfBunch.php, no x-ecomae-platform)
#   while / and /storefront/search-app already return x-ecomae-platform: primary.
#
# Orders completing via PHP writes remain expected until cutoverAllowed=true.
# Product UI for /en/parts/* must not stay on PHP docroot.
#
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/epartscart-parts-chpu-aspnet-7b3b/scripts/cloudpanel_EPARTSCART_PARTS_CHPU_ASPNET_NOW.sh'
#   TMP=/tmp/epartscart-parts-chpu-aspnet-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/epartscart-parts-chpu-aspnet-7b3b
#   bash "$TMP" 2>&1 | tee /root/epartscart-parts-chpu-aspnet.log
#   grep -E 'RESULT=|GATE_|SHA=|PREFIX_|PARTS_' /root/epartscart-parts-chpu-aspnet.log | tail -80
#
# Silent "External action completed" without RESULT=PASS paste-back = FAIL.
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/epartscart-parts-chpu-aspnet-7b3b}"
PUBLIC_BASE="${ECOMAE_PUBLIC_BASE:-https://www.epartscart.com}"
PARTS_PATH="${ECOMAE_PARTS_PROBE:-/en/parts/TOYOTA/1310154101}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== EPARTSCART PARTS CHPU → ASP.NET NOW ========\n'
printf 'HOST=%s\n' "$(hostname -f 2>/dev/null || hostname || echo unknown)"
printf 'DATE_UTC=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf 'ECOMAE_BRANCH=%s\n' "$ECOMAE_BRANCH"
printf 'PROBE=%s%s\n' "$PUBLIC_BASE" "$PARTS_PATH"

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
printf 'REPO=%s SHA=%s\n' "$REPO" "$SHA"

if ! grep -q '@page "/en/parts/{PathBrand}/{PathArticle}"' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  printf 'ERROR: StorefrontSearchApp missing /en/parts CHPU route\n' >&2
  exit 1
fi
if ! grep -q 'location ^~ /en/parts/' \
  deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf; then
  printf 'ERROR: tenant classic-entry example missing /en/parts/ prefix\n' >&2
  exit 1
fi

# 1) Republish platform binary from this branch (FORCE_LIVE when available).
if [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  printf '\n---- FORCE_LIVE publish ----\n'
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/epartscart-parts-chpu-force-live.log | tail -60
  FL_RC=${PIPESTATUS[0]}
  set -e
  printf 'force_live_exit=%s\n' "$FL_RC"
fi

# 2) Install classic-entry (adds ^~ /en/parts/ + exact part_search onto epartscart server{}).
printf '\n---- classic-entry install (epartscart) ----\n'
export ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES
export ECOMAE_CLASSIC_ENTRY_TENANT_HOST="${ECOMAE_CLASSIC_ENTRY_TENANT_HOST:-www.epartscart.com}"
set +e
bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh 2>&1 | tee /root/epartscart-parts-chpu-classic-entry.log
CE_RC=${PIPESTATUS[0]}
set -e
printf 'classic_entry_exit=%s\n' "$CE_RC"
grep -E 'PREFIX_|OK installed|ERROR|PASS:' /root/epartscart-parts-chpu-classic-entry.log | tail -40 || true
[[ "$CE_RC" -eq 0 ]] || { printf 'RESULT=FAIL classic_entry_install\n'; exit 1; }

# 3) Prove public CHPU is ASP.NET primary (not PHP nero).
printf '\n---- prove %s ----\n' "$PARTS_PATH"
bash scripts/cloudpanel_EPARTSCART_PARTS_CHPU_PROVE.sh
PROVE_RC=$?
if [[ "$PROVE_RC" -ne 0 ]]; then
  printf 'RESULT=FAIL prove SHA=%s\n' "$SHA"
  exit 1
fi

printf '\nRESULT=PASS PARTS_CHPU_ASPNET=YES SHA=%s\n' "$SHA"
exit 0
