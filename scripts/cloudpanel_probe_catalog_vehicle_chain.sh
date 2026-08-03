#!/usr/bin/env bash
# Authenticated probe for live catalog vehicle-cache exact-route shadows.
# Reads MFA_ID / MS_ID from JSON rows (PHP uses MFA_ID/MS_ID; ASP.NET may emit either).
# Never removes PHP. Never prints API keys.
#
# Usage (CloudPanel):
#   source /etc/ecomae-aspnet/platform.env
#   bash scripts/cloudpanel_probe_catalog_vehicle_chain.sh
set -euo pipefail

BASE="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
KEY="${ECOMAE_CATALOG_API_KEY:-}"
SECTION="${ECOMAE_CATALOG_SECTION:-passenger}"

if [[ -z "$KEY" ]]; then
  printf 'ERROR: set ECOMAE_CATALOG_API_KEY (source /etc/ecomae-aspnet/platform.env)\n' >&2
  exit 2
fi

pick_id() {
  local file="$1"
  shift
  python3 - "$file" "$@" <<'PY'
import json, sys
path = sys.argv[1]
keys = sys.argv[2:]
doc = json.load(open(path, encoding="utf-8"))
rows = doc.get("data") or []
if not isinstance(rows, list):
    print(0)
    raise SystemExit(0)
for row in rows:
    if not isinstance(row, dict):
        continue
    for key in keys:
        val = row.get(key)
        try:
            n = int(val)
        except (TypeError, ValueError):
            continue
        if n > 0:
            print(n)
            raise SystemExit(0)
print(0)
PY
}

printf '-- manufacturers?section=%s --\n' "$SECTION"
curl -sS -m 30 -H "X-API-Key: ${KEY}" \
  -o /tmp/ecomae-vehicle-mfr.json -w 'HTTP %{http_code}\n' \
  "${BASE}/api/v1/catalog/manufacturers?section=${SECTION}"
MFA_ID="$(pick_id /tmp/ecomae-vehicle-mfr.json MFA_ID mfa_id)"
printf 'MFA_ID=%s (rows=%s)\n' "$MFA_ID" "$(python3 -c 'import json; d=json.load(open("/tmp/ecomae-vehicle-mfr.json")); print(d.get("rows") or len(d.get("data") or []))')"
python3 -m json.tool /tmp/ecomae-vehicle-mfr.json | head -20

if [[ "$MFA_ID" -le 0 ]]; then
  printf 'FAIL: no MFA_ID/mfa_id>0 in manufacturers data (check keys / cache warm).\n' >&2
  python3 -c 'import json; d=json.load(open("/tmp/ecomae-vehicle-mfr.json")); rows=d.get("data") or []; print("sample keys:", sorted((rows[0] or {}).keys()) if rows else None)' >&2
  exit 3
fi

printf '\n-- models?section=%s&mfa_id=%s --\n' "$SECTION" "$MFA_ID"
curl -sS -m 30 -H "X-API-Key: ${KEY}" \
  -o /tmp/ecomae-vehicle-models.json -w 'HTTP %{http_code}\n' \
  "${BASE}/api/v1/catalog/models?section=${SECTION}&mfa_id=${MFA_ID}"
MS_ID="$(pick_id /tmp/ecomae-vehicle-models.json MS_ID ms_id)"
printf 'MS_ID=%s (rows=%s)\n' "$MS_ID" "$(python3 -c 'import json; d=json.load(open("/tmp/ecomae-vehicle-models.json")); print(d.get("rows") or len(d.get("data") or []))')"
python3 -m json.tool /tmp/ecomae-vehicle-models.json | head -30

if [[ "$MS_ID" -le 0 ]]; then
  printf 'WARN: no MS_ID/ms_id>0 in models data — modifications auth 200 may not be probeable yet.\n' >&2
  exit 0
fi

printf '\n-- modifications?section=%s&ms_id=%s --\n' "$SECTION" "$MS_ID"
curl -sS -m 30 -H "X-API-Key: ${KEY}" \
  -o /tmp/ecomae-vehicle-mods.json -w 'HTTP %{http_code}\n' \
  "${BASE}/api/v1/catalog/modifications?section=${SECTION}&ms_id=${MS_ID}"
python3 -m json.tool /tmp/ecomae-vehicle-mods.json | head -30
printf '\nOK: vehicle-chain probe finished (PHP remains).\n'
