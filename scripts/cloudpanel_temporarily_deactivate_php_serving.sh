#!/usr/bin/env bash
# Temporarily deactivate PHP HTTP serving (including /php-reference/*) so ASP.NET
# Core can be deep-tested. Does NOT delete PHP files. Does NOT flip cutover /
# readyForPhpRemoval. KeepPhpProjectAvailable stays true.
#
#   ECOMAE_CONFIRM_TEMP_DEACTIVATE_PHP_SERVING=YES \
#     bash scripts/cloudpanel_temporarily_deactivate_php_serving.sh
#
# Restore:
#   ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES \
#     bash scripts/cloudpanel_restore_php_reference_serving.sh
set -euo pipefail

if [[ "${ECOMAE_CONFIRM_TEMP_DEACTIVATE_PHP_SERVING:-}" != "YES" ]]; then
  printf 'REFUSE: set ECOMAE_CONFIRM_TEMP_DEACTIVATE_PHP_SERVING=YES\n' >&2
  printf 'This pauses PHP HTTP serving for ASP.NET deep testing only.\n' >&2
  printf 'It does not invent ReadyToRemovePhp / cutoverAllowed / PHP source deletion.\n' >&2
  exit 2
fi

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/temp-deactivate-php-serving-7b3b}"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
FLAG_ETC="/etc/ecomae-aspnet/php_serving_deactivated"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== TEMP DEACTIVATE PHP HTTP SERVING (%s) ========\n' "$ECOMAE_BRANCH"

REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
if [[ -z "$REPO" ]]; then
  printf 'ERROR: repo checkout not found\n' >&2
  exit 1
fi

cd "$REPO"
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
printf 'REPO=%s SHA=%s\n' "$REPO" "$SHA"

mkdir -p /etc/ecomae-aspnet
printf 'status=php-serving-temporarily-deactivated sha=%s time=%s\nkeepPhpProjectAvailable=true\ncutoverAllowed=false\nreadyForPhpRemoval=false\n' \
  "$(git rev-parse HEAD)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$FLAG_ETC"
chmod 644 "$FLAG_ETC"

# Docroot flags (nginx / PHP-FPM see these)
mapfile -t DOCROOTS < <(
  python3 - <<'PY' 2>/dev/null || true
import pathlib, re
roots=set()
for base in (pathlib.Path('/etc/nginx'), pathlib.Path('/home')):
    if not base.exists():
        continue
    for p in base.rglob('*.conf'):
        try:
            text=p.read_text(errors='ignore')
        except Exception:
            continue
        if 'epartscart' not in text.lower() and 'ecomae' not in text.lower():
            continue
        for m in re.finditer(r'root\s+([^;]+);', text):
            root=m.group(1).strip().strip('"').strip("'")
            if root.startswith('/') and pathlib.Path(root).is_dir():
                roots.add(root)
for r in sorted(roots):
    print(r)
PY
)
# Always include common CloudPanel roots
for extra in \
  /home/epartscart/htdocs/www.epartscart.com \
  /home/ecomae/htdocs/www.ecomae.com \
  /var/www/epartscart \
  /var/www/ecomae
do
  [[ -d "$extra" ]] && DOCROOTS+=("$extra")
done

# unique
mapfile -t DOCROOTS < <(printf '%s\n' "${DOCROOTS[@]}" | awk 'NF && !seen[$0]++')
for dir in "${DOCROOTS[@]}"; do
  [[ -d "$dir" ]] || continue
  printf 'status=php-serving-temporarily-deactivated sha=%s\n' "$(git rev-parse HEAD)" \
    > "$dir/.epc_php_serving_deactivated"
  # Sync ops helper into docroot if missing
  if [[ -f "$REPO/content/general_pages/epc_php_serving_deactivate.php" ]]; then
    mkdir -p "$dir/content/general_pages"
    cp -f "$REPO/content/general_pages/epc_php_serving_deactivate.php" \
      "$dir/content/general_pages/epc_php_serving_deactivate.php"
  fi
  if [[ -f "$REPO/index.php" ]]; then
    cp -f "$REPO/index.php" "$dir/index.php"
  fi
  if [[ -f "$REPO/content/general_pages/epc_php_reference_router.php" ]]; then
    cp -f "$REPO/content/general_pages/epc_php_reference_router.php" \
      "$dir/content/general_pages/epc_php_reference_router.php"
  fi
  printf 'FLAG docroot %s\n' "$dir"
done

# platform.env — ASP.NET prefers storefront apps; 503 /php-reference via middleware
if [[ -f "$ENV_FILE" ]]; then
  cp -a "$ENV_FILE" "${ENV_FILE}.bak.temp-php-off.$(date +%Y%m%d%H%M%S)"
  python3 - <<PY
from pathlib import Path
p = Path("$ENV_FILE")
lines = p.read_text().splitlines()
keys = {
  "EcomAE__PhpReference__TemporarilyDeactivatePhpServing": "true",
  "EcomAE__PhpReference__KeepPhpProjectAvailable": "true",
  "EcomAE__PhpReference__Mode": "aspnet-only-deep-test-php-serving-off",
  "MigrationRouteCutover__StorefrontAspNetEnabled": "true",
  "MigrationRouteCutover__AdminAspNetEnabled": "true",
  "MigrationRouteCutover__RequirePhpFallback": "true",
}
out = []
seen = set()
for line in lines:
    if not line.strip() or line.lstrip().startswith("#") or "=" not in line:
        out.append(line)
        continue
    k = line.split("=", 1)[0].strip()
    if k in keys:
        out.append(f"{k}={keys[k]}")
        seen.add(k)
    else:
        out.append(line)
for k, v in keys.items():
    if k not in seen:
        out.append(f"{k}={v}")
p.write_text("\n".join(out) + "\n")
print("updated", p)
PY
else
  printf 'WARN: %s missing — create from deploy/aspnet/platform.env.example\n' "$ENV_FILE" >&2
fi

# Nginx: 503 for /php-reference/* and residual /en/ commerce (product URLs stay on :5100)
NGINX_SNIPPET=/etc/nginx/snippets/ecomae-php-serving-temporarily-deactivated.conf
mkdir -p /etc/nginx/snippets
cat > "$NGINX_SNIPPET" <<'NGINX'
# Generated by cloudpanel_temporarily_deactivate_php_serving.sh
# Include inside epartscart/ecomae server blocks (before PHP location).
location ^~ /php-reference {
    default_type text/plain;
    add_header X-EcomAE-Php-Serving temporarily-deactivated always;
    add_header X-EcomAE-Keep-Php-Project true always;
    add_header X-EcomAE-Cutover-Allowed false always;
    add_header X-EcomAE-Ready-For-Php-Removal false always;
    return 503 "PHP reference serving temporarily deactivated for ASP.NET deep testing.\n";
}
location ^~ /en/ {
    default_type text/plain;
    add_header X-EcomAE-Php-Serving temporarily-deactivated always;
    return 503 "Interim PHP /en/ commerce pages paused — use ASP.NET /storefront/* apps.\n";
}
location ^~ /me/ {
    default_type text/plain;
    add_header X-EcomAE-Php-Serving temporarily-deactivated always;
    return 503 "Interim PHP lang commerce pages paused — use ASP.NET /storefront/* apps.\n";
}
location ^~ /ru/ {
    default_type text/plain;
    add_header X-EcomAE-Php-Serving temporarily-deactivated always;
    return 503 "Interim PHP lang commerce pages paused — use ASP.NET /storefront/* apps.\n";
}
NGINX
printf 'Wrote %s — include in server blocks if not already wired by classic-entry\n' "$NGINX_SNIPPET"

# Best-effort include into known mega-conf / tenant confs
python3 - <<'PY'
from pathlib import Path
snippet = "include /etc/nginx/snippets/ecomae-php-serving-temporarily-deactivated.conf;"
marker = "# ecomae-temp-php-serving-off"
candidates = list(Path("/etc/nginx/sites-enabled").glob("*")) + list(Path("/etc/nginx/conf.d").glob("*"))
for conf in candidates:
    if not conf.is_file():
        continue
    try:
        text = conf.read_text(errors="ignore")
    except Exception:
        continue
    if "epartscart" not in text.lower() and "ecomae" not in text.lower():
        continue
    if marker in text or snippet in text:
        print("already:", conf)
        continue
    # Insert after each server_name line block start — simple: after first "server {"
    if "server {" not in text:
        continue
    parts = text.split("server {", 1)
    if len(parts) != 2:
        continue
    new = parts[0] + "server {\n    " + marker + "\n    " + snippet + "\n" + parts[1]
    bak = conf.with_suffix(conf.suffix + ".bak.temp-php-off")
    bak.write_text(text)
    conf.write_text(new)
    print("patched:", conf, "bak:", bak)
PY

nginx -t
systemctl reload nginx

# Republish ASP.NET so PreferAspNetApps is live
if [[ -x "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh" ]]; then
  export ECOMAE_BRANCH
  # FORCE LIVE has its own prove; if it fails we still leave flags (operator can diagnose)
  bash "$REPO/scripts/cloudpanel_FORCE_LIVE_NOW.sh" || true
else
  systemctl restart ecomae-platform.service || true
fi

printf '\n== Prove ==\n'
Q="epc_php_off=$(date +%s)"
code_ref="$(curl -sS -o /tmp/epc_php_ref_off.txt -w '%{http_code}' -A 'Mozilla/5.0' --max-time 20 \
  "https://www.epartscart.com/php-reference/storefront?${Q}" || true)"
code_en="$(curl -sS -o /tmp/epc_php_en_off.txt -w '%{http_code}' -A 'Mozilla/5.0' --max-time 20 \
  "https://www.epartscart.com/en/shop/part_search?${Q}" || true)"
code_home="$(curl -sS -o /tmp/epc_php_home.txt -w '%{http_code}' -A 'Mozilla/5.0' --max-time 45 \
  "https://www.epartscart.com/?${Q}" || true)"
printf 'php-reference/storefront http=%s\n' "$code_ref"
printf '/en/shop/part_search http=%s\n' "$code_en"
printf '/ http=%s\n' "$code_home"
curl -sS -A 'Mozilla/5.0' --max-time 20 "https://www.epartscart.com/migration/php-reference-mode?${Q}" | head -c 800 || true
printf '\n'

fail=0
[[ "$code_ref" == "503" ]] || { printf 'FAIL php-reference not 503\n'; fail=1; }
[[ "$code_en" == "503" ]] || { printf 'FAIL /en/ not 503\n'; fail=1; }
[[ "$code_home" == "200" ]] || { printf 'WARN home http=%s (ASP.NET should still answer)\n' "$code_home"; }

if [[ "$fail" -ne 0 ]]; then
  cat <<EOF >&2
RESULT=FAIL — PHP serving pause incomplete
Check nginx include + platform.env TemporarilyDeactivatePhpServing=true + flag $FLAG_ETC
EOF
  exit 1
fi

cat <<EOF

#####################################################################
#  RESULT=PASS — PHP HTTP serving temporarily deactivated
#  ASP.NET product URLs stay up; /php-reference and /en/ return 503
#  KeepPhpProjectAvailable=true · cutoverAllowed=false · readyForPhpRemoval=false
#  No new PHP feature work — fix gaps in ASP.NET Core only
#  Restore when done:
#    ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES \\
#      bash scripts/cloudpanel_restore_php_reference_serving.sh
#####################################################################
EOF
exit 0
