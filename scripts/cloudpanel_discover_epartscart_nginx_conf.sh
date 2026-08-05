#!/usr/bin/env bash
# Find the nginx site conf that actually serves epartscart.com (server_name).
# CloudPanel often has NO www.epartscart.com.conf; do NOT assume wildcard-ecomae
# (that vhost is frequently server_name *.ecomae.com only).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DISCOVER_PY="$ROOT/scripts/lib/ecomae_discover_epartscart_nginx_conf.py"

echo "== sites-enabled =="
ls -1 /etc/nginx/sites-enabled 2>/dev/null || true
echo ""
echo "== server_name in sites-enabled =="
grep -RniE 'server_name' /etc/nginx/sites-enabled 2>/dev/null | head -n 80 || true
echo ""
echo "== configs mentioning epartscart =="
grep -RniE 'epartscart' /etc/nginx 2>/dev/null | head -n 80 || true
echo ""
echo "== CloudPanel home nginx confs =="
find /home -path '*/conf/nginx/*' \( -name '*.conf' -o -name '*-*.conf' \) 2>/dev/null | head -n 80 || true
echo ""
echo "== grep epartscart under /home/*/conf =="
grep -RniE 'epartscart' /home/*/conf/nginx 2>/dev/null | head -n 80 || true
echo ""
echo "== clpctl site:list (if available) =="
clpctl site:list 2>/dev/null || true
echo ""
echo "== discover by server_name (authoritative) =="
if [[ -f "$DISCOVER_PY" ]]; then
  python3 "$DISCOVER_PY" --list || true
else
  printf 'ERROR: missing %s\n' "$DISCOVER_PY" >&2
fi
echo ""
echo "If EPARTSCART_VHOST=missing, create one:"
echo "  ECOMAE_CONFIRM_ENSURE_EPARTSCART_VHOST=YES \\"
echo "    bash scripts/cloudpanel_ensure_epartscart_nginx_vhost.sh"
