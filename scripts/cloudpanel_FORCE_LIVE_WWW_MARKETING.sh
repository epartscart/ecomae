#!/usr/bin/env bash
# Republish :5100 + hard-prove www.ecomae.com marketing home (styled epm-hub).
# Merge alone does NOT update the live binary or nginx snips.
#
# CloudPanel root paste (LifeOS film after hero + full marketing site):
#   ECOMAE_BRANCH=cursor/www-lifeos-film-frontpage-7b3b bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/www-lifeos-film-frontpage-7b3b/scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh)" 2>&1 | tee /root/force-live-www-marketing.log
#
# After merge to main:
#   ECOMAE_BRANCH=main bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh)" 2>&1 | tee /root/force-live-www-marketing.log
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/www-lifeos-film-frontpage-7b3b}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

WWW_BASE="${ECOMAE_WWW_BASE:-https://www.ecomae.com}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== FORCE LIVE WWW MARKETING (%s) ========\n' "$ECOMAE_BRANCH"
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

if [[ ! -x scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh || true
fi

set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/force-live-www-marketing-inner.log
FORCE_RC=${PIPESTATUS[0]}
set -e
printf 'FORCE_LIVE_NOW exit=%s (inner storefront RESULT may WARN; www prove below is authoritative)\n' "$FORCE_RC"

# Re-apply classic-entry so www gets /platform-assets/ + / → marketing/app from the example.
if [[ -x scripts/cloudpanel_install_classic_entry_aspnet_primary.sh ]]; then
  set +e
  ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
  ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
    bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts \
    2>&1 | tee -a /root/force-live-www-marketing-inner.log
  set -e
fi

# STOP_PRODUCT_PHP pack sits at TOP of server{} and used to force www / → storefront.
# Re-run the fixed pack so www home is marketing/app and epartscart stays storefront.
if [[ -x scripts/cloudpanel_STOP_PRODUCT_PHP_NOW.sh ]]; then
  set +e
  ECOMAE_CONFIRM_STOP_PRODUCT_PHP_NOW=YES \
  ECOMAE_BRANCH="$ECOMAE_BRANCH" \
    bash scripts/cloudpanel_STOP_PRODUCT_PHP_NOW.sh \
    2>&1 | tee /root/force-live-www-stop-php.log
  set -e
else
  printf 'WARN: STOP_PRODUCT_PHP script missing — skipping re-apply\n' >&2
fi

# Surgical safety: any www server{} still proxying / to storefront/app → marketing/app.
python3 - <<'PY'
from pathlib import Path
import re, time, shutil

WWW_HOSTS = {"www.ecomae.com", "ecomae.com"}
SERVER_START = re.compile(r"(?m)^[ \t]*server\s*\{")
SERVER_NAME = re.compile(r"(?im)^\s*server_name\s+([^;]+);")
HOME_LOC = re.compile(
    r"(?ms)^([ \t]*location\s*=\s*/\s*\{.*?proxy_pass\s+http://127\.0\.0\.1:5100/)storefront/app(;.*?X-EcomAE-Route-Cutover\s+)([^\s;]+)",
    re.S,
)

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

def is_www(names):
    variants = set()
    for h in WWW_HOSTS:
        variants.add(h)
        variants.add(h[4:] if h.startswith("www.") else "www." + h)
    return any(n in variants for n in names)

patched = 0
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
        out = text
        changed = False
        for start, end, body in sorted(find_server_blocks(text), key=lambda t: t[0], reverse=True):
            if not is_www(names_of(body)):
                continue
            new_body, n = HOME_LOC.subn(
                r"\1marketing/app\2stop-product-php-home-marketing",
                body,
                count=1,
            )
            if n:
                out = out[:start] + new_body + out[end:]
                changed = True
                print(f"surgical-home-fix {conf} → marketing/app")
        if changed:
            bak = conf.with_name(conf.name + ".bak.www-marketing-home." + time.strftime("%Y%m%d%H%M%S"))
            shutil.copy2(conf, bak)
            conf.write_text(out)
            patched += 1
print(f"surgical_patched_files={patched}")
PY

nginx -t
systemctl reload nginx
systemctl restart ecomae-platform.service || true
sleep 5

printf '\n== www.ecomae.com hard prove ==\n'
fail=0
prove() {
  local name="$1" url="$2" needle="$3" expect_ctype="${4:-}"
  local body tmp code ctype hdr
  tmp="$(mktemp)"
  hdr="$(mktemp)"
  code="$(curl -sS -D "$hdr" -o "$tmp" -w '%{http_code}' --connect-timeout 20 -A 'Mozilla/5.0' \
    "${url}?epc_prove=$(date +%s)" || echo 000)"
  body="$(cat "$tmp" 2>/dev/null || true)"
  ctype="$(grep -i '^content-type:' "$hdr" | tr -d '\r' | head -1 || true)"
  cutover="$(grep -i '^x-ecomae-route-cutover:' "$hdr" | tr -d '\r' | awk '{print $2}' | head -1 || true)"
  rm -f "$tmp" "$hdr"
  if [[ "$code" != "200" ]]; then
    printf 'FAIL %s http=%s url=%s\n' "$name" "$code" "$url"
    fail=1
    return
  fi
  if [[ -n "$expect_ctype" ]] && ! grep -qi "$expect_ctype" <<<"$ctype"; then
    printf 'FAIL %s wrong content-type %q (want %s)\n' "$name" "$ctype" "$expect_ctype"
    fail=1
    return
  fi
  if [[ -n "$needle" ]] && ! grep -Fq "$needle" <<<"$body"; then
    printf 'FAIL %s missing needle %q (http=200 cutover=%s)\n' "$name" "$needle" "${cutover:-none}"
    fail=1
    return
  fi
  printf 'PASS %s http=%s cutover=%s\n' "$name" "$code" "${cutover:-n/a}"
}

# Must be marketing surface, not nero storefront.
prove home-surface "$WWW_BASE/" 'ecomae-chrome-surface'
prove home-hub "$WWW_BASE/" 'epm-hub'
prove home-css-link "$WWW_BASE/" '/platform-assets/epc_ecomae_platform_marketing.css'
prove home-lifeos-film "$WWW_BASE/" 'epm-lofilm'
prove home-lifeos-title "$WWW_BASE/" 'Understand the LifeOS platform'
prove home-lifeos-video "$WWW_BASE/" '/lifeos/media/lifeos-daily-clone-routine.mp4'
prove home-sections "$WWW_BASE/" 'ehm-home-sections'
prove home-not-storefront "$WWW_BASE/" 'ehm-'
# Negative: storefront nero marker must not dominate home
tmpn="$(mktemp)"
curl -sS -o "$tmpn" --connect-timeout 20 -A 'Mozilla/5.0' "$WWW_BASE/?epc_neg=$(date +%s)" || true
if grep -qi 'content="storefront"' "$tmpn" && ! grep -qi 'content="marketing"' "$tmpn"; then
  printf 'FAIL home still chrome-surface storefront\n'
  fail=1
else
  printf 'PASS home-not-chrome-storefront\n'
fi
rm -f "$tmpn"

prove css-marketing "$WWW_BASE/platform-assets/epc_ecomae_platform_marketing.css" 'epm-hub' 'text/css'
prove css-sections "$WWW_BASE/platform-assets/epc_ecomae_home_sections.css" '' 'text/css'
prove css-lifeos-film "$WWW_BASE/platform-assets/epc_ecomae_marketing_lifeos_film.css" 'epm-lofilm' 'text/css'
prove mark-svg "$WWW_BASE/platform-assets/ecomae-mark.svg" '' 'image/svg'

# Full-site PHP snapshot pages must be styled (rewritten to platform-assets).
for path in /platform /platform/about /platform/pricing /platform/industries /documentation /compare /blockchain /solutions /legal /privacy /about /contact /industries; do
  prove "page${path//\//-}" "$WWW_BASE$path" 'epm-topbar'
  prove "page${path//\//-}-css" "$WWW_BASE$path" '/platform-assets/epc_ecomae_platform_marketing.css'
done

if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL — www marketing site still broken (see proves above)\n'
  printf 'Hint: nginx -T | grep -nE \"location = /|platform-assets|STOP-PRODUCT|classic-entry-home\"\n'
  exit 1
fi

printf '\nRESULT=PASS — www.ecomae.com full marketing site + LifeOS film after hero (SHA=%s)\n' "$SHA"
printf 'Open: %s/\n' "$WWW_BASE"
exit 0
