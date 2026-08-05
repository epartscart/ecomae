#!/usr/bin/env bash
# Ensure a dedicated nginx vhost serves (www.)epartscart.com.
#
# On this CloudPanel, sites-enabled often has only:
#   default.conf, wildcard-ecomae (*.ecomae.com), www.ecomae.com.conf
# epartscart.com then hitchhikes on default_server — classic-entry cannot be
# installed on wildcard-ecomae because that server_name never matches epartscart.
#
# This script:
#   1) Returns an existing conf whose server_name includes epartscart, OR
#   2) Creates /etc/nginx/sites-enabled/www.epartscart.com.conf by cloning the
#      PHP/ssl skeleton from www.ecomae.com.conf (or ECOMAE_EPARTSCART_CLONE_FROM).
#
#   ECOMAE_CONFIRM_ENSURE_EPARTSCART_VHOST=YES \
#     bash scripts/cloudpanel_ensure_epartscart_nginx_vhost.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DISCOVER_PY="$ROOT/scripts/lib/ecomae_discover_epartscart_nginx_conf.py"
TARGET="${ECOMAE_EPARTSCART_VHOST_PATH:-/etc/nginx/sites-enabled/www.epartscart.com.conf}"

if [[ "${ECOMAE_CONFIRM_ENSURE_EPARTSCART_VHOST:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_ENSURE_EPARTSCART_VHOST=YES\n' >&2
  printf 'Creates/locates a dedicated epartscart.com nginx server block.\n' >&2
  exit 2
fi

if [[ ! -f "$DISCOVER_PY" ]]; then
  printf 'ERROR: missing %s\n' "$DISCOVER_PY" >&2
  exit 1
fi

EXISTING="$(python3 "$DISCOVER_PY" --print-path 2>/dev/null || true)"
if [[ -n "${EXISTING:-}" && -f "$EXISTING" ]]; then
  printf 'OK existing epartscart vhost: %s\n' "$EXISTING"
  grep -nE 'server_name' "$EXISTING" | head -n 10 || true
  printf '%s\n' "$EXISTING"
  exit 0
fi

CLONE_FROM="${ECOMAE_EPARTSCART_CLONE_FROM:-}"
if [[ -z "$CLONE_FROM" ]]; then
  for candidate in \
    /etc/nginx/sites-enabled/www.ecomae.com.conf \
    /etc/nginx/sites-available/www.ecomae.com.conf \
    /etc/nginx/sites-enabled/default.conf \
    /etc/nginx/sites-enabled/default
  do
    if [[ -f "$candidate" ]]; then
      CLONE_FROM="$candidate"
      break
    fi
  done
fi

if [[ -z "${CLONE_FROM:-}" || ! -f "$CLONE_FROM" ]]; then
  printf 'ERROR: no clone source conf found (www.ecomae.com.conf / default)\n' >&2
  ls -1 /etc/nginx/sites-enabled 2>/dev/null >&2 || true
  exit 1
fi

printf 'Creating dedicated epartscart vhost from %s → %s\n' "$CLONE_FROM" "$TARGET"

# Prefer epartscart SSL material when present; else reuse clone source certs.
SSL_CRT=""
SSL_KEY=""
for base in www.epartscart.com epartscart.com; do
  if [[ -f "/etc/nginx/ssl-certificates/${base}.crt" && -f "/etc/nginx/ssl-certificates/${base}.key" ]]; then
    SSL_CRT="/etc/nginx/ssl-certificates/${base}.crt"
    SSL_KEY="/etc/nginx/ssl-certificates/${base}.key"
    break
  fi
done

python3 - "$CLONE_FROM" "$TARGET" "${SSL_CRT}" "${SSL_KEY}" <<'PY'
from pathlib import Path
import re
import sys

src, dest, ssl_crt, ssl_key = sys.argv[1:5]
text = Path(src).read_text(encoding="utf-8", errors="replace")

# Drop classic-entry host-gates / packs that may have been wrongly installed on
# a shared template — we want a clean epartscart server skeleton.
text = re.sub(
    r"(?ms)^[ \t]*location\s*@epc_classic_php_passthrough\s*\{.*?\n[ \t]*\}\n?",
    "",
    text,
)
text = re.sub(
    r"(?ms)^[ \t]*location\s*=\s*(?:/|/[Cc][Pp]/?|/[Ee][Rr][Pp]/?|/[Bb][Oo][Ss]/?|/php-reference/[^\s{]+)\s*\{.*?\n[ \t]*\}\n?",
    "",
    text,
)

def replace_server_names(cfg: str) -> str:
    def _sub(m: re.Match[str]) -> str:
        return m.group(1) + "epartscart.com www.epartscart.com;"
    out, n = re.subn(r"(?im)^(\s*server_name\s+)[^;]+;", _sub, cfg, count=1)
    if n == 0:
        raise SystemExit("ERROR: clone source has no server_name directive")
    # Normalize any remaining server_name lines in same file to avoid dual hosts
    def _sub_rest(m: re.Match[str]) -> str:
        return m.group(1) + "epartscart.com www.epartscart.com;"
    out = re.sub(r"(?im)^(\s*server_name\s+)[^;]+;", _sub_rest, out)
    return out

text = replace_server_names(text)

if ssl_crt and ssl_key:
    text = re.sub(
        r"(?im)^(\s*ssl_certificate\s+)\S+;",
        rf"\1{ssl_crt};",
        text,
    )
    text = re.sub(
        r"(?im)^(\s*ssl_certificate_key\s+)\S+;",
        rf"\1{ssl_key};",
        text,
    )

banner = (
    "# Managed by cloudpanel_ensure_epartscart_nginx_vhost.sh\n"
    "# Dedicated (www.)epartscart.com vhost — do not conflate with wildcard-ecomae (*.ecomae.com).\n"
)
Path(dest).write_text(banner + text, encoding="utf-8")
print(f"WROTE {dest}")
PY

# Soft-link into sites-available for CloudPanel hygiene when writing sites-enabled directly
if [[ -d /etc/nginx/sites-available && ! -e /etc/nginx/sites-available/www.epartscart.com.conf ]]; then
  cp -a "$TARGET" /etc/nginx/sites-available/www.epartscart.com.conf || true
fi

nginx -t
systemctl reload nginx
printf 'OK ensured epartscart vhost: %s\n' "$TARGET"
grep -nE 'server_name|ssl_certificate|root ' "$TARGET" | head -n 30 || true
printf '%s\n' "$TARGET"
exit 0
