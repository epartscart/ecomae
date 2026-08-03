#!/usr/bin/env bash
# Authenticated probe for live catalog vehicle-cache exact-route shadows.
# Prefers warm MFA_IDs from epc_umapi_models, then walks manufacturer list.
# Never removes PHP. Never prints API keys.
#
# Usage (CloudPanel):
#   set -a; source /etc/ecomae-aspnet/platform.env; set +a
#   bash scripts/cloudpanel_probe_catalog_vehicle_chain.sh
#
# Optional:
#   ECOMAE_VEHICLE_CHAIN_MAX_MFA=40
#   ECOMAE_PUBLIC_BASE_URL=https://www.ecomae.com
#   ECOMAE_ASPNET_LOOPBACK=http://127.0.0.1:5100
set -euo pipefail

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
if [[ -z "${ECOMAE_CATALOG_API_KEY:-}" && -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

BASE="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
LOOP="${ECOMAE_ASPNET_LOOPBACK:-http://127.0.0.1:5100}"
KEY="${ECOMAE_CATALOG_API_KEY:-${CATALOG_API_KEY:-}}"
SECTION="${ECOMAE_CATALOG_SECTION:-passenger}"
MAX_MFA="${ECOMAE_VEHICLE_CHAIN_MAX_MFA:-40}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UA='Mozilla/5.0 (compatible; EcomAE-VehicleChainProbe/1.0)'

if [[ -z "$KEY" ]]; then
  printf 'ERROR: ECOMAE_CATALOG_API_KEY missing. Use: set -a; source %s; set +a\n' "$ENV_FILE" >&2
  exit 2
fi

# Avoid `printf --stuff` (GNU printf treats leading -- as options).
say() { printf '%s\n' "$*"; }

curl_json() {
  # Usage: curl_json OUTFILE PATH_WITH_QUERY
  # Tries public URL first, then loopback :5100 (bypasses Cloudflare 403).
  local out="$1"
  local path="$2"
  local code
  code="$(curl -sS -m 30 -A "$UA" -H "X-API-Key: ${KEY}" \
    -o "$out" -w '%{http_code}' \
    "${BASE}${path}" 2>/dev/null || echo 000)"
  if [[ "$code" == "200" || "$code" == "401" || "$code" == "400" ]]; then
    printf '%s' "$code"
    return 0
  fi
  code="$(curl -sS -m 30 -A "$UA" -H "X-API-Key: ${KEY}" \
    -o "$out" -w '%{http_code}' \
    "${LOOP}${path}" 2>/dev/null || echo 000)"
  printf '%s' "$code"
}

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

list_mfa_from_manufacturers() {
  python3 - "$1" "$MAX_MFA" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
limit = int(sys.argv[2])
rows = doc.get("data") or []
ids, seen = [], set()

def popular_rank(row):
    for k in ("popular", "is_popular", "POPULAR"):
        v = row.get(k)
        if v in (1, True, "1", "true", "True"):
            return 0
    return 1

for row in sorted([r for r in rows if isinstance(r, dict)], key=popular_rank):
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

parse_id_counts() {
  python3 -c '
import re,sys
ids=[]
for line in sys.stdin:
    line=line.strip()
    m=re.match(r"^(\d+)\s+(\d+)$", line)
    if not m:
        m=re.search(r"\|\s*(\d+)\s*\|\s*(\d+)\s*\|", line)
    if m:
        ids.append(m.group(1))
print(" ".join(ids))
'
}

WARM_HELPER="$ROOT/scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh"
[[ -x "$WARM_HELPER" ]] || WARM_HELPER="$ROOT/scripts/cloudpanel_list_warm_catalog_models_mfa.sh"

# Prefer DB-warm MFA_IDs (from epc_umapi_models) when helper is present.
WARM_MFA=""
if [[ -x "$WARM_HELPER" ]]; then
  say "== warm MFA_IDs from epc_umapi_models =="
  if warm_out="$(bash "$WARM_HELPER" models 2>/dev/null || bash "$WARM_HELPER" 2>/dev/null)"; then
    printf '%s\n' "$warm_out"
    WARM_MFA="$(printf '%s\n' "$warm_out" | parse_id_counts)"
  else
    say "WARN: warm MFA list helper failed (continuing with manufacturers walk)"
  fi
fi

WARM_MS=""
if [[ -x "$ROOT/scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh" ]]; then
  say "== warm MS_IDs from epc_umapi_modifications =="
  if warm_ms_out="$(bash "$ROOT/scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh" modifications 2>/dev/null)"; then
    printf '%s\n' "$warm_ms_out"
    WARM_MS="$(printf '%s\n' "$warm_ms_out" | parse_id_counts)"
  fi
fi

say "== manufacturers?section=${SECTION} =="
mfr_code="$(curl_json /tmp/ecomae-vehicle-mfr.json "/api/v1/catalog/manufacturers?section=${SECTION}")"
say "HTTP ${mfr_code}"
MFR_ROWS="$(python3 -c 'import json; d=json.load(open("/tmp/ecomae-vehicle-mfr.json")); print(d.get("rows") or len(d.get("data") or []))' 2>/dev/null || echo 0)"
say "manufacturers rows=${MFR_ROWS}"
python3 -m json.tool /tmp/ecomae-vehicle-mfr.json 2>/dev/null | head -16 || true

MFA_LIST="${WARM_MFA}$(list_mfa_from_manufacturers /tmp/ecomae-vehicle-mfr.json 2>/dev/null || true)"
# Dedupe while preserving order.
MFA_LIST="$(python3 -c 'import sys; seen=set(); out=[]
for tok in sys.argv[1].split():
    if tok.isdigit() and int(tok)>0 and tok not in seen:
        seen.add(tok); out.append(tok)
print(" ".join(out[:int(sys.argv[2])]))' "$MFA_LIST" "$MAX_MFA")"

if [[ -z "$MFA_LIST" ]]; then
  say "FAIL: no MFA_ID candidates (warm DB empty and manufacturers walk empty)." >&2
  exit 3
fi
say "MFA candidates: ${MFA_LIST}"

MFA_ID=0
MS_ID=0
TRIED=0
: > /tmp/ecomae-vehicle-models.json
for candidate in $MFA_LIST; do
  TRIED=$((TRIED + 1))
  say "== models?section=${SECTION}&mfa_id=${candidate} (try ${TRIED}) =="
  code="$(curl_json /tmp/ecomae-vehicle-models.json "/api/v1/catalog/models?section=${SECTION}&mfa_id=${candidate}")"
  rows="$(python3 -c 'import json; d=json.load(open("/tmp/ecomae-vehicle-models.json")); print(int(d.get("rows") or len(d.get("data") or [])))' 2>/dev/null || echo 0)"
  say "HTTP ${code} rows=${rows}"
  if [[ "$code" == "200" && "$rows" -gt 0 ]]; then
    MFA_ID="$candidate"
    MS_ID="$(pick_id /tmp/ecomae-vehicle-models.json MS_ID ms_id)"
    break
  fi
done

say "chosen MFA_ID=${MFA_ID} MS_ID=${MS_ID} (tried ${TRIED})"
if [[ "$MFA_ID" -gt 0 ]]; then
  python3 -m json.tool /tmp/ecomae-vehicle-models.json | head -30
else
  say "WARN: no non-empty models among ${MAX_MFA} MFA_IDs." >&2
  say "Hint: bash scripts/cloudpanel_list_warm_catalog_models_mfa.sh" >&2
  exit 4
fi

# Build MS candidates: warm mods table first, then MS_IDs from chosen models payload.
MS_FROM_MODELS="$(python3 - <<'PY'
import json
rows=json.load(open("/tmp/ecomae-vehicle-models.json")).get("data") or []
ids=[]
seen=set()
for row in rows:
    if not isinstance(row, dict):
        continue
    for k in ("MS_ID", "ms_id"):
        try:
            n=int(row.get(k) or 0)
        except (TypeError, ValueError):
            continue
        if n>0 and n not in seen:
            seen.add(n); ids.append(str(n))
            break
    if len(ids)>=40:
        break
print(" ".join(ids))
PY
)"
MS_LIST="$(python3 -c 'import sys; seen=set(); out=[]
for tok in (sys.argv[1]+" "+sys.argv[2]+" "+sys.argv[3]).split():
    if tok.isdigit() and int(tok)>0 and tok not in seen:
        seen.add(tok); out.append(tok)
print(" ".join(out[:40]))' "$WARM_MS" "$MS_ID" "$MS_FROM_MODELS")"

if [[ -z "$MS_LIST" ]]; then
  say "WARN: no MS_ID candidates — cannot probe modifications." >&2
  exit 0
fi
say "MS candidates: ${MS_LIST}"

CHOSEN_MS=0
MS_TRIED=0
: > /tmp/ecomae-vehicle-mods.json
for candidate in $MS_LIST; do
  MS_TRIED=$((MS_TRIED + 1))
  say "== modifications?section=${SECTION}&ms_id=${candidate} (try ${MS_TRIED}) =="
  mods_code="$(curl_json /tmp/ecomae-vehicle-mods.json "/api/v1/catalog/modifications?section=${SECTION}&ms_id=${candidate}")"
  mods_rows="$(python3 -c 'import json; d=json.load(open("/tmp/ecomae-vehicle-mods.json")); print(int(d.get("rows") or len(d.get("data") or [])))' 2>/dev/null || echo 0)"
  say "HTTP ${mods_code} rows=${mods_rows}"
  if [[ "$mods_code" == "200" && "$mods_rows" -gt 0 ]]; then
    CHOSEN_MS="$candidate"
    break
  fi
done

say "chosen MS_ID=${CHOSEN_MS} (tried ${MS_TRIED})"
if [[ "$CHOSEN_MS" -gt 0 ]]; then
  python3 -m json.tool /tmp/ecomae-vehicle-mods.json | head -30
else
  say "WARN: modifications auth works but no warm ms_id among candidates (database-empty)." >&2
  say "Hint: bash scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh modifications" >&2
fi
say "OK: vehicle-chain probe finished (PHP remains)."
