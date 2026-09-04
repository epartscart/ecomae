#!/usr/bin/env bash
# Publish current ASP.NET + restore Classic CP/ERP php-reference twins.
# Merge alone does nothing until this runs as root on CloudPanel.
#
# Opens side-by-side:
#   https://www.ecomae.com/cp                 ↔  https://www.ecomae.com/php-reference/cp
#   https://www.ecomae.com/erp                ↔  https://www.ecomae.com/php-reference/erp
#   https://www.epartscart.com/cp             ↔  https://www.epartscart.com/php-reference/cp
#
# Paste-safe (this branch):
#   ECOMAE_BRANCH=cursor/deploy-php-reference-live-7529 \
#   ECOMAE_CONFIRM_PHP_REFERENCE_COMPARE_LIVE=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/deploy-php-reference-live-7529/scripts/cloudpanel_PHP_REFERENCE_COMPARE_LIVE_NOW.sh)" \
#     2>&1 | tee /root/php-reference-compare-live.log
#
# After merge to main:
#   ECOMAE_BRANCH=main ECOMAE_CONFIRM_PHP_REFERENCE_COMPARE_LIVE=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_PHP_REFERENCE_COMPARE_LIVE_NOW.sh)" \
#     2>&1 | tee /root/php-reference-compare-live.log
#
# Does not flip cutoverAllowed / readyForPhpRemoval. Does not delete PHP.
set -euo pipefail

if [[ "${ECOMAE_CONFIRM_PHP_REFERENCE_COMPARE_LIVE:-}" != "YES" ]]; then
  printf 'REFUSE: set ECOMAE_CONFIRM_PHP_REFERENCE_COMPARE_LIVE=YES\n' >&2
  exit 2
fi
if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/deploy-php-reference-live-7529}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== PHP REFERENCE COMPARE LIVE NOW (%s) ========\n' "$ECOMAE_BRANCH"

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
FULL="$(git rev-parse HEAD)"
printf 'REPO=%s SHA=%s FULL=%s\n' "$REPO" "$SHA" "$FULL"

if [[ ! -f epc_php_reference_boot.php ]]; then
  printf 'ERROR: epc_php_reference_boot.php missing — checkout too old\n' >&2
  exit 1
fi

chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh \
  scripts/cloudpanel_restore_php_reference_serving.sh \
  scripts/cloudpanel_install_classic_entry_aspnet_primary.sh 2>/dev/null || true

printf '\n---- [1/4] FORCE_LIVE_NOW (publish :5100 + classic-entry) ----\n'
set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/php-reference-compare-force-live.log
FORCE_RC=${PIPESTATUS[0]}
set -e
printf 'FORCE_LIVE_NOW exit=%s\n' "$FORCE_RC"

printf '\n---- [2/4] sync boot.php into ecomae + epartscart docroots ----\n'
mapfile -t DOCROOTS < <(
  {
    ls -d /home/*/htdocs/www.epartscart.com 2>/dev/null || true
    ls -d /home/*/htdocs/epartscart.com 2>/dev/null || true
    ls -d /home/*/htdocs/www.ecomae.com 2>/dev/null || true
    ls -d /home/*/htdocs/ecomae.com 2>/dev/null || true
    ls -d /home/*/htdocs/cp.ecomae.com 2>/dev/null || true
    ls -d /var/www/epartscart* 2>/dev/null || true
    ls -d /var/www/ecomae* 2>/dev/null || true
  } | sed '/^$/d' | sort -u
)
SYNCED=0
for dir in "${DOCROOTS[@]+"${DOCROOTS[@]}"}"; do
  [[ -d "$dir" && -f "$dir/index.php" ]] || continue
  mkdir -p "$dir/content/general_pages"
  cp -f "$REPO/epc_php_reference_boot.php" "$dir/epc_php_reference_boot.php" || true
  cp -f "$REPO/content/general_pages/epc_php_reference_router.php" \
    "$dir/content/general_pages/epc_php_reference_router.php" || true
  SYNCED=$((SYNCED + 1))
  printf '  boot.php → %s\n' "$dir"
done
printf 'Docroots synced: %s\n' "$SYNCED"

printf '\n---- [3/4] restore php-reference (strip archive 503) ----\n'
set +e
ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES \
  bash scripts/cloudpanel_restore_php_reference_serving.sh 2>&1 | tee /root/php-reference-compare-restore.log
RESTORE_RC=${PIPESTATUS[0]}
set -e
printf 'restore exit=%s\n' "$RESTORE_RC"

# Re-apply classic-entry AFTER restore so boot.php locations win over leftover STOP 503.
printf '\n---- [3b] classic-entry --all-hosts (boot.php rewrites) ----\n'
set +e
ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
  bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts \
  2>&1 | tee -a /root/php-reference-compare-restore.log
CLASSIC_RC=${PIPESTATUS[0]}
set -e
printf 'classic-entry exit=%s\n' "$CLASSIC_RC"

printf '\n---- [4/4] prove Classic CP/ERP twins ----\n'
fail=0
prove_twin() {
  local url="$1"
  local want="$2"
  local hdr body code loc ref
  hdr="$(mktemp)"
  body="$(mktemp)"
  code="$(curl -sS -D "$hdr" -o "$body" -w '%{http_code}' -A 'Mozilla/5.0' --max-time 30 "$url" || echo 000)"
  loc="$(grep -i '^location:' "$hdr" | awk '{print $2}' | tr -d '\r' | head -1 || true)"
  ref="$(grep -i '^x-ecomae-php-reference:' "$hdr" | awk '{print $2}' | tr -d '\r' | head -1 || true)"
  if grep -q 'Archive paused for platform deep test' "$body"; then
    printf 'FAIL %s http=%s archive-503-body\n' "$url" "$code"
    fail=1
  elif [[ "$code" == "503" ]]; then
    printf 'FAIL %s http=503\n' "$url"
    fail=1
  elif [[ "$loc" == "/?epc_php_reference="* || "$loc" == *"epc_php_reference=${want}" ]]; then
    printf 'FAIL %s http=%s bounced-to-storefront loc=%s\n' "$url" "$code" "$loc"
    fail=1
  elif [[ -n "$ref" && "$ref" == "$want" ]]; then
    printf 'PASS %s http=%s X-EcomAE-Php-Reference=%s\n' "$url" "$code" "$ref"
  elif [[ "$code" == "200" ]] && grep -Eiq 'DOCTYPE html|bootstrap_admin|Control Panel|ERP' "$body"; then
    printf 'PASS %s http=200 html-shell\n' "$url"
  elif [[ "$code" == "302" && ( "$loc" == *"/php-reference"* || "$loc" == *"/CP"* || "$loc" == *"/ERP"* || "$loc" == *login* ) ]]; then
    printf 'PASS %s http=302 loc=%s (php login/shell)\n' "$url" "$loc"
  else
    printf 'FAIL %s http=%s loc=%s ref=%s\n' "$url" "$code" "${loc:-none}" "${ref:-none}"
    fail=1
  fi
  rm -f "$hdr" "$body"
}

prove_twin "https://www.ecomae.com/php-reference/cp" cp
prove_twin "https://www.ecomae.com/php-reference/erp" erp
prove_twin "https://www.epartscart.com/php-reference/cp" cp

if [[ "$fail" -ne 0 ]]; then
  cat <<EOF >&2

RESULT=FAIL — Classic CP/ERP twins still blocked
SHA=$SHA
FORCE_LIVE=$FORCE_RC restore=$RESTORE_RC classic-entry=$CLASSIC_RC
Debug:
  curl -sSI https://www.ecomae.com/php-reference/cp | head -20
  curl -sSI https://www.ecomae.com/php-reference/erp | head -20
  curl -sSI https://www.epartscart.com/php-reference/cp | head -20
  nginx -T 2>/dev/null | grep -n 'php-reference\\|epc_php_reference_boot' | head -40
EOF
  exit 1
fi

cat <<EOF

#####################################################################
#  RESULT=PASS — Classic CP/ERP php-reference twins are live
#  SHA=$SHA
#  Open side by side (same admin session):
#    ecomae CP:       https://www.ecomae.com/cp
#                     https://www.ecomae.com/php-reference/cp
#    ecomae ERP:      https://www.ecomae.com/erp
#                     https://www.ecomae.com/php-reference/erp
#    ePartsCart CP:   https://www.epartscart.com/cp
#                     https://www.epartscart.com/php-reference/cp
#  Product /cp /erp stay ASP.NET. cutoverAllowed=false.
#####################################################################
EOF
exit 0
