#!/usr/bin/env bash
# FORCE live epartscart storefront fix RIGHT NOW.
# Merging PRs does nothing until this runs on the CloudPanel box as root.
#
# Paste-safe (after this lands on main):
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_NOW.sh)"
#
# What it does:
#   1) git reset checkout to origin/main
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
if ! grep -q 'StorefrontPhpCanonical.PartSearch' \
  aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor; then
  printf 'ERROR: chrome still posts search-app — checkout too old\n' >&2
  exit 1
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

if [[ "${#DOCROOTS[@]}" -eq 0 ]]; then
  printf 'WARN: no epartscart docroots discovered — will still publish :5100\n' >&2
else
  printf 'Docroots (%s):\n' "${#DOCROOTS[@]}"
  printf '  %s\n' "${DOCROOTS[@]}"
fi

# Always include repo itself (for epc-static from checkout if used)
DOCROOTS+=("$REPO")

SYNCED=0
for dir in "${DOCROOTS[@]}"; do
  [[ -d "$dir" ]] || continue
  mkdir -p "$dir/content/general_pages"
  cp -f "$REPO/epc_storefront_stub_redirect.php" "$dir/epc_storefront_stub_redirect.php"
  if [[ -f "$dir/index.php" ]]; then
    if ! grep -q 'epc_storefront_stub_redirect.php' "$dir/index.php"; then
      # Insert require right after opening / legacy guard if present
      if grep -q 'epc-ecomae-legacy-path-guard.php' "$dir/index.php"; then
        # Prefer full index.php from repo when structure matches
        cp -f "$REPO/index.php" "$dir/index.php"
      else
        cp -f "$REPO/index.php" "$dir/index.php"
      fi
    else
      # Keep stub require; still refresh stub file (done above)
      :
    fi
  fi
  cp -f "$REPO/content/general_pages/epc_storefront_professional_shell.css" \
    "$dir/content/general_pages/epc_storefront_professional_shell.css"
  cp -f "$REPO/content/general_pages/epc_storefront_professional_shell_css.php" \
    "$dir/content/general_pages/" 2>/dev/null || true
  # Public proof marker (bypass caches with unique query later)
  printf 'sha=%s time=%s\n' "$FULL" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
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
location ^~ /aspnet-php-assets/ {
    proxy_pass http://127.0.0.1:5100;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header Cookie $http_cookie;
}
# END ecomae-storefront-search-app-redirect
NGX

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
done < <(grep -Rll 'epartscart' /etc/nginx/sites-enabled /etc/nginx/sites-available /home/*/conf/nginx 2>/dev/null || true)

if [[ "$NGINX_TOUCHED" -gt 0 ]]; then
  nginx -t
  systemctl reload nginx
  printf 'nginx reloaded (%s confs touched)\n' "$NGINX_TOUCHED"
else
  printf 'WARN: no nginx confs mentioning epartscart were patched\n' >&2
fi

# Also try classic-entry installer (best-effort)
if [[ -f scripts/cloudpanel_install_classic_entry_aspnet_primary.sh ]]; then
  ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
  ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
    bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts \
    || printf 'WARN: classic-entry installer non-zero\n' >&2
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
ln -sfn "$RELEASE_DIR" "$RELEASE_ROOT/current"
printf 'current -> %s\n' "$(readlink -f "$RELEASE_ROOT/current")"

install -d /etc/systemd/system "$ENV_DIR"
install -m 0644 deploy/aspnet/ecomae-platform.service /etc/systemd/system/ecomae-platform.service
systemctl daemon-reload
systemctl enable ecomae-platform.service

# Hard bounce :5100
systemctl stop ecomae-platform.service || true
sleep 1
# Clear any orphan listener on 5100 (stale process left old binary serving)
if command -v fuser >/dev/null 2>&1; then
  fuser -k 5100/tcp 2>/dev/null || true
fi
systemctl start ecomae-platform.service
sleep 4
systemctl --no-pager --full status ecomae-platform.service || true
bash scripts/wait_for_aspnet_health.sh || true

fail=0
printf '\n== Prove LOCAL :5100 ==\n'
BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 45 http://127.0.0.1:5100/ || true)"
if ! grep -Fq 'epc-nero-shell' <<<"$BODY"; then
  BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 45 http://127.0.0.1:5100/storefront/app || true)"
fi
for needle in \
  'Catalog <span class="hidden-sm">of products</span>' \
  'action="/en/shop/part_search"' \
  'header-call-box a { background:#ef4444'
do
  if grep -Fq "$needle" <<<"$BODY"; then
    printf 'PASS local %s\n' "$needle"
  else
    printf 'FAIL local missing %s\n' "$needle"
    fail=1
  fi
done
if grep -Fq 'action="/storefront/search-app"' <<<"$BODY"; then
  printf 'FAIL local still has search-app form — OLD BINARY\n'
  fail=1
fi

LOC="$(curl -sSI -A 'Mozilla/5.0' --max-time 20 \
  'http://127.0.0.1:5100/storefront/search-app?article=1310154101' \
  | awk 'BEGIN{IGNORECASE=1} /^location:/{print $2}' | tr -d '\r' | head -1)"
printf 'local search-app Location: %s\n' "$LOC"
[[ "$LOC" == *'/en/shop/part_search'* ]] || { printf 'FAIL local search-app redirect\n'; fail=1; }

printf '\n== Prove PUBLIC %s ==\n' "$PUBLIC_BASE"
# Bust caches
Q="epc_deploy_probe=$(date +%s)"
P_BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 45 "${PUBLIC_BASE}/?${Q}" || true)"
P_HDR="$(curl -sSI -A 'Mozilla/5.0' --max-time 20 "${PUBLIC_BASE}/storefront/search-app?article=1310154101&${Q}" || true)"
printf '%s\n' "$P_HDR" | head -15

if grep -Fq 'action="/en/shop/part_search"' <<<"$P_BODY"; then
  printf 'PASS public home posts to /en/shop/part_search\n'
else
  printf 'FAIL public home still not on new binary\n'
  fail=1
fi
if grep -Fq 'action="/storefront/search-app"' <<<"$P_BODY"; then
  printf 'FAIL public home still posts to /storefront/search-app\n'
  fail=1
fi
if printf '%s' "$P_HDR" | grep -qiE '^location:.*(/en/shop/part_search|part_search)'; then
  printf 'PASS public search-app redirects to part_search\n'
elif printf '%s' "$P_HDR" | grep -qiE '^HTTP/.* 404'; then
  printf 'FAIL public search-app still 404 (PHP stub / nginx redirect not live)\n'
  fail=1
else
  printf 'FAIL public search-app unexpected response\n'
  fail=1
fi

MARKER="$(curl -sS -A 'Mozilla/5.0' --max-time 15 "${PUBLIC_BASE}/epc-live-deploy-marker.txt?${Q}" || true)"
printf 'public marker: %s\n' "$MARKER"
if [[ "$MARKER" == *"$FULL"* ]] || [[ "$MARKER" == *"$SHA"* ]]; then
  printf 'PASS public deploy marker SHA matches checkout\n'
else
  printf 'WARN public marker missing/mismatch — www docroot may not be the live root\n'
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
  printf '\nRESULT=FAIL — live ASP.NET / is still stale (PHP /en/ sync is NOT enough).\n' >&2
  printf 'Do NOT mark deploy complete. Paste this whole log.\n' >&2
  printf 'Debug:\n' >&2
  printf '  readlink -f %s/current; ls -l %s/current/platform/EcomAE.Platform.dll\n' "$RELEASE_ROOT" "$RELEASE_ROOT" >&2
  printf '  systemctl cat ecomae-platform.service | sed -n "1,20p"\n' >&2
  printf '  ss -lntp | grep 5100 || netstat -lntp | grep 5100\n' >&2
  printf '  curl -sS http://127.0.0.1:5100/ | grep -o "action=\\\"[^\"]*\\\"" | sort -u | head\n' >&2
  printf '  journalctl -u ecomae-platform.service -n 100 --no-pager\n' >&2
  printf '  bash scripts/cloudpanel_discover_epartscart_nginx_conf.sh | head -100\n' >&2
  exit 1
fi

write_marker_status pass 'public-home-and-search-redirect-proven'
printf '\nRESULT=PASS — public epartscart shows new storefront + search redirect (SHA %s)\n' "$SHA"
exit 0
