#!/usr/bin/env bash
# Install only /marketing/* exact-route presentation shadows on www.ecomae.com.
# Wrapper around presentation installer allowlist — never broad location /.
# Live / must remain PHP epm-hub until dual-sample + RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
EXAMPLE="$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf"

# shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

if [[ "${ECOMAE_CONFIRM_INSTALL_MARKETING_APP_SHADOWS:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_MARKETING_APP_SHADOWS=YES\n' >&2
  printf 'After install: bash scripts/cloudpanel_probe_marketing_app_shadows.sh\n' >&2
  printf 'Live / must stay PHP epm-hub (cloudpanel_probe_ecomae_marketing_php_chrome.sh).\n' >&2
  exit 2
fi
[[ -f "$CONF" ]] || { printf 'ERROR: missing %s\n' "$CONF" >&2; exit 1; }
[[ -f "$EXAMPLE" ]] || { printf 'ERROR: missing %s\n' "$EXAMPLE" >&2; exit 1; }
ecomae_assert_nginx_shadow_target_allowed "$CONF" presentation

bak="/root/$(basename "$CONF").bak.marketing-apps.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"
printf 'Backup: %s\n' "$bak"

python3 - "$CONF" "$EXAMPLE" <<'PY'
from pathlib import Path
import re, sys
conf_path, example_path = Path(sys.argv[1]), Path(sys.argv[2])
text = conf_path.read_text(encoding="utf-8")
example = example_path.read_text(encoding="utf-8")
blocks = []
for m in re.finditer(r"(?m)^(location = (/marketing/[^\s{]+)\s*\{.*?\n\})", example, flags=re.S):
    block_raw, route = m.group(1), m.group(2)
    indented = "\n".join(("  " + line if line.strip() else line) for line in block_raw.splitlines())
    blocks.append((route, indented.rstrip() + "\n"))
if len(blocks) < 30:
    raise SystemExit(f"ERROR: expected >=30 /marketing/* routes, found {len(blocks)}")
inserted, already = [], []
for route, block in blocks:
    if re.search(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{", text):
        already.append(route)
        continue
    m = re.search(r"\n  location / \{", text)
    if not m:
        raise SystemExit("ERROR: insertion point missing")
    marker = m.start() + 1
    text = text[:marker] + block + "\n" + text[marker:]
    inserted.append(route)
conf_path.write_text(text, encoding="utf-8")
print(f"MARKETING ROUTES: {len(blocks)}")
print(f"ALREADY PRESENT: {len(already)}")
print(f"INSERTED: {len(inserted)}")
for r in inserted:
    print("  +", r)
PY

nginx -t
systemctl reload nginx
printf 'Reloaded nginx. Probe:\n'
printf '  bash scripts/cloudpanel_probe_marketing_app_shadows.sh\n'
printf '  bash scripts/cloudpanel_probe_ecomae_marketing_php_chrome.sh  # / must stay PHP epm-hub\n'
printf 'Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
printf 'PHP was NOT removed. cutoverAllowed stays false.\n'
