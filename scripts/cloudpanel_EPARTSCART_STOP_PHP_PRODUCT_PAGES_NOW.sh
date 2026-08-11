#!/usr/bin/env bash
# Stop ALL product PHP HTML pages on ePartsCart — ASP.NET Core only.
#
# Policy (operator lock):
#   - Browser product URLs (/ /en/* /storefront/* /cp /erp) → Kestrel :5100
#   - PHP page bodies ONLY via /php-reference/* and ONLY when you ask to restore
#   - Backend ajax/write bridges may still call PHP APIs (not product HTML)
#   - cutoverAllowed=false, readyForPhpRemoval=false
#
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/epartscart-stop-php-product-pages-7b3b/scripts/cloudpanel_EPARTSCART_STOP_PHP_PRODUCT_PAGES_NOW.sh'
#   TMP=/tmp/epartscart-stop-php-product-pages-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/epartscart-stop-php-product-pages-7b3b
#   bash "$TMP" 2>&1 | tee /root/epartscart-stop-php-product-pages.log
#   grep -E 'RESULT=|GATE_|SHA=|PREFIX_|PHP_PRODUCT' /root/epartscart-stop-php-product-pages.log | tail -100
#
# Silent External action without RESULT=PASS paste-back = FAIL.
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/epartscart-stop-php-product-pages-7b3b}"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
PUBLIC_BASE="${ECOMAE_PUBLIC_BASE:-https://www.epartscart.com}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== EPARTSCART STOP PHP PRODUCT PAGES NOW ========\n'
printf 'HOST=%s\n' "$(hostname -f 2>/dev/null || hostname || echo unknown)"
printf 'DATE_UTC=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf 'ECOMAE_BRANCH=%s\n' "$ECOMAE_BRANCH"

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

# 1) Platform flags: ASP.NET primary + pause /php-reference HTML until you ask to restore.
if [[ -f "$ENV_FILE" ]]; then
  cp -a "$ENV_FILE" "${ENV_FILE}.bak.stop-php-pages.$(date +%Y%m%d%H%M%S)"
  python3 - <<PY
from pathlib import Path
p = Path("$ENV_FILE")
keys = {
  "EcomAE__PhpReference__PreferAspNetStorefrontApps": "true",
  "EcomAE__PhpReference__TemporarilyDeactivatePhpServing": "true",
  "EcomAE__PhpReference__KeepPhpProjectAvailable": "true",
  "EcomAE__PhpReference__Mode": "aspnet-primary-php-reference",
  "MigrationRouteCutover__RequirePhpFallback": "true",
}
lines = p.read_text().splitlines()
out, seen = [], set()
for line in lines:
    if not line.strip() or line.lstrip().startswith("#") or "=" not in line:
        out.append(line); continue
    k = line.split("=", 1)[0].strip()
    if k in keys:
        out.append(f"{k}={keys[k]}"); seen.add(k)
    else:
        out.append(line)
for k, v in keys.items():
    if k not in seen:
        out.append(f"{k}={v}")
p.write_text("\n".join(out) + "\n")
print("platform.env → PreferAspNet=true TemporarilyDeactivatePhpServing=true (php-reference paused)")
PY
fi
mkdir -p /etc/ecomae-aspnet
touch /etc/ecomae-aspnet/php_serving_deactivated

# 2) Publish binary
if [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  printf '\n---- FORCE_LIVE ----\n'
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/epartscart-stop-php-force-live.log | tail -50
  set -e
fi

# 3) Classic-entry: location ^~ /en/ → :5100 (entire lang tree)
printf '\n---- classic-entry install (blanket /en/) ----\n'
export ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES
export ECOMAE_CLASSIC_ENTRY_TENANT_HOST="${ECOMAE_CLASSIC_ENTRY_TENANT_HOST:-www.epartscart.com}"
set +e
bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh 2>&1 | tee /root/epartscart-stop-php-classic-entry.log
CE_RC=${PIPESTATUS[0]}
set -e
grep -E 'PREFIX_|OK installed|ERROR|PASS:' /root/epartscart-stop-php-classic-entry.log | tail -40 || true
[[ "$CE_RC" -eq 0 ]] || { printf 'RESULT=FAIL classic_entry_install\n'; exit 1; }

# Restart platform so env flags apply
systemctl restart ecomae-platform.service 2>/dev/null || true
sleep 2

# 4) Prove
bash scripts/cloudpanel_EPARTSCART_NO_PHP_PRODUCT_PAGES_PROVE.sh
PROVE_RC=$?
if [[ "$PROVE_RC" -ne 0 ]]; then
  printf 'RESULT=FAIL prove SHA=%s\n' "$SHA"
  exit 1
fi

printf '\nRESULT=PASS PHP_PRODUCT_PAGES=STOPPED ASPNET_PRIMARY=YES SHA=%s\n' "$SHA"
printf 'NOTE: restore PHP archive compare only when asked:\n'
printf '  ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE=YES bash scripts/cloudpanel_restore_php_reference_serving.sh\n'
printf '  (do NOT re-enable product /en HTML on PHP-FPM)\n'
exit 0
