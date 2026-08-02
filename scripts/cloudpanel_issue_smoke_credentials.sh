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
printf 'Note: does not CREATE tables (ecomae_aspnet often lacks CREATE). Uses existing epc_api_clients.\n'

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
  printf '\nIf INSERT/UPDATE was denied for ecomae_aspnet (issuer never CREATE TABLEs), either:\n' >&2
  printf '  A) Point at PHP app DB user (has write):\n' >&2
  printf '       export ECOMAE_PHP_DOCROOT=/path/to/site/htdocs   # must contain config.php\n' >&2
  printf '       ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh\n' >&2
  printf '  B) Or export ECOMAE_SMOKE_DB_HOST/NAME/USER/PASSWORD and re-run.\n' >&2
  printf '  C) Or GRANT INSERT,UPDATE ON <db>.epc_api_clients TO '\''ecomae_aspnet'\''@'\''localhost'\'';\n' >&2
  printf 'If the table is missing, create it once as MySQL root (DDL SQL is printed by the PHP issuer).\n' >&2
  exit "$rc"
fi

# shellcheck disable=SC1090
set -a; source "$ENV_FILE"; set +a
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
