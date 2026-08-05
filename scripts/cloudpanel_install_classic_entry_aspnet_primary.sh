#!/usr/bin/env bash
# Install exact-route classic PHP entry → ASP.NET primary redirects on www.
# Keeps PHP as reference (/index.php + deep /cp|/erp|/bos module paths).
# NEVER broad location trees. NEVER deletes PHP.
#
# Usage:
#   ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
#     bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
EXAMPLE="$ROOT/deploy/aspnet/nginx-classic-entry-aspnet-primary-shadow-example.conf"

# shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

if [[ "${ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES\n' >&2
  printf 'This installs exact-route redirects only (classic entries → ASP.NET apps).\n' >&2
  printf 'PHP remains as reference via /index.php and deep module paths.\n' >&2
  exit 2
fi
if [[ ! -f "$CONF" ]]; then
  printf 'ERROR: missing nginx site conf %s\n' "$CONF" >&2
  exit 1
fi
if [[ ! -f "$EXAMPLE" ]]; then
  printf 'ERROR: missing example %s\n' "$EXAMPLE" >&2
  exit 1
fi
# www only — never tenant/industry vhosts by default.
ecomae_assert_nginx_shadow_target_allowed "$CONF" exact-route

bak="/root/$(basename "$CONF").bak.classic-entry-aspnet.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"
printf 'Backup: %s\n' "$bak"

python3 - "$CONF" "$EXAMPLE" <<'PY'
from pathlib import Path
import re, sys

conf_path, example_path = Path(sys.argv[1]), Path(sys.argv[2])
text = conf_path.read_text(encoding="utf-8")
example = example_path.read_text(encoding="utf-8")

blocks = []
# Allow exact root "/" and paths like "/cp/" ("/" + zero-or-more non-space/brace).
for m in re.finditer(r"(?m)^(location = (/[^\s{]*)\s*\{.*?\n\})", example, flags=re.S):
    block_raw, route = m.group(1), m.group(2)
    if route == "":
        raise SystemExit("ERROR: empty route")
    if not re.search(r"(?m)^\s*return\s+302\s+", block_raw):
        raise SystemExit(f"ERROR: expected return 302 in {route}")
    if re.search(r"(?m)^\s*proxy_pass\s+", block_raw):
        raise SystemExit(f"ERROR: classic-entry pack must be return-302 only ({route})")
    # Refuse accidental broad prefix forms in the example allowlist.
    if route in {"/api", "/storefront"}:
        raise SystemExit(f"ERROR: refusing broad path {route}")
    indented = "\n".join(("  " + line if line.strip() else line) for line in block_raw.splitlines())
    blocks.append((route, indented.rstrip() + "\n"))

expected = 48  # / + 4 CP + 4 ERP + 4 BOS + 35 top-level marketing (bos slug excluded)
if len(blocks) != expected:
    raise SystemExit(f"ERROR: expected {expected} classic-entry redirects, found {len(blocks)}")

inserted, replaced = [], []
for route, block in blocks:
    pattern = re.compile(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{.*?\n[ \t]*\}}\n?", re.S)
    if pattern.search(text):
        text = pattern.sub(block + "\n", text, count=1)
        replaced.append(route)
        continue
    m = re.search(r"\n  location / \{", text)
    if not m:
        raise SystemExit("ERROR: insertion point missing (location / {)")
    marker = m.start() + 1
    text = text[:marker] + block + "\n" + text[marker:]
    inserted.append(route)

conf_path.write_text(text, encoding="utf-8")
print(f"REPLACED: {len(replaced)}")
for r in replaced:
    print("  ~", r)
print(f"INSERTED: {len(inserted)}")
for r in inserted:
    print("  +", r)
PY

nginx -t
systemctl reload nginx

printf '\nReloaded nginx. Classic entries now redirect to ASP.NET apps:\n'
printf '  / → /marketing/app\n'
printf '  /cp/ /CP/ → /cp/app\n'
printf '  /erp/ /ERP/ → /erp/app\n'
printf '  /bos/ /BOS/ → /bos/app\n'
printf '  /solutions /privacy /platform … → /marketing/<slug>\n'
printf '\nPHP reference kept:\n'
printf '  https://www.ecomae.com/index.php  (marketing home)\n'
printf '  deep /cp|/erp|/bos module paths (not exact entry)\n'
printf '  /migration/compare\n'
printf 'Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
