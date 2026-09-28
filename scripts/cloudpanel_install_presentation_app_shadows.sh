#!/usr/bin/env bash
# Install Blazor presentation-parity preview + login-bridge exact-routes.
# Routes: /cp|erp|bos|storefront/{app,login} and /auth/login/admin.
# Never broad /cp|/erp|/bos|/storefront|/. Never removes PHP product chrome.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
EXAMPLE="$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf"

# shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

if [[ "${ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES\n' >&2
  exit 2
fi
[[ -f "$CONF" ]] || { printf 'ERROR: missing %s\n' "$CONF" >&2; exit 1; }
[[ -f "$EXAMPLE" ]] || { printf 'ERROR: missing %s\n' "$EXAMPLE" >&2; exit 1; }
# Presentation/login shadows: platform www only. Never tenant/industry by default.
ecomae_assert_nginx_shadow_target_allowed "$CONF" presentation

bak="/root/$(basename "$CONF").bak.presentation-apps.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"
printf 'Backup: %s\n' "$bak"

python3 - "$CONF" "$EXAMPLE" <<'PY'
from pathlib import Path
import re, sys

def find_insert_marker(cfg: str) -> int:
    """Match mega-conf / CloudPanel variants (spaces, tabs, location /php, etc.)."""
    for pat in (
        r"\n[ \t]*location / \{",
        r"\n[ \t]*location /\s*\{",
        r"\n[ \t]*location /php",
        r"\n[ \t]*location ~",
        r"\n[ \t]*include[ \t]+fastcgi",
        r"\n[ \t]*include[ \t]+",
    ):
        m = re.search(pat, cfg)
        if m:
            return m.start() + 1
    raise SystemExit(
        "ERROR: insertion point missing "
        "(need `location / {` or fallback location/include inside site conf). "
        "Set ECOMAE_NGINX_SITE_CONF=/etc/nginx/sites-enabled/www.ecomae.com.conf"
    )

conf_path, example_path = Path(sys.argv[1]), Path(sys.argv[2])
text = conf_path.read_text(encoding="utf-8")
example = example_path.read_text(encoding="utf-8")
# Example conf is the allowlist. Accept every exact location except broad product chrome.
broad = {"/cp", "/erp", "/bos", "/storefront", "/"}
blocks=[]
for m in re.finditer(r"(?m)^(location = (/[^\s{]+)\s*\{.*?\n\})", example, flags=re.S):
    block_raw, route = m.group(1), m.group(2)
    if route in broad:
        raise SystemExit(f"ERROR: refusing broad path {route}")
    indented="\n".join(("  "+line if line.strip() else line) for line in block_raw.splitlines())
    blocks.append((route, indented.rstrip()+"\n"))
expected = 411  # Exact ASP.NET page and presentation routes; broad product chrome remains excluded.
if len(blocks) != expected:
    raise SystemExit(f"ERROR: expected {expected} presentation/login routes, found {len(blocks)}")
def server_ranges(cfg: str):
    ranges = []
    for match in re.finditer(r"(?m)^\s*server\s*\{", cfg):
        depth = 0
        end = None
        for index in range(match.end() - 1, len(cfg)):
            char = cfg[index]
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    end = index + 1
                    break
        if end is not None:
            ranges.append((match.start(), end))
    return ranges

def has_top_level_return_301(server: str) -> bool:
    """Ignore redirects nested inside locations; only server-level redirects exclude a block."""
    depth = 0
    top_level = []
    for char in server:
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
        elif depth == 1:
            top_level.append(char)
    return re.search(r"(?m)^\s*return\s+301\s+", "".join(top_level)) is not None

def is_product_server(server: str) -> bool:
    return (
        "root /home/ecomae/htdocs/www.ecomae.com;" in server
        and re.search(r"(?m)^\s*server_name\s+[^;]+;", server) is not None
        and not has_top_level_return_301(server)
    )

inserted=[]; already=[]; target_count=0
for start, end in reversed(server_ranges(text)):
    server = text[start:end]
    if not is_product_server(server):
        continue
    target_count += 1
    for route, block in blocks:
        if re.search(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{", server):
            already.append(route)
            continue
        marker = find_insert_marker(server)
        server = server[:marker] + block + "\n" + server[marker:]
        inserted.append(route)
    text = text[:start] + server + text[end:]
if target_count == 0:
    raise SystemExit("ERROR: no non-redirect product server blocks matched")
conf_path.write_text(text, encoding="utf-8")
print(f"TARGET SERVER BLOCKS: {target_count}")
print(f"ALREADY PRESENT: {len(already)}")
print(f"INSERTED: {len(inserted)}")
PY

nginx -t
systemctl reload nginx
printf 'Reloaded nginx. Preview + login URLs:\n'
printf '  https://www.ecomae.com/cp/app  https://www.ecomae.com/cp/login\n'
printf '  https://www.ecomae.com/erp/app https://www.ecomae.com/erp/login\n'
printf '  https://www.ecomae.com/bos/app https://www.ecomae.com/bos/login\n'
printf '  https://www.ecomae.com/storefront/app https://www.ecomae.com/storefront/login https://www.ecomae.com/marketing/app https://www.ecomae.com/storefront/checkout-app\n'
printf '  POST https://www.ecomae.com/auth/login/admin\n'
printf 'Product chrome /CP/ /ERP/ /BOS/ / remain PHP. Do NOT remove PHP.\n'
printf 'Set EcomAE__SecretSuccession (PHP secret_succession) in platform.env for login bridge writes.\n'
printf 'Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
