#!/usr/bin/env bash
# Wait until ASP.NET loopback /health returns HTTP 200.
# Used after systemd restart and before final-gate capture/smoke.
set -euo pipefail

ASPNET_BASE="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"
TIMEOUT_SEC="${ECOMAE_HEALTH_WAIT_SECONDS:-60}"
INTERVAL_SEC="${ECOMAE_HEALTH_POLL_SECONDS:-2}"

printf 'Waiting for %s/health (timeout %ss)...\n' "$ASPNET_BASE" "$TIMEOUT_SEC"
deadline=$((SECONDS + TIMEOUT_SEC))
while (( SECONDS < deadline )); do
  code="$(curl -sS -m 5 -o /tmp/ecomae-health-wait.txt -w '%{http_code}' "$ASPNET_BASE/health" 2>/dev/null || true)"
  if [[ "$code" == "200" ]]; then
    printf 'OK   %s/health HTTP 200 after %ss\n' "$ASPNET_BASE" "$SECONDS"
    exit 0
  fi
  sleep "$INTERVAL_SEC"
done

printf 'ERROR %s/health not ready within %ss (last HTTP %s)\n' "$ASPNET_BASE" "$TIMEOUT_SEC" "${code:-000}" >&2
printf 'Check: systemctl status ecomae-platform.service --no-pager\n' >&2
printf 'Logs:  journalctl -u ecomae-platform.service -n 80 --no-pager\n' >&2
exit 1
