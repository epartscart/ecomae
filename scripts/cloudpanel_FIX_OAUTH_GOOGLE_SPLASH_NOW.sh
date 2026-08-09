#!/usr/bin/env bash
# Fix Google storefront OAuth showing "Loading — Please wait" splash.
#
# Root cause: /api/epc_oauth_start.php returns 503 when Google is not configured
# (or was swallowed by nginx fastcgi_intercept_errors → epc-platform-splash.html).
# #975-era live: unknown provider → plain 400; provider=google → splash HTML.
#
# This script:
#   1) Pulls latest main (422 status + oauth nginx locations + translate JS)
#   2) Injects location = /api/epc_oauth_*.php { fastcgi_intercept_errors off; }
#   3) Reloads nginx, syncs api/epc_oauth_start.php into tenant docroots if present
#   4) Proves start is NOT splash (422 unconfigured OR 302 to Google when configured)
#
# Paste as root:
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/oauth-google-lang-fix-7b3b/scripts/cloudpanel_FIX_OAUTH_GOOGLE_SPLASH_NOW.sh'
#   TMP=/tmp/fix-oauth-google-splash-now.sh
#   curl -fsSL "$URL" -o "$TMP"
#   grep -q FIX_OAUTH_GOOGLE_SPLASH_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   bash "$TMP" 2>&1 | tee /root/fix-oauth-google-splash-now.log
#   grep -E 'RESULT=|GATE_|OAUTH_|SHA=|ERROR|FAIL|configured' /root/fix-oauth-google-splash-now.log | tail -80
set -euo pipefail

printf '======== FIX_OAUTH_GOOGLE_SPLASH_NOW ========\n'
printf 'HOST=%s DATE_UTC=%s\n' "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" -eq 0 ]] || die "must_run_as_root"

BRANCH="${ECOMAE_BRANCH:-cursor/oauth-google-lang-fix-7b3b}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  [[ -n "$d" && -d "$d/.git" ]] && REPO="$d" && break
done
[[ -n "$REPO" ]] || die "repo_not_found"

cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$BRANCH" || die "git_fetch_failed"
git checkout -f "$BRANCH" || die "git_checkout_failed"
git reset --hard "origin/$BRANCH" || die "git_reset_failed"
SHA="$(git rev-parse --short HEAD)"
printf 'REPO=%s SHA=%s BRANCH=%s\n' "$REPO" "$SHA" "$BRANCH"

# Sync oauth start PHP into common docroots (product /api still PHP-FPM).
SRC_START="$REPO/api/epc_oauth_start.php"
[[ -f "$SRC_START" ]] || die "missing_api_epc_oauth_start"
grep -q 'http_response_code(422)' "$SRC_START" || die "oauth_start_missing_422_fix"
while IFS= read -r dest; do
  [[ -d "$dest" ]] || continue
  cp -a "$SRC_START" "$dest/epc_oauth_start.php"
  printf 'SYNC_OAUTH_START %s\n' "$dest/epc_oauth_start.php"
done < <(find /home /var/www -maxdepth 5 -type d -name api 2>/dev/null | head -40)

# Sync translate JS into platform-assets / content mirrors when present
for js in epc_google_translate_storefront.js epc_google_translate_cp.js; do
  src="$REPO/content/general_pages/$js"
  [[ -f "$src" ]] || continue
  while IFS= read -r dest; do
    cp -a "$src" "$dest"
    printf 'SYNC_TRANSLATE %s\n' "$dest"
  done < <(find /home /var/www /opt -maxdepth 8 -type f -name "$js" 2>/dev/null | head -40)
done

# Inject nginx exact locations (idempotent)
python3 - <<'PY'
from pathlib import Path
import re, time
marker = "# ecomae-oauth-no-splash"
snippet = """
  # ecomae-oauth-no-splash
  location = /api/epc_oauth_start.php {
    include fastcgi_params;
    fastcgi_intercept_errors off;
    fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
    fastcgi_pass 127.0.0.1:PHP_FPM_PORT;
  }
  location = /api/epc_oauth_callback.php {
    include fastcgi_params;
    fastcgi_intercept_errors off;
    fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
    fastcgi_pass 127.0.0.1:PHP_FPM_PORT;
  }
"""
changed = 0
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
        if "epartscart" not in text.lower() and "ecomae" not in text.lower() and "server_name" not in text:
            continue
        if marker in text:
            print("nginx_already", conf)
            continue
        # Detect php-fpm port from existing fastcgi_pass
        m = re.search(r"fastcgi_pass\s+127\.0\.0\.1:(\d+)", text)
        port = m.group(1) if m else "9000"
        block = snippet.replace("PHP_FPM_PORT", port)
        # Insert before first location ~ \.php$
        new, n = re.subn(r"(location\s+~\s+\\\.php\$\s*\{)", block + r"\1", text, count=1)
        if n == 0:
            # append before last closing brace of server if possible
            print("nginx_skip_no_php_location", conf)
            continue
        bak = conf.with_name(conf.name + ".bak.oauth-splash." + time.strftime("%Y%m%d%H%M%S"))
        bak.write_text(text)
        conf.write_text(new)
        changed += 1
        print("nginx_patched", conf, "port", port)
print("nginx_files_patched", changed)
PY

nginx -t || die "nginx_t_failed"
systemctl reload nginx || die "nginx_reload_failed"

# Prove
BODY="$(mktemp)"
CODE="$(curl -sS -o "$BODY" -w '%{http_code}' --max-time 25 -k \
  'https://www.epartscart.com/api/epc_oauth_start.php?provider=google&context=storefront&return_url=%2F' || echo 000)"
printf 'OAUTH_START_CODE=%s\n' "$CODE"
head -c 280 "$BODY"; echo

if grep -qi 'Loading — Please wait\|epc-platform-splash' "$BODY"; then
  rm -f "$BODY"
  die "still_splash — nginx intercept or stale php"
fi

if [[ "$CODE" == "302" ]] || [[ "$CODE" == "301" ]]; then
  printf 'GATE_OK oauth_redirect_to_provider (configured)\n'
  printf 'OAUTH_GOOGLE_CONFIGURED=YES\n'
elif [[ "$CODE" == "422" ]] && grep -qi 'not configured' "$BODY"; then
  printf 'GATE_OK oauth_clear_unconfigured_message (no splash)\n'
  printf 'OAUTH_GOOGLE_CONFIGURED=NO — set Google client_id/secret in Super CP Auth / epc_oauth_config\n'
  printf 'OAUTH_CALLBACK_URI=https://www.ecomae.com/api/epc_oauth_callback.php\n'
else
  printf 'GATE_BAD unexpected code=%s\n' "$CODE"
  rm -f "$BODY"
  die "unexpected_oauth_start_response"
fi
rm -f "$BODY"

printf '======== PASTE_ME_BEGIN ========\n'
printf 'SHA=%s BRANCH=%s\n' "$SHA" "$BRANCH"
printf 'OAUTH_START_CODE=%s\n' "$CODE"
printf '======== PASTE_ME_END ========\n'
printf 'RESULT=PASS FIX_OAUTH_GOOGLE_SPLASH_NOW SHA=%s\n' "$SHA"
exit 0
