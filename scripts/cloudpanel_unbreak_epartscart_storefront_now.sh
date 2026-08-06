#!/usr/bin/env bash
# UNBREAK epartscart after failed PHP-pause / warm-up loop / incomplete restore.
#
# Live failure (observed):
#   /              → ASP.NET OK
#   /cp/login      → ASP.NET OK
#   /storefront/*  → splash or PHP 404  (NOT proxied to :5100)
#   /en/*          → splash            (PHP-FPM dead OR php-off snippet still on)
#   /index.php     → splash
#   home chrome    → still PreferAspNetApps (/storefront/* links)
#   www.ecomae.com /storefront/* → OK (proves Kestrel is fine; epartscart nginx wrong)
#
#   ECOMAE_CONFIRM_UNBREAK_EPARTSCART_STOREFRONT=YES \
#     bash scripts/cloudpanel_unbreak_epartscart_storefront_now.sh
set -euo pipefail

if [[ "${ECOMAE_CONFIRM_UNBREAK_EPARTSCART_STOREFRONT:-}" != "YES" ]]; then
  printf 'REFUSE: set ECOMAE_CONFIRM_UNBREAK_EPARTSCART_STOREFRONT=YES\n' >&2
  exit 2
fi
if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

REPO="${ECOMAE_REPO:-/opt/ecomae-aspnet-source}"
[[ -d "$REPO/.git" ]] || REPO=/root/ecomae
cd "$REPO"

BRANCH="${ECOMAE_BRANCH:-main}"
printf '======== UNBREAK EPARTSCART STOREFRONT (%s) ========\n' "$BRANCH"

git fetch origin "$BRANCH"
git checkout -f "$BRANCH"
git reset --hard "origin/$BRANCH"

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
FLAG_ETC="/etc/ecomae-aspnet/php_serving_deactivated"
NGINX_SNIPPET=/etc/nginx/snippets/ecomae-php-serving-temporarily-deactivated.conf

# ---------------------------------------------------------------------------
# 1) Fully clear PHP-off artifacts (restore was incomplete if /en/ still splash)
# ---------------------------------------------------------------------------
rm -f "$FLAG_ETC" "$NGINX_SNIPPET"
find /home /var/www -maxdepth 5 -name '.epc_php_serving_deactivated' -delete 2>/dev/null || true

if [[ -f "$ENV_FILE" ]]; then
  cp -a "$ENV_FILE" "${ENV_FILE}.bak.unbreak.$(date +%Y%m%d%H%M%S)"
  python3 - <<PY
from pathlib import Path
p = Path("$ENV_FILE")
keys = {
  "EcomAE__PhpReference__TemporarilyDeactivatePhpServing": "false",
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
print("platform.env PreferAspNet/PHP-off cleared →", p)
PY
fi

# Remove include lines from all nginx confs
python3 - <<'PY'
from pathlib import Path
marker = "# ecomae-temp-php-serving-off"
snippet = "include /etc/nginx/snippets/ecomae-php-serving-temporarily-deactivated.conf;"
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
        if marker not in text and snippet not in text:
            continue
        lines = [ln for ln in text.splitlines() if marker not in ln and snippet not in ln]
        conf.write_text("\n".join(lines) + "\n")
        print("removed php-off include:", conf)
PY

# Strip stub→/en exact locations ( PreferAspNet + /en 503 = splash loop )
python3 - <<'PY'
from pathlib import Path
import re, time
block_re = re.compile(
    r"\nlocation = /storefront/(?:search-app|cart-app|checkout-app|orders-app|login|garage-app) \{.*?\n\}",
    re.S,
)
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
        if "storefront/search-app" not in text:
            continue
        new, n = block_re.subn("\n# removed stub→/en by unbreak_epartscart_storefront_now\n", text)
        if n:
            bak = conf.with_name(conf.name + ".bak.unbreak-stubs." + time.strftime("%Y%m%d%H%M%S"))
            bak.write_text(text)
            conf.write_text(new)
            print(f"stripped {n} stubs: {conf}")
PY

# ---------------------------------------------------------------------------
# 2) Restart PHP-FPM (index.php + /en/ were splash = FPM/upstream dead)
# ---------------------------------------------------------------------------
printf '\n== Restart PHP-FPM ==\n'
for svc in php8.3-fpm php8.2-fpm php8.1-fpm php8.0-fpm php7.4-fpm php-fpm; do
  if systemctl list-unit-files "${svc}.service" 2>/dev/null | grep -q "${svc}"; then
    systemctl restart "$svc" && printf 'restarted %s\n' "$svc" || true
  fi
done
# CloudPanel sometimes uses versioned sockets only — kick all php*-fpm
while read -r u; do
  [[ -n "$u" ]] || continue
  systemctl restart "$u" && printf 'restarted %s\n' "$u" || true
done < <(systemctl list-units --type=service --all 'php*-fpm*' --no-legend 2>/dev/null | awk '{print $1}' || true)

# Quick local PHP prove (bypass Cloudflare)
printf 'local php-fpm sockets:\n'
ls -la /run/php/*.sock 2>/dev/null || ls -la /var/run/php/*.sock 2>/dev/null || printf 'WARN: no php socks visible\n'

# ---------------------------------------------------------------------------
# 3) Re-install classic-entry (storefront + _framework → :5100 on epartscart)
# ---------------------------------------------------------------------------
printf '\n== Install classic-entry (all hosts) ==\n'
ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
  bash "$REPO/scripts/cloudpanel_install_classic_entry_aspnet_primary.sh" --all-hosts

# Verify epartscart server block actually contains storefront proxy
python3 - <<'PY'
from pathlib import Path
import sys
sys.path.insert(0, "/opt/ecomae-aspnet-source/scripts/lib")
try:
    from ecomae_nginx_server_block_edit import find_server_blocks, server_names, host_matches
except Exception:
    sys.path.insert(0, str(Path("/root/ecomae/scripts/lib")))
    from ecomae_nginx_server_block_edit import find_server_blocks, server_names, host_matches

found = False
for base in (Path("/etc/nginx/sites-enabled"), Path("/etc/nginx/conf.d")):
    if not base.exists():
        continue
    for conf in base.iterdir():
        if not conf.is_file():
            continue
        text = conf.read_text(errors="ignore")
        if "epartscart" not in text.lower():
            continue
        for start, end, body in find_server_blocks(text):
            names = server_names(body)
            if not host_matches(names, "www.epartscart.com"):
                continue
            has_sf = "location ^~ /storefront/" in body and "5100" in body
            has_fw = "location ^~ /_framework/" in body
            print(f"epartscart block in {conf}: names={names[:6]} storefront_proxy={has_sf} framework_proxy={has_fw}")
            # Show whether dangerous stubs remain
            for stub in ("search-app", "garage-app", "login"):
                if f"location = /storefront/{stub}" in body:
                    print(f"  WARN still has exact stub location = /storefront/{stub}")
            found = True
            if not has_sf:
                print("FAIL: epartscart server block missing ^~ /storefront/ → :5100")
                sys.exit(2)
if not found:
    print("FAIL: could not find www.epartscart.com server{} block")
    sys.exit(2)
print("OK epartscart storefront proxy present")
PY

nginx -t
systemctl reload nginx

# ---------------------------------------------------------------------------
# 4) Restart ASP.NET (PreferAspNetApps=false from platform.env)
# ---------------------------------------------------------------------------
systemctl restart ecomae-platform.service || true
sleep 4
systemctl is-active ecomae-platform.service || true
ss -lntp | rg ':5100' || netstat -lntp 2>/dev/null | rg ':5100' || true

# Optional publish
if [[ "${ECOMAE_ALSO_FORCE_LIVE:-}" == "YES" && -x "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh" ]]; then
  export ECOMAE_BRANCH="$BRANCH"
  bash "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh" || true
fi

# Sync splash escape-hatch
if [[ -f "$REPO/epc-platform-splash.html" ]]; then
  for d in /home/ecomae/htdocs/www.ecomae.com /home/ecomae/htdocs/www.epartscart.com "$REPO"; do
    [[ -d "$d" ]] && cp -a "$REPO/epc-platform-splash.html" "$d/epc-platform-splash.html" && echo "splash → $d"
  done
fi

# ---------------------------------------------------------------------------
# 5) Prove — local first (127.0.0.1 + Host), then public
# ---------------------------------------------------------------------------
printf '\n== Local prove (Host: www.epartscart.com → 127.0.0.1) ==\n'
local_fail=0
for path in / /storefront/app /storefront/search-app /en/shop/part_search /index.php; do
  body=$(mktemp)
  # Try https localhost vhost; fall back to http://127.0.0.1:5100 for ASP.NET paths
  code=$(curl -sk -o "$body" -w '%{http_code}' -A 'Mozilla/5.0' --max-time 20 \
    --resolve www.epartscart.com:443:127.0.0.1 "https://www.epartscart.com${path}" 2>/dev/null || echo 000)
  size=$(wc -c <"$body")
  splash=0
  rg -q 'Loading your store' "$body" && [[ "$size" -lt 5000 ]] && splash=1
  printf 'local %s http=%s size=%s splash=%s\n' "$path" "$code" "$size" "$splash"
  if [[ "$path" == /storefront/* || "$path" == / ]]; then
    if [[ "$splash" -eq 1 ]]; then
      printf '  FAIL local splash on %s\n' "$path"
      local_fail=1
    fi
  fi
  if [[ "$path" == /en/* || "$path" == /index.php ]]; then
    if [[ "$splash" -eq 1 ]]; then
      printf '  FAIL PHP still splash on %s — check php-fpm / nginx php location\n' "$path"
      local_fail=1
    fi
  fi
  rm -f "$body"
done

# Direct Kestrel
printf '\n== Direct Kestrel :5100 ==\n'
for path in /storefront/app /storefront/search-app; do
  code=$(curl -sS -o /tmp/kbody -w '%{http_code}' -H 'Host: www.epartscart.com' --max-time 20 \
    "http://127.0.0.1:5100${path}" || echo 000)
  size=$(wc -c </tmp/kbody)
  splash=0; rg -q 'Loading your store' /tmp/kbody && [[ "$size" -lt 5000 ]] && splash=1
  printf 'kestrel %s http=%s size=%s splash=%s\n' "$path" "$code" "$size" "$splash"
  [[ "$code" == "200" && "$splash" -eq 0 && "$size" -gt 5000 ]] || {
    printf '  FAIL Kestrel not serving %s — ecomae-platform.service issue\n' "$path"
    local_fail=1
  }
done

printf '\n== Public prove ==\n'
fail=0
Q="epc_unbreak=$(date +%s)"
for path in / /storefront/app /storefront/search-app /storefront/garage-app /en/shop/part_search /en/users/login; do
  body=$(mktemp)
  code=$(curl -sS -o "$body" -w '%{http_code}' -A 'Mozilla/5.0' --max-time 45 \
    "https://www.epartscart.com${path}?${Q}" || echo 000)
  size=$(wc -c <"$body")
  if rg -q 'Loading your store' "$body" && [[ "$size" -lt 5000 ]]; then
    printf 'FAIL %s http=%s SPLASH size=%s\n' "$path" "$code" "$size"
    fail=1
  elif [[ "$path" == /storefront/app && "$size" -lt 5000 ]]; then
    printf 'FAIL %s http=%s too small size=%s\n' "$path" "$code" "$size"
    fail=1
  else
    title=$(rg -o '<title>[^<]+' "$body" | head -1 || true)
    printf 'PASS %s http=%s size=%s %s\n' "$path" "$code" "$size" "$title"
  fi
  rm -f "$body"
done

# Home should prefer /en/ links again (PreferAspNetApps=false) OR /storefront that works
home=$(mktemp)
curl -sS -o "$home" -A 'Mozilla/5.0' --max-time 45 "https://www.epartscart.com/?${Q}" || true
sf_n=$(rg -o 'href="/storefront/[^"]+"' "$home" | wc -l || true)
en_n=$(rg -o 'href="/en/[^"]+"' "$home" | wc -l || true)
printf 'home storefront_links=%s en_links=%s\n' "$sf_n" "$en_n"
rm -f "$home"

if [[ "$fail" -ne 0 || "$local_fail" -ne 0 ]]; then
  cat <<EOF >&2

RESULT=FAIL — still broken
Debug on server:
  systemctl status ecomae-platform.service --no-pager -l | head -40
  systemctl status php*-fpm --no-pager | head -40
  curl -sS -H 'Host: www.epartscart.com' http://127.0.0.1:5100/storefront/app | head -c 200
  nginx -T 2>/dev/null | grep -n 'storefront' | head -40
EOF
  exit 1
fi

cat <<EOF

#####################################################################
#  RESULT=PASS — epartscart storefront unbroken
#  /storefront/* → ASP.NET :5100
#  /en/* and PHP no longer warm-up splash
#  PreferAspNetApps=false (chrome can use /en/ again)
#  Soft-refresh https://www.epartscart.com/
#####################################################################
EOF
exit 0
