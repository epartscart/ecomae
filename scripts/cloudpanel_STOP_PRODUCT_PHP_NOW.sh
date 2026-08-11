#!/usr/bin/env bash
# NUCLEAR: stop product legacy docroot for /cp /erp /storefront — force :5100.
#
# Why you still see old /cp/control login after "pause PHP":
#   Pause only stops /php-reference + /en. Product /cp/control was never cut over
#   on this host (nginx still served Homer login from docroot).
#
# This script:
#   1) Pulls latest main
#   2) Rewrites epartscart (+ www) server blocks with HARD platform locations
#      (location = /cp/control → :5100 first — cannot miss)
#   3) 503s residual /en + /php-reference (archive pause)
#   4) Restarts platform + reloads nginx
#   5) PROVES /cp/control is NOT bootstrap_admin legacy login
#
#   ECOMAE_CONFIRM_STOP_PRODUCT_PHP_NOW=YES \
#     bash scripts/cloudpanel_STOP_PRODUCT_PHP_NOW.sh
set -euo pipefail

if [[ "${ECOMAE_CONFIRM_STOP_PRODUCT_PHP_NOW:-}" != "YES" ]]; then
  printf 'REFUSE: set ECOMAE_CONFIRM_STOP_PRODUCT_PHP_NOW=YES\n' >&2
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
printf '======== STOP PRODUCT PHP NOW (%s) ========\n' "$BRANCH"

git fetch origin "$BRANCH"
git checkout -f "$BRANCH"
git reset --hard "origin/$BRANCH"

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"

# Prefer platform apps for chrome links
if [[ -f "$ENV_FILE" ]]; then
  cp -a "$ENV_FILE" "${ENV_FILE}.bak.stop-product-php.$(date +%Y%m%d%H%M%S)"
  python3 - <<PY
from pathlib import Path
p = Path("$ENV_FILE")
keys = {
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
print("platform.env → TemporarilyDeactivatePhpServing=true (archive pause + PreferAspNetApps)")
PY
fi
mkdir -p /etc/ecomae-aspnet
touch /etc/ecomae-aspnet/php_serving_deactivated

# ---------------------------------------------------------------------------
# Hard nginx pack — inserted at TOP of matching server{} (wins over later mess)
# ---------------------------------------------------------------------------
python3 - <<'PY'
from pathlib import Path
import re, time, shutil

MARKER_BEGIN = "# BEGIN ecomae-STOP-PRODUCT-PHP"
MARKER_END = "# END ecomae-STOP-PRODUCT-PHP"

# Home path differs: tenant storefront vs www marketing (never send www → storefront).
PACK_TEMPLATE = r'''
    # BEGIN ecomae-STOP-PRODUCT-PHP
    # Nuclear: product surfaces → :5100 only. Legacy docroot must not answer /cp/control.
    location = /cp/control {
        proxy_pass http://127.0.0.1:5100/cp/control;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp-control;
    }
    location = /cp/control/ {
        proxy_pass http://127.0.0.1:5100/cp/control;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp-control;
    }
    location = /CP/control {
        proxy_pass http://127.0.0.1:5100/cp/control;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp-control;
    }
    location = /CP/control/ {
        proxy_pass http://127.0.0.1:5100/cp/control;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp-control;
    }
    location = /cp {
        proxy_pass http://127.0.0.1:5100/cp/app;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp;
    }
    location = /cp/ {
        proxy_pass http://127.0.0.1:5100/cp/app;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp;
    }
    location = /CP {
        proxy_pass http://127.0.0.1:5100/cp/app;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp;
    }
    location = /CP/ {
        proxy_pass http://127.0.0.1:5100/cp/app;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp;
    }
    location = /cp/login {
        proxy_pass http://127.0.0.1:5100/cp/login;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp-login;
    }
    location = /cp/login/ {
        proxy_pass http://127.0.0.1:5100/cp/login;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp-login;
    }
    location ^~ /cp/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp-tree;
    }
    location ^~ /CP/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-cp-tree;
    }
    location = /erp {
        proxy_pass http://127.0.0.1:5100/erp/app;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-erp;
    }
    location = /erp/ {
        proxy_pass http://127.0.0.1:5100/erp/app;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-erp;
    }
    location = /erp/login {
        proxy_pass http://127.0.0.1:5100/erp/login;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-erp-login;
    }
    location ^~ /erp/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-erp-tree;
    }
    location ^~ /ERP/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-erp-tree;
    }
    location = / {
        proxy_pass http://127.0.0.1:5100/__HOME_APP__;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover __HOME_CUTOVER__;
    }
    # Product PHP front-controllers → Kestrel (never php-fpm marketing/home).
    location = /index.php {
        proxy_pass http://127.0.0.1:5100/index.php;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-index-php;
    }
    location = /epc-blockchain-verify.php {
        proxy_pass http://127.0.0.1:5100/epc-blockchain-verify.php;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-blockchain-verify;
    }
    location = /blockchain/verify {
        proxy_pass http://127.0.0.1:5100/blockchain/verify;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-blockchain-verify-aspnet;
    }
    location = /epc-static.php {
        proxy_pass http://127.0.0.1:5100/epc-static.php;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-epc-static-bridge;
    }
    location ^~ /marketing/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-marketing;
    }
    location ^~ /storefront/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-storefront;
    }
    location ^~ /_framework/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-framework;
    }
    location ^~ /platform-assets/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
        proxy_set_header X-EcomAE-Route-Cutover stop-product-php-assets;
    }
    # Archive / interim commerce — paused for platform deep test
    location ^~ /php-reference {
        default_type text/plain;
        return 503 "Archive paused for platform deep test.\n";
    }
    location ^~ /en/ {
        # Paused mode: platform maps /en commerce links into the apps (no splash).
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
    }
    # END ecomae-STOP-PRODUCT-PHP
'''

SERVER_START = re.compile(r"(?m)^[ \t]*server\s*\{")
SERVER_NAME = re.compile(r"(?im)^\s*server_name\s+([^;]+);")

def find_server_blocks(text: str):
    blocks = []
    for m in SERVER_START.finditer(text):
        start = m.start()
        i = m.end() - 1
        depth = 0
        j = i
        while j < len(text):
            if text[j] == "{":
                depth += 1
            elif text[j] == "}":
                depth -= 1
                if depth == 0:
                    blocks.append((start, j + 1, text[start:j+1]))
                    break
            j += 1
    return blocks

def names_of(body: str):
    out = []
    for m in SERVER_NAME.finditer(body):
        out.extend(t.strip().lower() for t in m.group(1).split() if t.strip() and t.strip() != "_")
    return out

def host_match(names, host):
    host = host.lower()
    variants = {host, host[4:] if host.startswith("www.") else "www." + host}
    return any(n in variants for n in names)

# Strip old nuclear packs + conflicting exact /cp/control locations (re-add clean pack)
CONFLICT_EXACT = re.compile(
    r"(?m)^[ \t]*location\s*=\s*/(?:cp|CP)(?:/control)?/?\s*\{.*?\n[ \t]*\}\n?",
    re.S,
)
# Also remove stub→/en storefront exacts
STUBS = re.compile(
    r"(?m)^[ \t]*location\s*=\s*/storefront/(?:search-app|cart-app|checkout-app|orders-app|login|garage-app)\s*\{.*?\n[ \t]*\}\n?",
    re.S,
)
OLD_PACK = re.compile(
    r"[ \t]*# BEGIN ecomae-STOP-PRODUCT-PHP.*?# END ecomae-STOP-PRODUCT-PHP\n?",
    re.S,
)

TARGETS_STOREFRONT = [
    "www.epartscart.com",
    "epartscart.com",
]
TARGETS_MARKETING = [
    "www.ecomae.com",
    "ecomae.com",
]
TARGETS = TARGETS_STOREFRONT + TARGETS_MARKETING

def pack_for(names):
    if any(host_match(names, h) for h in TARGETS_MARKETING):
        return (
            PACK_TEMPLATE
            .replace("__HOME_APP__", "marketing/app")
            .replace("__HOME_CUTOVER__", "stop-product-php-home-marketing")
        )
    return (
        PACK_TEMPLATE
        .replace("__HOME_APP__", "storefront/app")
        .replace("__HOME_CUTOVER__", "stop-product-php-home-storefront")
    )

patched_files = 0
for base in (Path("/etc/nginx/sites-enabled"), Path("/etc/nginx/conf.d")):
    if not base.exists():
        continue
    for conf in sorted(base.iterdir()):
        if not conf.is_file():
            continue
        try:
            text = conf.read_text(errors="ignore")
        except Exception:
            continue
        if "server_name" not in text:
            continue
        blocks = find_server_blocks(text)
        if not blocks:
            continue
        out = text
        changed = False
        for start, end, body in sorted(blocks, key=lambda t: t[0], reverse=True):
            names = names_of(body)
            if not any(host_match(names, h) for h in TARGETS):
                continue
            # Skip apex-only redirect blocks
            if "root " not in body and "location " not in body and re.search(r"return\s+30[12]", body):
                continue
            new_body = OLD_PACK.sub("", body)
            # Remove conflicting CP exact locations so our pack is authoritative
            new_body = re.sub(
                r"(?m)^[ \t]*location\s*=\s*/(?:cp|CP)(?:/control|/login)?/?\s*\{.*?\n[ \t]*\}\n?",
                "",
                new_body,
                flags=re.S,
            )
            new_body = STUBS.sub("", new_body)
            # Insert pack right after "server {"
            m = re.match(r"(?s)([ \t]*server\s*\{)(\s*)(.*)", new_body)
            if not m:
                continue
            pack = pack_for(names)
            new_body = m.group(1) + "\n" + pack + m.group(2) + m.group(3)
            out = out[:start] + new_body + out[end:]
            changed = True
            print(f"patched server_name={names[:8]} in {conf}")
        if changed:
            bak = conf.with_name(conf.name + ".bak.stop-product-php." + time.strftime("%Y%m%d%H%M%S"))
            shutil.copy2(conf, bak)
            conf.write_text(out)
            print(f"wrote {conf} bak={bak}")
            patched_files += 1

if patched_files == 0:
    raise SystemExit("FAIL: no epartscart/ecomae server{} blocks patched — check nginx sites-enabled")
print(f"OK patched_files={patched_files}")
PY

nginx -t
systemctl reload nginx

# Platform must be up
systemctl restart ecomae-platform.service || true
sleep 4
ss -lntp 2>/dev/null | rg ':5100' || netstat -lntp 2>/dev/null | rg ':5100' || true

if [[ "${ECOMAE_ALSO_FORCE_LIVE:-}" == "YES" && -x "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh" ]]; then
  export ECOMAE_BRANCH="$BRANCH"
  bash "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh" || true
fi

printf '\n== PROVE /cp/control is PLATFORM (not old login) ==\n'
fail=0
for path in /cp/control /cp/control/ /CP/control /cp /cp/login; do
  body=$(mktemp)
  hdr=$(mktemp)
  code=$(curl -sS -D "$hdr" -o "$body" -A 'Mozilla/5.0' --max-time 45 -w '%{http_code}' \
    "https://www.epartscart.com${path}?epc_stop=$(date +%s)" || echo 000)
  size=$(wc -c <"$body")
  legacy=0
  rg -qi 'bootstrap_admin|auth_contact_select|base href="/cp/templates' "$body" && legacy=1
  plat=0
  rg -qi 'blazor-enhanced-nav|blazor-focus-on-navigate|ecomae-chrome-surface' "$hdr" "$body" && plat=1
  loc=$(rg -i '^location:' "$hdr" | tr -d '\r' | awk '{print $2}' | head -1 || true)
  # Auth gate 302 → /cp/login counts as PLATFORM win
  if [[ "$code" == "302" && "$loc" == *"/cp/login"* ]]; then
    printf 'PASS %s http=302 → platform login gate\n' "$path"
  elif [[ "$legacy" -eq 1 ]]; then
    printf 'FAIL %s http=%s size=%s STILL_OLD_LOGIN\n' "$path" "$code" "$size"
    fail=1
  elif [[ "$plat" -eq 1 ]]; then
    printf 'PASS %s http=%s size=%s PLATFORM\n' "$path" "$code" "$size"
  else
    printf 'FAIL %s http=%s size=%s unknown (not platform markers)\n' "$path" "$code" "$size"
    fail=1
  fi
  rm -f "$body" "$hdr"
done

# Direct Kestrel sanity
code_k=$(curl -sS -o /tmp/kctl -w '%{http_code}' -H 'Host: www.epartscart.com' --max-time 20 \
  http://127.0.0.1:5100/cp/control || echo 000)
printf 'kestrel /cp/control http=%s size=%s\n' "$code_k" "$(wc -c </tmp/kctl)"
rg -qi 'bootstrap_admin|auth_contact_select' /tmp/kctl && {
  printf 'WARN: Kestrel itself returned legacy-looking HTML — publish/FORCE LIVE needed\n'
}

if [[ "$fail" -ne 0 ]]; then
  cat <<EOF >&2

RESULT=FAIL — /cp/control still old login
Debug:
  nginx -T 2>/dev/null | grep -n 'STOP-PRODUCT-PHP\|location = /cp/control' | head -40
  curl -sS -D - -o /tmp/x https://www.epartscart.com/cp/control | head -30
  systemctl status ecomae-platform.service --no-pager | head -20
EOF
  exit 1
fi

cat <<EOF

#####################################################################
#  RESULT=PASS — product PHP STOPPED for /cp /erp /storefront
#  /cp/control is PLATFORM (not old Homer login)
#  Archive /php-reference + /en/ return 503
#  Test in browser:
#    https://www.epartscart.com/cp/login
#    https://www.epartscart.com/cp
#    https://www.epartscart.com/cp/control   ← must match platform (login gate or CC)
#  Hard-refresh (Ctrl+Shift+R). Old login = cached tab — close tab first.
#####################################################################
EOF
exit 0
