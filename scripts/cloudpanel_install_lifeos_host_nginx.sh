#!/usr/bin/env bash
# Install LifeOS host nginx locations so lifeos.ecomae.com / does NOT hit www marketing.
# Usage (root / CloudPanel):
#   ECOMAE_CONFIRM_INSTALL_LIFEOS_HOST_NGINX=YES bash scripts/cloudpanel_install_lifeos_host_nginx.sh
set -euo pipefail

CONFIRM="${ECOMAE_CONFIRM_INSTALL_LIFEOS_HOST_NGINX:-}"
if [[ "$CONFIRM" != "YES" ]]; then
  printf 'Refusing: set ECOMAE_CONFIRM_INSTALL_LIFEOS_HOST_NGINX=YES\n' >&2
  exit 2
fi

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EXAMPLE="$ROOT/deploy/aspnet/nginx-lifeos-host-aspnet-primary-example.conf"
MARKER="X-EcomAE-Route-Cutover lifeos-host-home"
HOST_NEEDLE="lifeos.ecomae.com"

if [[ ! -f "$EXAMPLE" ]]; then
  printf 'ERROR: missing %s\n' "$EXAMPLE" >&2
  exit 1
fi

mapfile -t CONFS < <(grep -RIl --include='*.conf' "$HOST_NEEDLE" /etc/nginx 2>/dev/null || true)
if [[ ${#CONFS[@]} -eq 0 ]]; then
  printf 'WARN: no nginx conf mentions %s — create a server_name block, then re-run\n' "$HOST_NEEDLE" >&2
  printf 'Example locations are in %s\n' "$EXAMPLE"
  exit 0
fi

STAMP="$(date -u +%Y%m%d%H%M%S)"
SNIPPET="/etc/nginx/snippets/ecomae-lifeos-host-aspnet-primary.conf"
mkdir -p /etc/nginx/snippets
cp -f "$EXAMPLE" "$SNIPPET"
printf 'Wrote %s\n' "$SNIPPET"

changed=0
for conf in "${CONFS[@]}"; do
  if grep -Fq "$MARKER" "$conf" 2>/dev/null || grep -Fq "ecomae-lifeos-host-aspnet-primary.conf" "$conf" 2>/dev/null; then
    printf 'OK already wired: %s\n' "$conf"
    continue
  fi
  bak="${conf}.bak.lifeos-host.${STAMP}"
  cp -a "$conf" "$bak"
  # Prefer include near top of first server block that names lifeos.
  if grep -Fq "server_name" "$conf" && grep -Fq "$HOST_NEEDLE" "$conf"; then
    # Insert include after the matching server_name line (first occurrence).
    awk -v host="$HOST_NEEDLE" -v inc="include $SNIPPET;" '
      BEGIN { done=0 }
      {
        print
        if (!done && $0 ~ /server_name/ && index($0, host) > 0) {
          print "    " inc
          done=1
        }
      }
      END { if (!done) exit 3 }
    ' "$conf" >"${conf}.tmp.lifeos" || {
      printf 'WARN: could not auto-insert include into %s — restore bak %s and paste manually\n' "$conf" "$bak" >&2
      rm -f "${conf}.tmp.lifeos"
      continue
    }
    mv "${conf}.tmp.lifeos" "$conf"
    changed=1
    printf 'Patched %s (bak %s)\n' "$conf" "$bak"
  fi
done

if nginx -t 2>/tmp/epc-lifeos-nginx-t.err; then
  systemctl reload nginx
  printf 'PASS: lifeos host nginx installed + reloaded\n'
else
  printf 'ERROR: nginx -t failed after lifeos install\n' >&2
  cat /tmp/epc-lifeos-nginx-t.err >&2 || true
  exit 1
fi

printf 'changed=%s\n' "$changed"
