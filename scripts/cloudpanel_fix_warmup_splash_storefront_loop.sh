#!/usr/bin/env bash
# Fix stuck "Loading your store..." warm-up after PHP pause + tenant stub→/en redirects.
#
# Live failure mode:
#   / works (ASP.NET) but menu clicks → /storefront/*-app
#   nginx exact locations 302 → /en/...
#   TemporarilyDeactivatePhpServing makes /en/* return 503
#   error_page 503 → epc-platform-splash.html → shopper trapped for minutes
#
# This script:
#   1) Strips tenant nginx stub→/en exact locations (leave ^~ /storefront/ → :5100)
#   2) Syncs fixed epc-platform-splash.html (probes / then navigates home)
#   3) Re-installs classic-entry (storefront tree → Kestrel)
#   4) Restarts platform + proves /storefront/app and /storefront/search-app are NOT splash
#
#   ECOMAE_CONFIRM_FIX_WARMUP_SPLASH_LOOP=YES \
#     bash scripts/cloudpanel_fix_warmup_splash_storefront_loop.sh
set -euo pipefail

if [[ "${ECOMAE_CONFIRM_FIX_WARMUP_SPLASH_LOOP:-}" != "YES" ]]; then
  printf 'REFUSE: set ECOMAE_CONFIRM_FIX_WARMUP_SPLASH_LOOP=YES\n' >&2
  exit 2
fi

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

REPO="${ECOMAE_REPO:-/opt/ecomae-aspnet-source}"
if [[ ! -d "$REPO/.git" ]]; then
  REPO=/root/ecomae
fi
cd "$REPO"

BRANCH="${ECOMAE_BRANCH:-main}"
printf '======== FIX WARMUP SPLASH / STOREFRONT LOOP (%s) ========\n' "$BRANCH"

git fetch origin "$BRANCH"
git checkout -f "$BRANCH"
git reset --hard "origin/$BRANCH"

# 1) Strip dangerous stub→/en exact locations from live nginx confs
python3 - <<'PY'
from pathlib import Path
import re

# Match exact location blocks that bounce storefront apps to PHP /en/
block_re = re.compile(
    r"\nlocation = /storefront/(?:search-app|cart-app|checkout-app|orders-app|login|garage-app) \{.*?\n\}",
    re.S,
)

patched = 0
for base in (Path("/etc/nginx/sites-enabled"), Path("/etc/nginx/conf.d")):
    if not base.exists():
        continue
    for conf in base.iterdir():
        if not conf.is_file():
            continue
        try:
            text = conf.read_text(errors="ignore")
        except Exception:
            continue
        if "storefront/search-app" not in text and "storefront/garage-app" not in text:
            continue
        new, n = block_re.subn("\n# removed stub→/en by cloudpanel_fix_warmup_splash_storefront_loop.sh\n", text)
        if n:
            bak = conf.with_name(conf.name + ".bak.warmup-splash-fix." + __import__("time").strftime("%Y%m%d%H%M%S"))
            bak.write_text(text)
            conf.write_text(new)
            print(f"stripped {n} stub location(s): {conf} bak={bak}")
            patched += 1
print(f"nginx confs patched: {patched}")
PY

# 2) Sync fixed splash into platform + tenant docroots
SPLASH_SRC="$REPO/epc-platform-splash.html"
if [[ -f "$SPLASH_SRC" ]]; then
  while IFS= read -r -d '' dest; do
    cp -a "$SPLASH_SRC" "$dest"
    printf 'splash synced → %s\n' "$dest"
  done < <(find /home /var/www /opt -maxdepth 6 -type f -name 'epc-platform-splash.html' -print0 2>/dev/null || true)
  # also copy next to common docroots even if missing
  for d in \
    /home/ecomae/htdocs/www.ecomae.com \
    /home/ecomae/htdocs/www.epartscart.com \
    /opt/ecomae-aspnet-source
  do
    if [[ -d "$d" ]]; then
      cp -a "$SPLASH_SRC" "$d/epc-platform-splash.html"
      printf 'splash ensured → %s/epc-platform-splash.html\n' "$d"
    fi
  done
fi

# 3) Re-install classic-entry so ^~ /storefront/ → :5100 is present (new example has no stub→/en)
if [[ -x "$REPO/scripts/cloudpanel_install_classic_entry_aspnet_primary.sh" ]]; then
  ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
  ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
    bash "$REPO/scripts/cloudpanel_install_classic_entry_aspnet_primary.sh" --all-hosts || true
fi

nginx -t
systemctl reload nginx

# 4) Restart ASP.NET
systemctl restart ecomae-platform.service || true
sleep 3

# Optional: FORCE LIVE publish from this branch
if [[ "${ECOMAE_ALSO_FORCE_LIVE:-}" == "YES" && -x "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh" ]]; then
  export ECOMAE_BRANCH="$BRANCH"
  bash "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh" || true
fi

printf '\n== Prove (must NOT be splash) ==\n'
fail=0
for path in / /storefront/app /storefront/search-app /storefront/garage-app /storefront/login; do
  body="$(mktemp)"
  code="$(curl -sS -o "$body" -w '%{http_code}' -A 'Mozilla/5.0' --max-time 45 \
    "https://www.epartscart.com${path}?epc_warmup_fix=$(date +%s)" || echo 000)"
  if rg -q 'Loading your store' "$body" && [[ "$(wc -c <"$body")" -lt 4000 ]]; then
    printf 'FAIL %s http=%s SPLASH\n' "$path" "$code"
    fail=1
  else
    title="$(rg -o '<title>[^<]+' "$body" | head -1 || true)"
    printf 'PASS %s http=%s size=%s %s\n' "$path" "$code" "$(wc -c <"$body")" "$title"
  fi
  rm -f "$body"
done

if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL — if /storefront/* still splash: restore PHP fallbacks temporarily:\n'
  printf '  ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES bash scripts/cloudpanel_restore_php_reference_serving.sh\n'
  printf 'Then re-run classic-entry install + this script.\n'
  exit 1
fi

printf '\nRESULT=PASS — warm-up loop cleared; /storefront/* is ASP.NET again.\n'
printf 'Tell shoppers: hard-refresh or open https://www.epartscart.com/\n'
