#!/usr/bin/env bash
# Issue/rotate final-gate smoke API keys into /etc/ecomae-aspnet/platform.env and
# bind an active Super CP admin session cookie when one exists in MySQL.
# Never prints plaintext secrets. Never removes PHP.
#
# Usage (as root on CloudPanel):
#   ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh
#
# If admin cookie is still missing: log into https://www.ecomae.com/CP/ once, then re-run.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
PHP_BIN="${ECOMAE_PHP_BIN:-php}"

printf '%s\n' '== Issue final-gate smoke credentials =='
printf 'Env file: %s\n' "$ENV_FILE"
printf 'Note: prefers ConnectionStrings__TenantRegistry DB (same as ASP.NET), then PHP→TenantRegistry. CREATE only with ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES.\n'
printf 'Refuses PHP db≠TenantRegistry mismatch (keys would be invisible to ASP.NET). Escape: ECOMAE_SMOKE_DB_* or ECOMAE_SMOKE_ALLOW_PHP_DB_MISMATCH=YES.\n'
printf 'If PHP sessions DB differs: set ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES to copy admin session into TenantRegistry.\n'
printf 'If CREATE denied: bash scripts/cloudpanel_print_epc_api_clients_ddl.sh (paste as MySQL admin).\n'

if [[ "${ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS:-}" != "YES" ]]; then
  printf 'Refusing without confirmation.\n' >&2
  printf 'Run:\n' >&2
  printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh\n' >&2
  exit 2
fi

if ! command -v "$PHP_BIN" >/dev/null 2>&1; then
  printf 'ERROR: php CLI not found\n' >&2
  exit 2
fi

# Prefer PHP site docroot for app DB credentials when ecomae_aspnet cannot INSERT.
if [[ -z "${ECOMAE_PHP_DOCROOT:-}" ]]; then
  for candidate in \
    /home/ecomae/htdocs/www.ecomae.com \
    /home/ecomae/htdocs \
    /home/cloudpanel/htdocs/www.ecomae.com \
    /home/www/htdocs \
    /var/www/www.ecomae.com \
    /var/www/ecomae \
    /var/www/html
  do
    if [[ -f "$candidate/config.php" ]]; then
      export ECOMAE_PHP_DOCROOT="$candidate"
      printf 'Using PHP docroot: %s\n' "$ECOMAE_PHP_DOCROOT"
      break
    fi
  done
fi

export ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES
export ECOMAE_ASPNET_ENV_FILE="$ENV_FILE"
set +e
"$PHP_BIN" "$ROOT/scripts/php/issue_final_gate_smoke_credentials.php"
rc=$?
set -e

if [[ "$rc" -eq 3 ]]; then
  printf '\nAPI keys written. Admin session still missing.\n'
  printf '1) Open https://www.ecomae.com/CP/ and log in as Super CP admin.\n'
  printf '2) Re-run:\n'
  printf '   ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
  exit 3
fi
if [[ "$rc" -ne 0 ]]; then
  printf '\nIf the table is missing in TenantRegistry DB (e.g. asap.epc_api_clients):\n' >&2
  printf '  ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n' >&2
  printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh\n' >&2
  printf 'Or combine create+issue:\n' >&2
  printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES \\\n' >&2
  printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n' >&2
  printf 'If PHP user cannot reach TenantRegistry db, export ECOMAE_SMOKE_DB_* or GRANT to ecomae_aspnet.\n' >&2
  exit "$rc"
fi

# shellcheck disable=SC1090
set -a; source "$ENV_FILE"; set +a
# shellcheck disable=SC1091
source "$ROOT/scripts/cloudpanel_repair_smoke_cookie_env.sh"
bash "$ROOT/scripts/cloudpanel_validate_final_gate_env.sh"

printf '\n-- Probe admin session (loopback) --\n'
bash "$ROOT/scripts/wait_for_aspnet_health.sh" || true
probe_tmp="$(mktemp)"
code="$(curl -sS -m 20 -o "$probe_tmp" -w '%{http_code}' \
  -H "Cookie: ${ECOMAE_ADMIN_COOKIE_HEADER:-}" \
  http://127.0.0.1:5100/auth/session/probe || true)"
printf 'HTTP %s\n' "$code"
python3 - "$probe_tmp" <<'PY' || true
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
kind = doc.get("Kind") if doc.get("Kind") is not None else doc.get("kind")
auth = doc.get("IsAuthenticated")
if auth is None:
    auth = doc.get("isAuthenticated")
print(f"kind={kind!r} isAuthenticated={auth!r} userId={doc.get('userId') or doc.get('UserId')!r}")
is_admin = kind in ("Admin", 2) or str(kind) == "2"
if not is_admin or auth is False:
    raise SystemExit("Admin probe failed — log into Super CP and re-run issue script")
print("Admin session OK")
PY
rm -f "$probe_tmp"

printf '\nNext:\n'
printf '  source %s\n' "$ENV_FILE"
printf '  bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'
printf '  bash scripts/cloudpanel_commit_final_gate_smoke.sh\n'
printf 'Do NOT remove PHP.\n'
