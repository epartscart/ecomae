#!/usr/bin/env bash
# Help operator fill /etc/ecomae-aspnet/platform.env for final-gate smoke.
# Never prints full API keys or cookie values. Never removes PHP.
set -euo pipefail

ECOMAE_ASPNET_ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
ENV_FILE="${ECOMAE_ASPNET_ENV_DIR}/platform.env"

printf '%s\n' '== Prepare final-gate smoke secrets =='
printf 'Env file: %s\n' "$ENV_FILE"
printf 'This script does NOT invent keys. Plaintext API keys exist only at issuance time.\n'

if [[ ! -f "$ENV_FILE" ]]; then
  printf 'ERROR: %s missing. Run deploy first.\n' "$ENV_FILE" >&2
  exit 2
fi

bash "$(cd "$(dirname "$0")" && pwd)/cloudpanel_validate_final_gate_env.sh" || true

printf '\n-- How to get API keys --\n'
printf '1) Super CP → API clients (epc_api_clients). Create/reveal a price_pro + catalog key.\n'
printf '2) Keys must start with epc_pricepro_ and epc_catalog_ (full secret, not just prefix).\n'
printf '3) Put them in %s as:\n' "$ENV_FILE"
printf '     ECOMAE_PRICE_LOOKUP_API_KEY=epc_pricepro_...\n'
printf '     ECOMAE_CATALOG_API_KEY=epc_catalog_...\n'
printf 'DB only stores hashes — you cannot recover a lost plaintext key from MySQL.\n'

printf '\n-- How to get admin cookie --\n'
printf '1) Browser: log into https://www.ecomae.com/CP/ (or https://cp.ecomae.com/CP/) as Super CP admin.\n'
printf '2) DevTools → Network → any /CP/ request → Request Headers → Cookie.\n'
printf '3) Copy BOTH cookies into one line (no surrounding quotes in the env value):\n'
printf '     ECOMAE_ADMIN_COOKIE_HEADER=admin_session=PASTE; admin_u_id=123\n'
printf '4) admin_u_id must be digits. Do not paste only PHPSESSID or Cloudflare cookies.\n'
printf '5) Test (must show kind=2 or Kind=Admin, isAuthenticated=true):\n'
printf '     source %s\n' "$ENV_FILE"
printf '     curl -sS -H "Cookie: \$ECOMAE_ADMIN_COOKIE_HEADER" http://127.0.0.1:5100/auth/session/probe; echo\n'

# Optional: list API client prefixes from DB if mysql client + connection string available (no secrets).
if [[ -f "$ENV_FILE" ]]; then
  # shellcheck disable=SC1090
  set -a; source "$ENV_FILE"; set +a
fi
conn="${ConnectionStrings__TenantRegistry:-}"
if [[ -n "$conn" && "$conn" != *"<db_"* ]] && command -v mysql >/dev/null 2>&1; then
  printf '\n-- Active API client prefixes from platform DB (metadata only) --\n'
  python3 - "$conn" <<'PY' || printf 'WARN: could not query epc_api_clients (skip)\n'
import re, subprocess, sys
conn = sys.argv[1]
def pick(name, default=""):
    m = re.search(rf"(?:^|;)\s*{name}=([^;]+)", conn, re.I)
    return m.group(1) if m else default
host = pick("Server", "127.0.0.1")
db = pick("Database")
user = pick("User")
password = pick("Password")
if not (db and user):
    raise SystemExit(0)
sql = (
    "SELECT id, IFNULL(client_key_prefix,''), IFNULL(product,''), IFNULL(label,''), active "
    "FROM epc_api_clients ORDER BY id DESC LIMIT 20;"
)
cmd = ["mysql", "-N", "-h", host, "-u", user, f"-p{password}", db, "-e", sql]
try:
    out = subprocess.check_output(cmd, stderr=subprocess.DEVNULL, text=True)
except Exception:
    raise SystemExit(0)
print("id\tprefix\tproduct\tlabel\tactive")
print(out.strip() or "(no rows)")
print("Use a plaintext key matching one of these prefixes (issued when the client was created).")
PY
fi

printf '\nEdit now:\n  nano %s\n' "$ENV_FILE"
printf 'Then:\n  source %s\n' "$ENV_FILE"
printf '  bash scripts/cloudpanel_validate_final_gate_env.sh\n'
printf '  bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'
printf 'Do NOT remove PHP.\n'
