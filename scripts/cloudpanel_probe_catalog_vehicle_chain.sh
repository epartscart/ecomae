#!/usr/bin/env bash
# Authenticated probe for live catalog vehicle-cache exact-route shadows.
# Walks manufacturer MFA_ID values until models cache returns rows (first MFA
# is often empty). Reads MFA_ID/MS_ID keys (PHP uppercase or lowercase).
# Never removes PHP. Never prints API keys.
#
# Usage (CloudPanel):
#   source /etc/ecomae-aspnet/platform.env
#   bash scripts/cloudpanel_probe_catalog_vehicle_chain.sh
#
# Optional:
#   ECOMAE_VEHICLE_CHAIN_MAX_MFA=40   # max manufacturers to try (default 40)
set -euo pipefail

BASE="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
KEY="${ECOMAE_CATALOG_API_KEY:-}"
SECTION="${ECOMAE_CATALOG_SECTION:-passenger}"
MAX_MFA="${ECOMAE_VEHICLE_CHAIN_MAX_MFA:-40}"

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

list_mfa_ids() {
  python3 - "$1" "$MAX_MFA" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
limit = int(sys.argv[2])
rows = doc.get("data") or []
ids = []
seen = set()
# Prefer popular manufacturers when the flag is present.
def popular_rank(row):
    for k in ("popular", "is_popular", "POPULAR"):
        v = row.get(k)
        if v in (1, True, "1", "true", "True"):
            return 0
    return 1

ordered = sorted([r for r in rows if isinstance(r, dict)], key=popular_rank)
for row in ordered:
    for key in ("MFA_ID", "mfa_id"):
        try:
            n = int(row.get(key) or 0)
        except (TypeError, ValueError):
            continue
        if n > 0 and n not in seen:
            seen.add(n)
            ids.append(n)
            break
    if len(ids) >= limit:
        break
print(" ".join(str(i) for i in ids))
PY
}

printf '-- manufacturers?section=%s --\n' "$SECTION"
curl -sS -m 30 -H "X-API-Key: ${KEY}" \
  -o /tmp/ecomae-vehicle-mfr.json -w 'HTTP %{http_code}\n' \
  "${BASE}/api/v1/catalog/manufacturers?section=${SECTION}"
MFR_ROWS="$(python3 -c 'import json; d=json.load(open("/tmp/ecomae-vehicle-mfr.json")); print(d.get("rows") or len(d.get("data") or []))')"
printf 'manufacturers rows=%s\n' "$MFR_ROWS"
python3 -m json.tool /tmp/ecomae-vehicle-mfr.json | head -16

MFA_LIST="$(list_mfa_ids /tmp/ecomae-vehicle-mfr.json)"
if [[ -z "$MFA_LIST" ]]; then
  printf 'FAIL: no MFA_ID/mfa_id>0 in manufacturers data (check keys / cache warm).\n' >&2
  python3 -c 'import json; d=json.load(open("/tmp/ecomae-vehicle-mfr.json")); rows=d.get("data") or []; print("sample keys:", sorted((rows[0] or {}).keys()) if rows else None)' >&2
  exit 3
fi

MFA_ID=0
MS_ID=0
TRIED=0
: > /tmp/ecomae-vehicle-models.json
for candidate in $MFA_LIST; do
  TRIED=$((TRIED + 1))
  printf '\n-- models?section=%s&mfa_id=%s (try %s) --\n' "$SECTION" "$candidate" "$TRIED"
  code="$(curl -sS -m 30 -H "X-API-Key: ${KEY}" \
    -o /tmp/ecomae-vehicle-models.json -w '%{http_code}' \
    "${BASE}/api/v1/catalog/models?section=${SECTION}&mfa_id=${candidate}" || echo 000)"
  rows="$(python3 -c 'import json; d=json.load(open("/tmp/ecomae-vehicle-models.json")); print(int(d.get("rows") or len(d.get("data") or [])))')"
  printf 'HTTP %s rows=%s\n' "$code" "$rows"
  if [[ "$code" == "200" && "$rows" -gt 0 ]]; then
    MFA_ID="$candidate"
    MS_ID="$(pick_id /tmp/ecomae-vehicle-models.json MS_ID ms_id)"
    break
  fi
done

printf 'chosen MFA_ID=%s MS_ID=%s (tried %s manufacturers)\n' "$MFA_ID" "$MS_ID" "$TRIED"
if [[ "$MFA_ID" -gt 0 ]]; then
  python3 -m json.tool /tmp/ecomae-vehicle-models.json | head -30
else
  printf 'WARN: no non-empty models cache among first %s MFA_IDs.\n' "$MAX_MFA" >&2
  printf 'Hint (CloudPanel MySQL on TenantRegistry DB):\n' >&2
  printf "  SELECT mfa_id, COUNT(*) c FROM epc_umapi_models WHERE section='%s' GROUP BY mfa_id ORDER BY c DESC LIMIT 10;\n" "$SECTION" >&2
  printf 'Then: curl .../models?section=%s&mfa_id=<id>\n' "$SECTION" >&2
  exit 4
fi

if [[ "$MS_ID" -le 0 ]]; then
  printf 'WARN: models returned rows but no MS_ID/ms_id>0 — cannot probe modifications.\n' >&2
  exit 0
fi

printf '\n-- modifications?section=%s&ms_id=%s --\n' "$SECTION" "$MS_ID"
curl -sS -m 30 -H "X-API-Key: ${KEY}" \
  -o /tmp/ecomae-vehicle-mods.json -w 'HTTP %{http_code}\n' \
  "${BASE}/api/v1/catalog/modifications?section=${SECTION}&ms_id=${MS_ID}"
python3 -m json.tool /tmp/ecomae-vehicle-mods.json | head -30
printf '\nOK: vehicle-chain probe finished (PHP remains).\n'
