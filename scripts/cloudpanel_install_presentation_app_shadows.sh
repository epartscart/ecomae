#!/usr/bin/env bash
# Install Blazor presentation-parity preview exact-routes (/cp/app /erp/app /bos/app /storefront/app).
# Never broad /cp|/erp|/bos|/storefront|/. Never removes PHP product chrome.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
EXAMPLE="$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf"

if [[ "${ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES\n' >&2
  exit 2
fi
[[ -f "$CONF" ]] || { printf 'ERROR: missing %s\n' "$CONF" >&2; exit 1; }
[[ -f "$EXAMPLE" ]] || { printf 'ERROR: missing %s\n' "$EXAMPLE" >&2; exit 1; }

bak="/root/$(basename "$CONF").bak.presentation-apps.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"
printf 'Backup: %s\n' "$bak"

python3 - "$CONF" "$EXAMPLE" <<'PY'
from pathlib import Path
import re, sys
conf_path, example_path = Path(sys.argv[1]), Path(sys.argv[2])
text = conf_path.read_text(encoding="utf-8")
example = example_path.read_text(encoding="utf-8")
blocks=[]
for m in re.finditer(r"(?m)^(location = (/[^\s{]+)\s*\{.*?\n\})", example, flags=re.S):
    block_raw, route = m.group(1), m.group(2)
    if route in {"/cp","/erp","/bos","/storefront","/"}:
        raise SystemExit(f"ERROR: refusing broad path {route}")
    if not route.endswith("/app"):
        continue
    indented="\n".join(("  "+line if line.strip() else line) for line in block_raw.splitlines())
    blocks.append((route, indented.rstrip()+"\n"))
if len(blocks)!=4:
    raise SystemExit(f"ERROR: expected 4 presentation app routes, found {len(blocks)}")
inserted=[]; already=[]
for route, block in blocks:
    if re.search(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{", text):
        already.append(route); continue
    m=re.search(r"\n  location / \{", text)
    if not m: raise SystemExit("ERROR: insertion point missing")
    marker=m.start()+1
    text=text[:marker]+block+"\n"+text[marker:]
    inserted.append(route)
conf_path.write_text(text, encoding="utf-8")
print(f"ALREADY PRESENT: {len(already)}")
for r in already: print("  =", r)
print(f"INSERTED: {len(inserted)}")
for r in inserted: print("  +", r)
PY

nginx -t
systemctl reload nginx
printf 'Reloaded nginx. Preview URLs:\n'
printf '  https://www.ecomae.com/cp/app\n'
printf '  https://www.ecomae.com/erp/app\n'
printf '  https://www.ecomae.com/bos/app\n'
printf '  https://www.ecomae.com/storefront/app\n'
printf 'Product chrome /CP/ /ERP/ /BOS/ / remain PHP. Do NOT remove PHP.\n'
printf 'Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
