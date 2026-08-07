#!/usr/bin/env bash
# FORCE live epartscart storefront fix RIGHT NOW.
# Merging PRs does nothing until this runs on the CloudPanel box as root.
#
# Paste-safe:
#   ECOMAE_BRANCH=cursor/storefront-header-topmenu-visible-7b3b \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/storefront-header-topmenu-visible-7b3b/scripts/cloudpanel_FORCE_LIVE_NOW.sh)"
# After merge:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_NOW.sh)"
#
# What it does:
#   1) git reset checkout to origin/$ECOMAE_BRANCH
#   2) sync PHP stub redirect + CSS into EVERY epartscart nginx root
#   3) inject nginx 302 for /storefront/search-app → /en/shop/part_search
#   4) DIRECT dotnet publish + restart ecomae-platform (:5100)
#   5) FAIL unless public https://www.epartscart.com/ shows the new chrome
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
PUBLIC_BASE="${ECOMAE_PUBLIC_BASE:-https://www.epartscart.com}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== FORCE LIVE STOREFRONT NOW (%s) ========\n' "$ECOMAE_BRANCH"

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

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

# Refuse ancient tips
if ! grep -q 'epc_storefront_stub_redirect_maybe_exit' index.php; then
  printf 'ERROR: index.php missing stub redirect — checkout is not main tip\n' >&2
  exit 1
fi
if ! grep -q 'header-call-box a { background:#ef4444' \
  aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor; then
  printf 'ERROR: chrome missing inline PHP-look CSS — checkout too old\n' >&2
  exit 1
fi
# NOTE: chrome switched from StorefrontPhpCanonical.* literals to the mode-aware
# StorefrontSurfaceLinks.* wrapper — the old literal guard false-failed every deploy.
if ! grep -q 'StorefrontSurfaceLinks.PartSearch' \
  aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor; then
  printf 'ERROR: chrome missing StorefrontSurfaceLinks.PartSearch — checkout too old\n' >&2
  exit 1
fi
if ! grep -q 'PhpAspHomeBanners' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPreviewApp.razor; then
  printf 'ERROR: home missing PHP-parity sections (#892) — checkout too old\n' >&2
  exit 1
fi
if ! grep -q 'epc-garage-header-link' \
  aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor; then
  printf 'ERROR: chrome missing Garage Manager PHP class — checkout too old\n' >&2
  exit 1
fi
if ! grep -q 'background:linear-gradient(135deg,#090f1d' \
  aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor; then
  printf 'ERROR: chrome missing PHP top-menu gradient — checkout too old\n' >&2
  exit 1
fi
if ! grep -q 'color:rgba(255,255,255,.88) !important' \
  aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor; then
  printf 'ERROR: chrome missing top-menu visible CSS (nero dark-gray beat) — checkout too old\n' >&2
  exit 1
fi
if ! grep -q 'AdminSurfaceAuthGateMiddleware' aspnet/src/EcomAE.Platform/Program.cs; then
  printf 'ERROR: AdminSurfaceAuthGateMiddleware not wired — CP guest-browse leak\n' >&2
  exit 1
fi
if grep -q 'data-epc-guest-browse' aspnet/src/EcomAE.Platform/Components/Shared/LegacyAdminLoginForm.razor; then
  printf 'ERROR: guest-browse bypass still on login form — confidential leak\n' >&2
  exit 1
fi
if ! grep -q '@page "/cp/control"' aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor; then
  printf 'ERROR: /cp/control not owned by Command Centre — presentation split vs /cp\n' >&2
  exit 1
fi

# ---- Re-render tenant home snapshots with live settings (php on CloudPanel) ----
# electronicae / stylenlook / thejewellerytrend / taxofinca homes are PHP-rendered
# snapshots served by ASP.NET; regenerating here picks up live site settings + menus.
if command -v php >/dev/null 2>&1 && [[ -f scripts/render_php_home_snapshots.php ]]; then
  php scripts/render_php_home_snapshots.php \
    || printf 'WARN: tenant home snapshot render failed — committed snapshots serve\n' >&2
fi
# Full www.ecomae.com marketing site (all pages) — PHP router renders each page.
if command -v php >/dev/null 2>&1 && [[ -f scripts/render_ecomae_marketing_snapshots.php ]]; then
  php scripts/render_ecomae_marketing_snapshots.php >/tmp/epc-marketing-snapshots.log 2>&1 \
    && tail -1 /tmp/epc-marketing-snapshots.log \
    || printf 'WARN: marketing snapshot render failed — committed snapshots serve\n' >&2
fi

# ---- Discover epartscart document roots from nginx ----
mapfile -t DOCROOTS < <(
  {
    # From server blocks that mention epartscart
    python3 - <<'PY' 2>/dev/null || true
import re, pathlib
roots=set()
for base in (pathlib.Path('/etc/nginx'), pathlib.Path('/home')):
    if not base.exists():
        continue
    for p in base.rglob('*.conf'):
        try:
            text=p.read_text(errors='ignore')
        except Exception:
            continue
        if 'epartscart' not in text.lower():
            continue
        for m in re.finditer(r'^\s*root\s+([^;]+);', text, re.M):
            root=m.group(1).strip().strip('"\'')
            if root:
                roots.add(root)
for r in sorted(roots):
    print(r)
PY
    # Common CloudPanel paths
    ls -d /home/*/htdocs/www.epartscart.com 2>/dev/null || true
    ls -d /home/*/htdocs/epartscart.com 2>/dev/null || true
    ls -d /var/www/epartscart* 2>/dev/null || true
    ls -d /var/www/*/epartscart* 2>/dev/null || true
  } | sed '/^$/d' | sort -u
)

# Normalize + filter discovered roots:
# - drop the repo checkout itself (cp source == dest fails and set -e kills the run)
# - drop garbage doubled paths from malformed nginx root lines (…/htdocs/home/…)
# - only sync REAL existing PHP docroots (must already contain index.php); never mkdir new trees
REPO_REAL="$(readlink -f "$REPO")"
FILTERED=()
for dir in "${DOCROOTS[@]}"; do
  [[ -d "$dir" ]] || continue
  real="$(readlink -f "$dir")"
  [[ "$real" == "$REPO_REAL" ]] && continue
  [[ "$real" == *"/htdocs/home/"* ]] && continue
  [[ -f "$real/index.php" ]] || continue
  FILTERED+=("$real")
done
mapfile -t DOCROOTS < <(printf '%s\n' "${FILTERED[@]+"${FILTERED[@]}"}" | sed '/^$/d' | sort -u)

if [[ "${#DOCROOTS[@]}" -eq 0 ]]; then
  printf 'WARN: no epartscart docroots discovered — will still publish :5100\n' >&2
else
  printf 'Docroots (%s):\n' "${#DOCROOTS[@]}"
  printf '  %s\n' "${DOCROOTS[@]}"
fi

SYNCED=0
for dir in "${DOCROOTS[@]+"${DOCROOTS[@]}"}"; do
  [[ -d "$dir" ]] || continue
  mkdir -p "$dir/content/general_pages"
  # PHP-side sync is best-effort — the deploy's core is publish + restart + prove.
  cp -f "$REPO/epc_storefront_stub_redirect.php" "$dir/epc_storefront_stub_redirect.php" || true
  if [[ -f "$dir/index.php" ]]; then
    if ! grep -q 'epc_storefront_stub_redirect.php' "$dir/index.php"; then
      # Insert require right after opening / legacy guard if present
      if grep -q 'epc-ecomae-legacy-path-guard.php' "$dir/index.php"; then
        # Prefer full index.php from repo when structure matches
        cp -f "$REPO/index.php" "$dir/index.php" || true
      else
        cp -f "$REPO/index.php" "$dir/index.php" || true
      fi
    else
      # Keep stub require; still refresh stub file (done above)
      :
    fi
  fi
  cp -f "$REPO/content/general_pages/epc_storefront_professional_shell.css" \
    "$dir/content/general_pages/epc_storefront_professional_shell.css" || true
  cp -f "$REPO/content/general_pages/epc_storefront_professional_shell_css.php" \
    "$dir/content/general_pages/" 2>/dev/null || true
  # DA320/ROCKY warehouse bridge — ASP.NET falls back to this when tenant SQL is empty.
  mkdir -p "$dir/content/shop/docpart"
  cp -f "$REPO/content/shop/docpart/ajax_epc_warehouse_offers.php" \
    "$dir/content/shop/docpart/ajax_epc_warehouse_offers.php" || true
  # Marker starts pending — status=pass is written ONLY after public :5100 prove.
  # A matching sha alone does NOT mean live ASP.NET was republished.
  printf 'status=pending sha=%s time=%s note=php-docroot-synced-aspnet-not-proven-yet\n' \
    "$FULL" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    > "$dir/epc-live-deploy-marker.txt"
  SYNCED=$((SYNCED + 1))
  printf '  synced → %s\n' "$dir"
done
printf 'Synced docroots: %s\n' "$SYNCED"

# ---- Inject nginx search-app redirect into every conf mentioning epartscart ----
SNIP_FILE="/tmp/epc-storefront-search-app-redirect.snip"
cat > "$SNIP_FILE" <<'NGX'
# BEGIN ecomae-storefront-search-app-redirect
location = /storefront/search-app {
    if ($arg_mode = "attr") { return 302 /en/shop/warehouse-search$is_args$args; }
    if ($arg_mode = "vin") { return 302 /en/katalog-laximo$is_args$args; }
    if ($arg_mode = "car") { return 302 /en/vehicle-catalog$is_args$args; }
    if ($arg_mode = "engine") { return 302 /en/vehicle-catalog$is_args$args; }
    if ($arg_mode = "name") { return 302 /en/shop/search$is_args$args; }
    return 302 /en/shop/part_search$is_args$args;
}
location ^~ /platform-assets/ {
  proxy_pass http://127.0.0.1:5100;
  proxy_http_version 1.1;
  proxy_set_header Host $host;
  proxy_set_header X-Forwarded-Proto $scheme;
  proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
}
location ^~ /aspnet-php-assets/ {
    proxy_pass http://127.0.0.1:5100;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header Cookie $http_cookie;
}
# END ecomae-storefront-search-app-redirect
NGX

# Paused mode: /en must reach the platform (it maps commerce links into the apps)
# instead of the PHP warm-up splash. Insert INSIDE the marker block so refresh works.
if [[ -f /etc/ecomae-aspnet/php_serving_deactivated ]]; then
  # Scrub legacy Interim /en/ 503 pause blocks left in live nginx confs.
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
backup = Path('/root/nginx-en-503-scrub-force-live-' + time.strftime('%Y%m%d%H%M%S'))
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
            text = text[:start] + '\n'.join(lines) + text[end:]
            print(f'scrubbed /en/ 503 → :5100 in {conf}')
        if text != orig:
            backup.mkdir(parents=True, exist_ok=True)
            shutil.copy2(conf, backup / conf.name)
            conf.write_text(text)
            changed += 1
print(f'force-live nginx /en/ 503 scrub: {changed} conf(s)')
PY

  python3 - <<'PY'
from pathlib import Path
p = Path('/tmp/epc-storefront-search-app-redirect.snip')
snip = p.read_text()
en_block = '''location ^~ /en/ {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header Cookie $http_cookie;
}
# END ecomae-storefront-search-app-redirect'''
p.write_text(snip.replace('# END ecomae-storefront-search-app-redirect', en_block, 1))
print('snip: /en → :5100 (php serving paused)')
PY

  # The snip only lands in epartscart-named blocks — TENANT server blocks
  # (electronicae/stylenlook/thejewellerytrend/taxofinca) need /en → :5100 too,
  # or every home/header link (/en/…) dies on the PHP warm-up splash.
  python3 - <<'PY'
import re
from pathlib import Path

EN_BLOCK = '''    # BEGIN ecomae-paused-en-to-platform
    location ^~ /en/ {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Cookie $http_cookie;
    }
    # END ecomae-paused-en-to-platform
'''

HOSTS = ('epartscart', 'electronicae', 'stylenlook', 'thejewellerytrend', 'taxofinca', 'ecomae')
SERVER = re.compile(r'(?m)^[ \t]*server\s*\{')
NAME = re.compile(r'(?im)^\s*server_name\s+([^;]+);')
EN_EXISTS = re.compile(r'(?m)^[ \t]*location\s+(?:\^~\s+)?/en/\s*\{')

def blocks(text):
    out = []
    for m in SERVER.finditer(text):
        i = text.find('{', m.start())
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

for conf in sorted(Path('/etc/nginx/sites-enabled').glob('*.conf')):
    text = conf.read_text(errors='ignore')
    orig = text
    for start, end in reversed(blocks(text)):
        body = text[start:end]
        names = ' '.join(m.group(1) for m in NAME.finditer(body)).lower()
        if not any(h in names for h in HOSTS):
            continue
        if EN_EXISTS.search(body):
            continue
        m = re.match(r'(?s)([ \t]*server\s*\{)(.*)', body)
        if not m:
            continue
        body = m.group(1) + '\n' + EN_BLOCK + m.group(2)
        text = text[:start] + body + text[end:]
        print(f'added /en → :5100 in {conf} (server_name: {names[:70]})')
    if text != orig:
        conf.write_text(text)

print('paused /en routing ensured for all product server blocks')
PY
fi

NGINX_TOUCHED=0
while IFS= read -r conf; do
  [[ -f "$conf" ]] || continue
  python3 - <<PY
from pathlib import Path
import re
p = Path("$conf")
text = p.read_text(errors="ignore")
if "epartscart" not in text.lower():
    raise SystemExit(0)
snip = Path("$SNIP_FILE").read_text()
pat = re.compile(
    r"# BEGIN ecomae-storefront-search-app-redirect.*?# END ecomae-storefront-search-app-redirect\n?",
    re.S,
)
if pat.search(text):
    text2 = pat.sub(snip, text, count=1)
    p.write_text(text2)
    print("refreshed", p)
else:
    lines = text.splitlines(True)
    out = []
    inserted = False
    for line in lines:
        out.append(line)
        if (not inserted) and "server_name" in line and "epartscart" in line.lower():
            out.append("\n" + snip + "\n")
            inserted = True
    if not inserted:
        out = []
        for line in lines:
            out.append(line)
            if (not inserted) and line.lstrip().startswith("server") and "{" in line:
                out.append("\n" + snip + "\n")
                inserted = True
    if inserted:
        p.write_text("".join(out))
        print("injected", p)
PY
  NGINX_TOUCHED=$((NGINX_TOUCHED + 1))
done < <(grep -Rll 'epartscart' /etc/nginx/sites-enabled /etc/nginx/sites-available /home/*/conf/nginx 2>/dev/null | grep -E '\.conf$' || true)
# NOTE: '\.conf$' filter — sites-enabled is littered with .bak.* copies from old
# runs; injecting into those bloats them and hides which file nginx actually uses.

# Repair known breakage: earlier packs can leave DUPLICATE exact/plain location
# selectors in one server{} (e.g. two `location = /`) — nginx -t then fails and
# every reload is silently ignored, freezing nginx on a stale in-memory config.
nginx_repair_duplicate_locations() {
  python3 - <<'PY'
import re, shutil, time
from pathlib import Path

backup_root = Path('/root/nginx-conf-repair-' + time.strftime('%Y%m%d%H%M%S'))
changed_any = False

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

SERVER_PAT = re.compile(r'(?m)^[ \t]*server\s*\{')
LOC_PAT = re.compile(r'(?m)^[ \t]*location\s+(=\s+\S+|\^~\s+\S+|/\S*|/)\s*\{')

def norm_key(sel):
    # nginx: `location /cp/` and `location ^~ /cp/` are the SAME location slot.
    parts = sel.split()
    if parts[0] == '=':
        return '= ' + parts[1]
    if parts[0] == '^~':
        return 'P ' + parts[1]
    return 'P ' + parts[0]

for conf in sorted(Path('/etc/nginx/sites-enabled').glob('*.conf')):
    try:
        text = conf.read_text(errors='ignore')
    except Exception:
        continue
    orig = text
    for s_start, s_end in reversed(find_blocks(text, SERVER_PAT)):
        body = text[s_start:s_end]
        occ = {}
        for l_start, l_end in find_blocks(body, LOC_PAT):
            m = LOC_PAT.match(body, l_start)
            if not m:
                continue
            sel = ' '.join(m.group(1).split())
            occ.setdefault(norm_key(sel), []).append((l_start, l_end, sel.startswith('^~'), sel))
        drops = []
        for key, items in occ.items():
            if len(items) < 2:
                continue
            keep = next((i for i, it in enumerate(items) if it[2]), 0)
            for i, it in enumerate(items):
                if i != keep:
                    drops.append(it)
                    print(f'repair: dropping duplicate location {it[3]} in {conf}')
        for l_start, l_end, _, _ in sorted(drops, key=lambda t: t[0], reverse=True):
            body = body[:l_start] + body[l_end:]
        text = text[:s_start] + body + text[s_end:]
    if text != orig:
        backup_root.mkdir(parents=True, exist_ok=True)
        shutil.copy2(conf, backup_root / conf.name)
        conf.write_text(text)
        changed_any = True
        print(f'repaired {conf} (backup in {backup_root})')

print('repair-changed=' + ('yes' if changed_any else 'no'))
PY
}

if nginx -t 2>&1 | tee /tmp/epc-nginx-t.log; then
  systemctl reload nginx
  printf 'nginx reloaded (%s confs touched)\n' "$NGINX_TOUCHED"
else
  printf 'WARN: nginx -t failed — attempting duplicate-location repair\n' >&2
  nginx_repair_duplicate_locations || true
  if nginx -t 2>&1 | tee /tmp/epc-nginx-t.log; then
    systemctl reload nginx
    printf 'nginx repaired + reloaded\n'
  else
    printf 'WARN: nginx config STILL failing after repair — residual errors:\n' >&2
    grep -E 'emerg|error' /tmp/epc-nginx-t.log >&2 || true
    printf 'WARN: continuing to publish; the RUNNING nginx keeps its old config\n' >&2
  fi
fi

# Classic-entry: public / MUST proxy to :5100/storefront/app.
# Do NOT abort the whole deploy if this step fails — / is usually already routed
# to :5100 and the stale binary is the real problem; publish + restart must happen.
# The final public prove still decides PASS/FAIL.
if [[ -f scripts/cloudpanel_install_classic_entry_aspnet_primary.sh ]]; then
  if ! ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
       ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
       bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts; then
    printf 'WARN: classic-entry installer failed — continuing to publish; final prove decides\n' >&2
  fi
else
  printf 'WARN: classic-entry installer script missing — continuing to publish\n' >&2
fi

# lifeos.ecomae.com must NOT reuse www `location = /` → /marketing/app.
if [[ -f scripts/cloudpanel_install_lifeos_host_nginx.sh ]]; then
  if ! ECOMAE_CONFIRM_INSTALL_LIFEOS_HOST_NGINX=YES \
       bash scripts/cloudpanel_install_lifeos_host_nginx.sh; then
    printf 'WARN: lifeos host nginx install failed — app middleware still diverts /marketing/app on lifeos host\n' >&2
  fi
fi

# ---- Direct ASP.NET publish ----
command -v dotnet >/dev/null || { printf 'ERROR: dotnet missing\n' >&2; exit 1; }
STAMP="$(date -u +%Y%m%d%H%M%S)"
RELEASE_DIR="$RELEASE_ROOT/releases/$STAMP"
PLATFORM_DIR="$RELEASE_DIR/platform"
WORKERS_DIR="$RELEASE_DIR/workers"
mkdir -p "$PLATFORM_DIR" "$WORKERS_DIR"

printf '== Direct publish → %s ==\n' "$RELEASE_DIR"
dotnet restore aspnet/EcomAE.AspNetCore.sln
dotnet publish aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj -c Release -o "$PLATFORM_DIR"
dotnet publish aspnet/src/EcomAE.Workers/EcomAE.Workers.csproj -c Release -o "$WORKERS_DIR"
printf '%s\n' "$FULL" > "$RELEASE_DIR/PUBLISHED_GIT_SHA.txt"
# Service runs as www-data — root-owned publish trees cause silent stale/orphan :5100.
chown -R www-data:www-data "$RELEASE_DIR"
ln -sfn "$RELEASE_DIR" "$RELEASE_ROOT/current"
chown -h www-data:www-data "$RELEASE_ROOT/current" 2>/dev/null || true
printf 'current -> %s\n' "$(readlink -f "$RELEASE_ROOT/current")"
# Prove published DLL contains the new chrome markers (not just "dotnet publish exited 0").
if ! strings "$PLATFORM_DIR/EcomAE.Platform.dll" 2>/dev/null | grep -Fq 'header-call-box a { background:#ef4444'; then
  # Razor may compile away literal; also accept PartSearch canonical token in deps/views.
  if ! rg -l 'PartSearch|part_search' "$PLATFORM_DIR" --glob '*.dll' --glob '*.json' >/dev/null 2>&1 \
     && ! grep -RFq 'part_search' "$PLATFORM_DIR" 2>/dev/null; then
    printf 'WARN: could not string-scan published output for part_search (continuing to HTTP prove)\n' >&2
  fi
fi

install -d /etc/systemd/system "$ENV_DIR"
install -m 0644 deploy/aspnet/ecomae-platform.service /etc/systemd/system/ecomae-platform.service

# Kestrel runs from the publish tree and cannot find the PHP monorepo by walking
# up — the home catalog widgets (Family Product / Epart Catalog / Available Brands /
# Original Catalog) render their fallback alerts without this.
ENV_FILE="$ENV_DIR/platform.env"
touch "$ENV_FILE"
if grep -q '^ECOMAE_PHP_SOURCE_ROOT=' "$ENV_FILE"; then
  sed -i "s|^ECOMAE_PHP_SOURCE_ROOT=.*|ECOMAE_PHP_SOURCE_ROOT=$REPO|" "$ENV_FILE"
else
  printf 'ECOMAE_PHP_SOURCE_ROOT=%s\n' "$REPO" >> "$ENV_FILE"
fi
printf 'platform.env: ECOMAE_PHP_SOURCE_ROOT=%s\n' "$REPO"

systemctl daemon-reload
systemctl enable ecomae-platform.service

# Hard bounce :5100 — kill EVERYTHING on the port, then start unit.
systemctl stop ecomae-platform.service || true
sleep 1
if command -v fuser >/dev/null 2>&1; then
  fuser -k 5100/tcp 2>/dev/null || true
fi
# Also kill stray dotnet Platform processes not under systemd.
pkill -f 'EcomAE.Platform.dll' 2>/dev/null || true
sleep 1
systemctl start ecomae-platform.service
sleep 5
systemctl --no-pager --full status ecomae-platform.service || true
if ! systemctl is-active --quiet ecomae-platform.service; then
  printf 'ERROR: ecomae-platform.service is not active after start\n' >&2
  journalctl -u ecomae-platform.service -n 80 --no-pager >&2 || true
  exit 1
fi
bash scripts/wait_for_aspnet_health.sh || {
  printf 'ERROR: ASP.NET health wait failed\n' >&2
  journalctl -u ecomae-platform.service -n 80 --no-pager >&2 || true
  exit 1
}

fail=0
# Header form action is MODE-dependent (both are the NEW binary):
#   PHP-serving mode:  action="/en/shop/part_search"
#   PHP-paused mode:   action="/storefront/search-app" (TemporarilyDeactivatePhpServing=true)
# Staleness is proven by the PHP-parity home markers (#892), not by the form action.
check_form_action() {
  # $1 = scope label, $2 = body
  if grep -Fq 'action="/en/shop/part_search"' <<<"$2" \
     || grep -Fq 'action="/storefront/search-app"' <<<"$2"; then
    printf 'PASS %s header search form action present\n' "$1"
  else
    printf 'FAIL %s missing header search form action\n' "$1"
    fail=1
  fi
}

# nginx location = / proxies to THIS path — never prove only :5100/
printf '\n== Prove LOCAL :5100/storefront/app (nginx home target) ==\n'
BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 45 http://127.0.0.1:5100/storefront/app || true)"
printf 'local /storefront/app bytes=%s\n' "${#BODY}"
for needle in \
  'epc-nero-shell' \
  'Catalog <span class="hidden-sm">of products</span>' \
  'header-call-box a { background:#ef4444' \
  'epc-asp-home-banners' \
  'section-vin' \
  'epart-front-original-data'
do
  if grep -Fq "$needle" <<<"$BODY"; then
    printf 'PASS local %s\n' "$needle"
  else
    printf 'FAIL local missing %s\n' "$needle"
    fail=1
  fi
done
check_form_action local "$BODY"

# search-app: redirect to PHP part_search (PHP mode) OR serve the app 200 (paused mode).
# GET, not HEAD — Blazor endpoints answer 405 to HEAD (curl -I false-fails a good deploy).
SA_HDR="$(curl -sS -D - -o /dev/null -A 'Mozilla/5.0' --max-time 20 \
  'http://127.0.0.1:5100/storefront/search-app?article=1310154101' || true)"
# NOTE: awk IGNORECASE is gawk-only (mawk on Debian ignores it) — use tolower() match.
LOC="$(printf '%s' "$SA_HDR" | awk 'tolower($1) == "location:" {print $2}' | tr -d '\r' | head -1)"
printf 'local search-app Location: %s\n' "${LOC:-<none>}"
if [[ "$LOC" == *'/en/shop/part_search'* ]]; then
  printf 'PASS local search-app redirects to part_search (PHP mode)\n'
elif printf '%s' "$SA_HDR" | head -1 | grep -qE ' (200|302)'; then
  printf 'PASS local search-app answers %s\n' "$(printf '%s' "$SA_HDR" | head -1 | tr -d '\r')"
else
  printf 'FAIL local search-app neither redirect nor 200\n'
  fail=1
fi

printf '\n== Prove PUBLIC %s/ (must be ASP.NET via nginx→:5100/storefront/app) ==\n' "$PUBLIC_BASE"
Q="epc_deploy_probe=$(date +%s)"
P_BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 45 "${PUBLIC_BASE}/?${Q}" || true)"
P_HDR="$(curl -sS -D - -o /dev/null -A 'Mozilla/5.0' --max-time 20 "${PUBLIC_BASE}/storefront/search-app?article=1310154101&${Q}" || true)"
printf 'public / bytes=%s\n' "${#P_BODY}"
printf '%s\n' "$P_HDR" | head -15

# Origin bypass (skip Cloudflare) — proves nginx on this box, not CDN cache.
ORIGIN_BODY=""
if ORIGIN_IP="$(curl -4 -sS --max-time 5 https://ifconfig.me/ip 2>/dev/null || true)"; then
  :
fi
ORIGIN_BODY="$(curl -4 -k -sS -A 'Mozilla/5.0' --max-time 45 \
  --resolve "www.epartscart.com:443:127.0.0.1" \
  "https://www.epartscart.com/?${Q}" 2>/dev/null || true)"
if [[ -z "$ORIGIN_BODY" ]]; then
  ORIGIN_BODY="$(curl -4 -k -sS -A 'Mozilla/5.0' --max-time 45 \
    --resolve "www.epartscart.com:443:127.0.0.1" \
    "http://127.0.0.1/" -H 'Host: www.epartscart.com' 2>/dev/null || true)"
fi
if [[ -n "$ORIGIN_BODY" ]]; then
  printf 'origin-loopback / bytes=%s\n' "${#ORIGIN_BODY}"
  check_form_action origin-loopback "$ORIGIN_BODY"
  if ! grep -Fq 'epc-asp-home-banners' <<<"$ORIGIN_BODY"; then
    printf 'FAIL origin-loopback missing PHP-parity home banners — nginx/Kestrel on THIS box is stale\n'
    fail=1
  fi
else
  printf 'WARN origin-loopback curl failed (nginx may not listen 443 on loopback)\n' >&2
fi

for needle in \
  'epc-nero-shell' \
  'ecomae-chrome-surface' \
  'header-call-box a { background:#ef4444' \
  'epc-garage-header-link' \
  'background:linear-gradient(135deg,#090f1d' \
  'Mon-Fri from 9:00 to 18:00' \
  'of products' \
  'color:rgba(255,255,255,.88) !important' \
  'Selection catalogs' \
  'Vehicle Parts intelligence AI' \
  'epc-asp-home-banners' \
  'section-vin' \
  'epart-front-original-data' \
  'id="epc-umapi"' \
  'id="epc-brands"'
do
  if grep -Fq "$needle" <<<"$P_BODY"; then
    printf 'PASS public %s\n' "$needle"
  else
    printf 'FAIL public missing %s\n' "$needle"
    fail=1
  fi
done
check_form_action public "$P_BODY"
if grep -Fq 'epc-sf-home-depth' <<<"$P_BODY"; then
  printf 'FAIL public home still renders pre-#892 scaffold (OLD :5100 BINARY)\n'
  fail=1
fi
if grep -Fq 'Mon–Sat 9:00' <<<"$P_BODY"; then
  printf 'FAIL public still shows Mon–Sat stub hours (OLD BINARY)\n'
  fail=1
fi
if printf '%s' "$P_HDR" | grep -qiE '^location:.*(/en/shop/part_search|part_search)'; then
  printf 'PASS public search-app redirects to part_search (PHP mode)\n'
elif printf '%s' "$P_HDR" | head -1 | grep -qE ' 200'; then
  printf 'PASS public search-app serves app 200 (PHP-paused mode)\n'
elif printf '%s' "$P_HDR" | grep -qiE '^HTTP/.* 404'; then
  printf 'FAIL public search-app still 404 (nginx ^~ /storefront/ route not installed)\n'
  fail=1
else
  printf 'FAIL public search-app unexpected response\n'
  fail=1
fi

# ---- Prove other tenants + surfaces (non-fatal: DNS/routing may differ per host) ----
printf '\n== Prove tenant homes + marketing + CP/ERP (WARN-only) ==\n'
prove_host_marker() {
  local url="$1" marker="$2" label="$3" body
  body="$(curl -sS -A 'Mozilla/5.0' --max-time 30 "${url}?epc_probe=$(date +%s)" 2>/dev/null || true)"
  if grep -Fq "$marker" <<<"$body"; then
    printf 'PASS %s (%s)\n' "$label" "$marker"
  else
    printf 'WARN %s missing %s (bytes=%s)\n' "$label" "$marker" "${#body}"
  fi
}
prove_host_marker https://www.electronicae.com/ 'epc-er-home' 'electronicae home'
prove_host_marker https://www.stylenlook.com/ 'epc-frn-home' 'stylenlook home'
prove_host_marker https://www.thejewellerytrend.com/ 'epc-jrk-home' 'thejewellerytrend home'
prove_host_marker https://www.taxofinca.com/ 'epc-cpi-home' 'taxofinca home'
# 'rendered from PHP package' is the snapshot comment — present ONLY when the
# platform serves the home (PHP's own render never includes it) → proves ASP.NET.
prove_host_marker https://www.electronicae.com/ 'rendered from PHP package' 'electronicae served by platform'
prove_host_marker https://www.stylenlook.com/ 'rendered from PHP package' 'stylenlook served by platform'
prove_host_marker https://www.thejewellerytrend.com/ 'rendered from PHP package' 'thejewellerytrend served by platform'
prove_host_marker https://www.taxofinca.com/ 'rendered from PHP package' 'taxofinca served by platform'
prove_host_marker https://www.ecomae.com/ 'ehm-' 'ecomae marketing home'
prove_host_marker https://www.ecomae.com/platform/pricing 'epm-topbar' 'marketing pricing page'
prove_host_marker https://www.ecomae.com/platform/faq 'epm-topbar' 'marketing faq page'
prove_host_marker https://www.ecomae.com/privacy 'epm-topbar' 'marketing privacy page'
prove_host_marker https://www.ecomae.com/compare 'epm-topbar' 'marketing compare hub'
prove_host_marker https://www.epartscart.com/cp/login 'epc-login-html-form' 'CP login'
prove_host_marker https://www.epartscart.com/erp/login 'epc-login-html-form' 'ERP login'

MARKER="$(curl -sS -A 'Mozilla/5.0' --max-time 15 "${PUBLIC_BASE}/epc-live-deploy-marker.txt?${Q}" || true)"
printf 'public marker: %s\n' "$MARKER"
if [[ "$MARKER" == *"$FULL"* ]] || [[ "$MARKER" == *"$SHA"* ]]; then
  printf 'PASS public deploy marker SHA matches checkout\n'
else
  printf 'FAIL public marker missing/mismatch — www docroot may not be the live root\n'
  fail=1
fi

printf '\nPublished SHA: %s\nRelease: %s\n' "$SHA" "$RELEASE_DIR"
printf 'Hard refresh: Ctrl+Shift+R on %s/\n' "$PUBLIC_BASE"

write_marker_status() {
  local st="$1"
  local note="$2"
  local line
  line="$(printf 'status=%s sha=%s time=%s note=%s\n' "$st" "$FULL" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$note")"
  for dir in "${DOCROOTS[@]}"; do
    [[ -d "$dir" ]] || continue
    printf '%s' "$line" > "$dir/epc-live-deploy-marker.txt"
  done
}

if [[ "$fail" -ne 0 ]]; then
  write_marker_status fail 'php-may-be-synced-but-public-aspnet-home-still-stale'
  cat <<'EOF' >&2

#####################################################################
#  RESULT=FAIL
#  PHP sync / marker / search redirect is NOT a successful deploy.
#  Public https://www.epartscart.com/ must show the PHP-parity home
#  (epc-asp-home-banners + section-vin + epart-front-original-data).
#  Paste this WHOLE log. Do NOT mark the deploy complete.
#####################################################################
EOF
  printf 'Debug:\n' >&2
  printf '  readlink -f %s/current; ls -l %s/current/platform/EcomAE.Platform.dll\n' "$RELEASE_ROOT" "$RELEASE_ROOT" >&2
  printf '  systemctl cat ecomae-platform.service | sed -n "1,20p"\n' >&2
  printf '  ss -lntp | grep 5100 || netstat -lntp | grep 5100\n' >&2
  printf '  curl -sS http://127.0.0.1:5100/storefront/app | grep -o "action=\\\"[^\"]*\\\"" | sort -u | head\n' >&2
  printf '  curl -sS https://www.epartscart.com/ | grep -o "action=\\\"[^\"]*\\\"" | sort -u | head\n' >&2
  printf '  journalctl -u ecomae-platform.service -n 100 --no-pager\n' >&2
  printf '  bash scripts/cloudpanel_discover_epartscart_nginx_conf.sh | head -100\n' >&2
  exit 1
fi

write_marker_status pass 'public-home-and-search-redirect-proven'
cat <<EOF

#####################################################################
#  RESULT=PASS — public / is new ASP.NET storefront (SHA $SHA)
#####################################################################
EOF
exit 0
