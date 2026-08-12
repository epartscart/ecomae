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

ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/ecomae-home-footer-parity-7b3b}"
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
        if ".bak" in conf.name.lower() or not conf.name.endswith(".conf"):
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
            # Short bak under /root/nginx-bak — never append .bak onto .bak (ENAMETOOLONG).
            bak_dir = Path("/root/nginx-bak")
            bak_dir.mkdir(parents=True, exist_ok=True)
            base_name = conf.name.split(".bak", 1)[0]
            if not base_name.endswith(".conf"):
                base_name += ".conf"
            bak = bak_dir / f"{base_name}.www-marketing-home.{time.strftime('%Y%m%d%H%M%S')}.bak"
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
prove home-footer "$WWW_BASE/" 'epm-footer'
prove home-footer-legal "$WWW_BASE/" 'epm-footer__legal'
prove home-trust "$WWW_BASE/" 'id="trust"'
prove home-layla-css-link "$WWW_BASE/" '/platform-assets/epc_ecomae_layla_widget.css'
prove home-demo-css-link "$WWW_BASE/" '/platform-assets/epc_ecomae_demo_portal.css'
prove home-layla-avatar-link "$WWW_BASE/" '/platform-assets/layla-avatar.svg'
prove home-not-storefront "$WWW_BASE/" 'ehm-'
prove home-verify-aspnet "$WWW_BASE/" '/blockchain/verify'
# Negative: storefront nero marker must not dominate home
tmpn="$(mktemp)"
curl -sS -o "$tmpn" --connect-timeout 20 -A 'Mozilla/5.0' "$WWW_BASE/?epc_neg=$(date +%s)" || true
if grep -qi 'content="storefront"' "$tmpn" && ! grep -qi 'content="marketing"' "$tmpn"; then
  printf 'FAIL home still chrome-surface storefront\n'
  fail=1
else
  printf 'PASS home-not-chrome-storefront\n'
fi
# Home sections must contain real PHP-parity body (not an empty shell).
if ! grep -Fq 'Prove every critical' "$tmpn"; then
  printf 'FAIL home-sections body thin (missing blockchain proof copy)\n'
  fail=1
else
  printf 'PASS home-sections-body\n'
fi
# Product HTML must not advertise active .php entrypoints (PHP is reference-only).
if grep -Eoq '/epc-blockchain-verify\.php|/epc-static\.php' "$tmpn"; then
  printf 'FAIL home still emits product .php URLs\n'
  fail=1
else
  printf 'PASS home-no-product-php-urls\n'
fi
rm -f "$tmpn"

# index.php / legacy verify.php must hit ASP.NET (302→/ or verify), never PHP marketing-home v2.
idx_code="$(curl -sS -o /dev/null -w '%{http_code}' --connect-timeout 20 -A 'Mozilla/5.0' \
  "$WWW_BASE/index.php?epc_prove=$(date +%s)" || echo 000)"
idx_loc="$(curl -sS -o /dev/null -D - --connect-timeout 20 -A 'Mozilla/5.0' \
  "$WWW_BASE/index.php?epc_prove=$(date +%s)" 2>/dev/null | grep -i '^location:' | tr -d '\r' | awk '{print $2}' | head -1 || true)"
if [[ "$idx_code" == "302" || "$idx_code" == "301" ]] && grep -Eq '^/$|^/\?' <<<"${idx_loc:-}"; then
  printf 'PASS index-php-redirects-home http=%s loc=%s\n' "$idx_code" "$idx_loc"
elif [[ "$idx_code" == "200" ]]; then
  # Accept 200 only when ASP.NET primary (never x-ecomae-marketing-home PHP marker).
  hdr="$(mktemp)"; body="$(mktemp)"
  curl -sS -D "$hdr" -o "$body" --connect-timeout 20 -A 'Mozilla/5.0' "$WWW_BASE/index.php?epc_prove=$(date +%s)" || true
  if grep -qi 'x-ecomae-marketing-home' "$hdr" || grep -Fq 'ECOMAE-MARKETING-HOME-v8' "$body"; then
    printf 'FAIL index.php still serves PHP marketing home\n'; fail=1
  elif grep -qi 'x-ecomae-platform: primary' "$hdr" || grep -qi 'blazor-enhanced-nav' "$hdr"; then
    printf 'PASS index-php-aspnet-primary\n'
  else
    printf 'FAIL index.php ambiguous product response\n'; fail=1
  fi
  rm -f "$hdr" "$body"
else
  printf 'FAIL index.php http=%s loc=%s\n' "$idx_code" "${idx_loc:-none}"; fail=1
fi

prove blockchain-verify-ui "$WWW_BASE/blockchain/verify" 'Verify a business proof'

prove css-marketing "$WWW_BASE/platform-assets/epc_ecomae_platform_marketing.css" 'epm-hub' 'text/css'
prove css-sections "$WWW_BASE/platform-assets/epc_ecomae_home_sections.css" 'epc-ehm-rev-fallback' 'text/css'
prove css-lifeos-film "$WWW_BASE/platform-assets/epc_ecomae_marketing_lifeos_film.css" 'epm-lofilm' 'text/css'
prove css-layla "$WWW_BASE/platform-assets/epc_ecomae_layla_widget.css" 'epc-layla-splash--hidden' 'text/css'
prove css-demo-portal "$WWW_BASE/platform-assets/epc_ecomae_demo_portal.css" 'epc-demo-portal' 'text/css'
prove mark-svg "$WWW_BASE/platform-assets/ecomae-mark.svg" '' 'image/svg'
prove layla-avatar "$WWW_BASE/platform-assets/layla-avatar.svg" '' 'image/svg'

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
