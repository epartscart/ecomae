#!/usr/bin/env bash
# Fix ePartsCart top part-number search: strip nginx search-app → /en/shop/part_search
# redirect (loops under PreferAspNet / PHP-paused) and proxy search-app to Kestrel :5100.
# Also ensures /storefront/ tree and platform-assets reach ASP.NET.
#
# CloudPanel root SSH. Does not flip cutover locks. Does not delete PHP.
set -euo pipefail

SNIP_BEGIN='# BEGIN ecomae-storefront-search-app-redirect'
SNIP_END='# END ecomae-storefront-search-app-redirect'

PROXY_BLOCK=$(cat <<'NGX'
# BEGIN ecomae-storefront-search-app-redirect
location = /storefront/search-app {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header Cookie $http_cookie;
    proxy_set_header X-EcomAE-Route-Cutover storefront-search-app-warehouse-results;
}
location ^~ /platform-assets/ {
  proxy_pass http://127.0.0.1:5100;
  proxy_http_version 1.1;
  proxy_set_header Host $host;
  proxy_set_header X-Forwarded-Proto $scheme;
  proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
}
# END ecomae-storefront-search-app-redirect
NGX
)

python3 - <<'PY'
import re, shutil, time
from pathlib import Path

proxy = '''# BEGIN ecomae-storefront-search-app-redirect
location = /storefront/search-app {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header Cookie $http_cookie;
    proxy_set_header X-EcomAE-Route-Cutover storefront-search-app-warehouse-results;
}
location ^~ /platform-assets/ {
  proxy_pass http://127.0.0.1:5100;
  proxy_http_version 1.1;
  proxy_set_header Host $host;
  proxy_set_header X-Forwarded-Proto $scheme;
  proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
}
# END ecomae-storefront-search-app-redirect
'''

marker = re.compile(
    r"[ \t]*# BEGIN ecomae-storefront-search-app-redirect.*?# END ecomae-storefront-search-app-redirect\n?",
    re.S,
)
legacy_exact = re.compile(
    r"(?m)^[ \t]*location\s*=\s*/storefront/search-app\s*\{.*?\n[ \t]*\}\n?",
    re.S,
)

roots = [
    Path("/etc/nginx/sites-enabled"),
    Path("/etc/nginx/sites-available"),
    Path("/etc/nginx/conf.d"),
]
changed = 0
for root in roots:
    if not root.is_dir():
        continue
    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        low = text.lower()
        if "epartscart" not in low and "electronicae" not in low:
            continue
        if "storefront/search-app" not in text and "ecomae-storefront-search-app-redirect" not in text:
            # still inject if /storefront/ tree exists
            if "location ^~ /storefront/" not in text and "location ^~ /storefront {" not in text:
                continue
        original = text
        text = marker.sub("", text)
        # Remove legacy exact search-app blocks that 302 to part_search
        def drop_if_redirect(m: re.Match[str]) -> str:
            body = m.group(0)
            if "part_search" in body or "return 302" in body:
                return ""
            if "proxy_pass http://127.0.0.1:5100" in body:
                return body
            return ""
        text = legacy_exact.sub(drop_if_redirect, text)
        if "# BEGIN ecomae-storefront-search-app-redirect" not in text:
            # Insert before first /storefront/ tree or at end of first server block
            insert_at = text.find("location ^~ /storefront/")
            if insert_at < 0:
                insert_at = text.find("location ^~ /storefront {")
            if insert_at >= 0:
                text = text[:insert_at] + proxy + "\n" + text[insert_at:]
            else:
                text = text.rstrip() + "\n" + proxy + "\n"
        if text != original:
            bak = path.with_suffix(path.suffix + f".bak-search-fix-{int(time.time())}")
            shutil.copy2(path, bak)
            path.write_text(text, encoding="utf-8")
            changed += 1
            print(f"updated {path} (bak {bak.name})")

print(f"files_changed={changed}")
PY

nginx -t
systemctl reload nginx || service nginx reload

printf '\n== Prove search-app (must be 200, not part_search 302) ==\n'
for url in \
  'http://127.0.0.1:5100/storefront/search-app?article=0986424590' \
  'https://www.epartscart.com/storefront/search-app?article=0986424590'
do
  HDR="$(curl -skS -D - -o /tmp/epc_search_prove.html -A 'Mozilla/5.0' --max-time 45 "$url" || true)"
  CODE="$(printf '%s' "$HDR" | head -1 | tr -d '\r')"
  LOC="$(printf '%s' "$HDR" | awk 'tolower($1)=="location:"{print $2; exit}' | tr -d '\r')"
  printf '%s → %s location=%s bytes=%s\n' "$url" "$CODE" "${LOC:-<none>}" "$(wc -c </tmp/epc_search_prove.html 2>/dev/null || echo 0)"
  if [[ "${LOC:-}" == *part_search* ]]; then
    printf 'FAIL still redirecting to part_search\n' >&2
    exit 2
  fi
done

if rg -q 'Select manufacturer|Warehouse offers|epc-sf-brand|epcSfOfferBody' /tmp/epc_search_prove.html; then
  printf 'PASS search-app HTML contains brand picker / warehouse shell\n'
else
  printf 'WARN search-app HTML missing expected markers (check Kestrel publish)\n' >&2
fi

printf 'OK search-app warehouse edge fix applied\n'
