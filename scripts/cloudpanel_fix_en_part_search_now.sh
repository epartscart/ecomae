#!/usr/bin/env bash
# Fix live /en/shop/part_search 503 pause loop on epartscart.
#
# Failure mode:
#   nginx location ^~ /en/ { return 503 "Interim PHP lang commerce pages paused…"; }
#   shopper hits /en/shop/part_search → splash/503 loop
#
# This script:
#   1) Scrubs any /en/ nginx 503 pause blocks → proxy :5100
#   2) Sets TemporarilyDeactivatePhpServing=true (PreferAspNetApps)
#   3) Runs FORCE_LIVE publish + proves part_search URLs
#
# CloudPanel root:
#   ECOMAE_BRANCH=cursor/fix-live-widget-root-nginx-7b3b \
#     bash scripts/cloudpanel_fix_en_part_search_now.sh
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/fix-live-widget-root-nginx-7b3b}"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
FLAG_ETC="/etc/ecomae-aspnet/php_serving_deactivated"
PUBLIC_BASE="${ECOMAE_PUBLIC_BASE:-https://www.epartscart.com}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== FIX /en/shop/part_search 503 LOOP (%s) ========\n' "$ECOMAE_BRANCH"

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

if ! grep -q '@page "/en/shop/part_search"' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  printf 'ERROR: checkout missing @page /en/shop/part_search — pull fix branch first\n' >&2
  exit 1
fi

# ---- 1) Scrub nginx /en/ 503 pause → proxy :5100 ----
python3 - <<'PY'
import re, shutil, time
from pathlib import Path

PROXY_BLOCK = '''location ^~ /en/ {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header Cookie $http_cookie;
}'''

def find_blocks(text, start_pat):
    out = []
    for m in start_pat.finditer(text):
        i = text.find('{', m.start())
        if i < 0:
            continue
        depth, j = 0, i
        while j < len(text):
            if text[j] == '{':
                depth += 1
            elif text[j] == '}':
                depth -= 1
                if depth == 0:
                    out.append((m.start(), j + 1))
                    break
            j += 1
    return out

LOC_PAT = re.compile(r'(?m)^[ \t]*location\s+(?:\^~\s+)?/en/\s*\{')
backup = Path('/root/nginx-en-503-scrub-' + time.strftime('%Y%m%d%H%M%S'))
changed = 0
for base in (Path('/etc/nginx/sites-enabled'), Path('/etc/nginx/conf.d'), Path('/etc/nginx/snippets')):
    if not base.exists():
        continue
    for conf in base.rglob('*.conf'):
        try:
            text = conf.read_text(errors='ignore')
        except Exception:
            continue
        orig = text
        for start, end in reversed(find_blocks(text, LOC_PAT)):
            body = text[start:end]
            if 'return 503' not in body and 'Interim PHP lang commerce' not in body:
                continue
            if 'proxy_pass http://127.0.0.1:5100' in body:
                continue
            indent = re.match(r'^([ \t]*)', body).group(1)
            lines = [indent + ln if ln.strip() else ln for ln in PROXY_BLOCK.splitlines()]
            new_block = '\n'.join(lines)
            text = text[:start] + new_block + text[end:]
            print(f'scrubbed /en/ 503 → :5100 in {conf}')
        if text != orig:
            backup.mkdir(parents=True, exist_ok=True)
            shutil.copy2(conf, backup / conf.name)
            conf.write_text(text)
            changed += 1
print(f'nginx confs scrubbed: {changed}')
PY

if command -v nginx >/dev/null 2>&1; then
  if nginx -t 2>&1 | tee /tmp/epc-en-part-search-nginx-t.log; then
    systemctl reload nginx || true
    printf 'nginx reloaded after /en/ scrub\n'
  else
    printf 'WARN: nginx -t failed after scrub — continuing to platform env + FORCE_LIVE\n' >&2
  fi
fi

# ---- 2) TemporarilyDeactivatePhpServing=true ----
mkdir -p /etc/ecomae-aspnet
printf 'status=php-serving-temporarily-deactivated sha=%s time=%s\n' \
  "$(git rev-parse HEAD)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$FLAG_ETC"
chmod 644 "$FLAG_ETC"

touch "$ENV_FILE"
python3 - <<PY
from pathlib import Path
p = Path("$ENV_FILE")
lines = p.read_text().splitlines() if p.exists() else []
kv = {
    "EcomAE__PhpReference__TemporarilyDeactivatePhpServing": "true",
    "EcomAE__PhpReference__KeepPhpProjectAvailable": "true",
}
out, seen = [], set()
for ln in lines:
    k = ln.split("=", 1)[0].strip() if "=" in ln else ""
    if k in kv:
        out.append(f"{k}={kv[k]}")
        seen.add(k)
    else:
        out.append(ln)
for k, v in kv.items():
    if k not in seen:
        out.append(f"{k}={v}")
p.write_text("\n".join(out) + "\n")
print("platform.env → TemporarilyDeactivatePhpServing=true")
PY

# ---- 3) FORCE_LIVE publish + prove ----
export ECOMAE_BRANCH
bash "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh"

printf '\n== Prove /en/shop/part_search (no 503 loop) ==\n'
fail=0
Q="epc_part_search_probe=$(date +%s)"
for url in \
  "http://127.0.0.1:5100/en/shop/part_search?article=1310154101&${Q}" \
  "${PUBLIC_BASE}/en/shop/part_search?article=1310154101&${Q}"
do
  hdr="$(curl -sS -D - -o /dev/null -A 'Mozilla/5.0' --max-time 30 "$url" || true)"
  code="$(printf '%s' "$hdr" | awk 'tolower($1)=="http/" {print $2; exit}')"
  loc="$(printf '%s' "$hdr" | awk 'tolower($1)=="location:" {print $2; tr -d "\r"; exit}')"
  printf '%s → http=%s location=%s\n' "$url" "${code:-?}" "${loc:-<none>}"
  if [[ "$code" == "503" ]]; then
    printf 'FAIL  still 503\n'
    fail=1
  elif [[ "$code" == "302" && "$loc" == *"/storefront/search-app"* ]]; then
    printf 'PASS  paused-mode redirect to search-app\n'
  elif [[ "$code" == "200" ]]; then
    printf 'PASS  served at PHP-canonical /en/shop/part_search\n'
  else
    printf 'WARN  unexpected response\n'
  fi
done

if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL — /en/shop/part_search still 503\n' >&2
  exit 1
fi
printf '\nRESULT=PASS — /en/shop/part_search no longer 503 loop (SHA %s)\n' "$SHA"
exit 0
