#!/usr/bin/env bash
# FORCE LIVE — set ALL product site classes to ASP.NET-primary on CloudPanel.
# Merge alone does NOT republish :5100 or install classic-entry.
#
# Site classes covered:
#   - Super CP: www.ecomae.com + cp.ecomae.com (/ /cp /erp /bos /ip /lifeos /marketing)
#   - Named tenants: epartscart, electronicae, stylenlook, thejewellerytrend, taxofinca
#   - Industry showcase: 28 *.ecomae.com hosts (when present in nginx)
#   - LifeOS: lifeos.ecomae.com
#
# CloudPanel root paste (this branch):
#   ECOMAE_BRANCH=cursor/full-arch-link-remap-audit-7b3b ECOMAE_SKIP_LIFEOS_MP4=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/full-arch-link-remap-audit-7b3b/scripts/cloudpanel_FORCE_LIVE_ALL_SITES.sh)" \
#     2>&1 | tee /root/force-live-all-sites.log
#
# After merge to main:
#   ECOMAE_BRANCH=main ECOMAE_SKIP_LIFEOS_MP4=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_ALL_SITES.sh)" \
#     2>&1 | tee /root/force-live-all-sites.log
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== FORCE LIVE ALL SITES (%s) ========\n' "$ECOMAE_BRANCH"
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
FULL="$(git rev-parse HEAD)"
printf 'REPO=%s SHA=%s FULL=%s\n' "$REPO" "$SHA" "$FULL"

chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh \
  scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh \
  scripts/cloudpanel_FORCE_LIVE_LIFEOS_502_RECOVER.sh \
  scripts/cloudpanel_install_classic_entry_aspnet_primary.sh \
  scripts/cloudpanel_probe_classic_entry_aspnet_primary.sh 2>/dev/null || true

# 1) Republish ASP.NET (:5100) via storefront FORCE_LIVE core (inner may WARN on public prove).
printf '\n---- [1/4] FORCE_LIVE_NOW (publish :5100) ----\n'
set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/force-live-all-sites-inner.log
FORCE_RC=${PIPESTATUS[0]}
set -e
printf 'FORCE_LIVE_NOW exit=%s\n' "$FORCE_RC"

# 2) Classic-entry ASP.NET-primary on www + all named tenants (+ industry blocks when present).
printf '\n---- [2/4] classic-entry --all-hosts ----\n'
set +e
ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
  bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts \
  2>&1 | tee -a /root/force-live-all-sites-inner.log
CLASSIC_RC=${PIPESTATUS[0]}
set -e
printf 'classic-entry exit=%s\n' "$CLASSIC_RC"

# 3) www marketing prove (epm-hub) when marketing wrapper exists.
printf '\n---- [3/4] www marketing prove ----\n'
set +e
if [[ -x scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh ]]; then
  # Skip re-publish; classic-entry already applied — call prove section via env if supported,
  # otherwise re-run wrapper (idempotent publish).
  ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
    bash scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh \
    2>&1 | tee -a /root/force-live-all-sites-inner.log
  MKT_RC=${PIPESTATUS[0]}
else
  MKT_RC=0
fi
set -e
printf 'www marketing exit=%s\n' "$MKT_RC"

# 4) LifeOS 502 recover when LifeOS wrapper exists.
printf '\n---- [4/4] LifeOS recover ----\n'
set +e
if [[ -x scripts/cloudpanel_FORCE_LIVE_LIFEOS_502_RECOVER.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
    bash scripts/cloudpanel_FORCE_LIVE_LIFEOS_502_RECOVER.sh \
    2>&1 | tee -a /root/force-live-all-sites-inner.log
  LIFEOS_RC=${PIPESTATUS[0]}
else
  LIFEOS_RC=0
fi
set -e
printf 'LifeOS recover exit=%s\n' "$LIFEOS_RC"

# Public probe matrix (non-fatal; write summary for operator).
printf '\n---- public probe matrix ----\n'
PROBE_OUT=/root/force-live-all-sites-probe.txt
: > "$PROBE_OUT"
probe_one() {
  local url="$1"
  local code
  code="$(curl -sS -o /tmp/all-sites-probe.body -w '%{http_code}' --max-time 20 -A 'ecomae-force-live-all-sites' "$url" || echo 000)"
  local title
  title="$(grep -oiE '<title>[^<]+' /tmp/all-sites-probe.body 2>/dev/null | head -n1 | sed 's/<title>//I' || true)"
  printf '%s %s %s\n' "$code" "$url" "${title:0:80}" | tee -a "$PROBE_OUT"
}

probe_one "https://www.ecomae.com/"
probe_one "https://www.ecomae.com/cp"
probe_one "https://www.ecomae.com/erp"
probe_one "https://www.ecomae.com/bos"
probe_one "https://cp.ecomae.com/cp"
probe_one "https://www.epartscart.com/"
probe_one "https://www.epartscart.com/cp"
probe_one "https://www.epartscart.com/erp"
probe_one "https://www.epartscart.com/bos"
probe_one "https://www.epartscart.com/php-reference/cp"
probe_one "https://lifeos.ecomae.com/"
probe_one "https://agriculture.ecomae.com/"
probe_one "https://jewellery.ecomae.com/"
probe_one "https://healthcare.ecomae.com/"

if [[ -x scripts/cloudpanel_probe_classic_entry_aspnet_primary.sh ]]; then
  set +e
  bash scripts/cloudpanel_probe_classic_entry_aspnet_primary.sh --all-hosts \
    2>&1 | tee /root/force-live-all-sites-classic-probe.log
  set -e
fi

printf '\n======== FORCE LIVE ALL SITES DONE SHA=%s ========\n' "$SHA"
printf 'Inner log: /root/force-live-all-sites-inner.log\n'
printf 'Probe:     %s\n' "$PROBE_OUT"
printf 'Board:     GET /migration/all-sites-aspnet-primary\n'
printf 'Policy:    cutoverAllowed=false; writes PHP-authoritative; PHP via /php-reference/* only\n'

# Exit non-zero only if classic-entry hard-failed (publish WARNs are common).
if [[ "$CLASSIC_RC" -ne 0 ]]; then
  printf 'RESULT=FAIL classic-entry=%s\n' "$CLASSIC_RC" >&2
  exit "$CLASSIC_RC"
fi
printf 'RESULT=OK (verify probe matrix for splash/502)\n'
exit 0
