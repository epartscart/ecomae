#!/usr/bin/env bash
# Capture ASP.NET catalog miss samples for Batch 5 dual-sample compare.
# Default: write/refresh contract stubs. With ECOMAE_CATALOG_API_KEY, probe cold keys.
# Never prints API keys. Never claims PHP cutover.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${ECOMAE_CATALOG_MISS_SAMPLES_DIR:-$ROOT/docs/migration/evidence/catalog-miss-umapi}"
BASE="${ECOMAE_ASPNET_BASE_URL:-${ECOMAE_ASPNET_LOOPBACK:-http://127.0.0.1:5100}}"
PUBLIC="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
UA='Mozilla/5.0 (compatible; EcomAE-CatalogMissCapture/1.0)'

if [[ -z "${ECOMAE_CATALOG_API_KEY:-}" && -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

KEY="${ECOMAE_CATALOG_API_KEY:-${CATALOG_API_KEY:-}}"
OVERWRITE="${ECOMAE_OVERWRITE_CATALOG_MISS_SAMPLES:-0}"
mkdir -p "$OUT_DIR"

printf '== Catalog miss dual-sample capture (Batch 5) ==\n'
printf 'Base: %s\n' "$BASE"
printf 'Out:  %s\n' "$OUT_DIR"
if [[ -n "$KEY" ]]; then
  printf 'API key: present (value not printed)\n'
else
  printf 'API key: missing — writing/keeping contract stubs only\n'
fi

export ECOMAE_CATALOG_MISS_SAMPLES_DIR="$OUT_DIR"
export ECOMAE_ASPNET_BASE_URL="$BASE"
export ECOMAE_PUBLIC_BASE_URL="$PUBLIC"
export ECOMAE_CATALOG_API_KEY_PRESENT="$([ -n "$KEY" ] && echo 1 || echo 0)"
export ECOMAE_OVERWRITE_CATALOG_MISS_SAMPLES="$OVERWRITE"
# Pass key via env for python without echoing it in this script's argv logs.
export ECOMAE_CATALOG_API_KEY="$KEY"

python3 - <<'PY'
import json, os, subprocess, tempfile, datetime
from pathlib import Path

out_dir = Path(os.environ["ECOMAE_CATALOG_MISS_SAMPLES_DIR"])
base = os.environ.get("ECOMAE_ASPNET_BASE_URL", "http://127.0.0.1:5100").rstrip("/")
public = os.environ.get("ECOMAE_PUBLIC_BASE_URL", "https://www.ecomae.com").rstrip("/")
key = os.environ.get("ECOMAE_CATALOG_API_KEY") or ""
overwrite = os.environ.get("ECOMAE_OVERWRITE_CATALOG_MISS_SAMPLES", "0") == "1"
ua = "Mozilla/5.0 (compatible; EcomAE-CatalogMissCapture/1.0)"
now = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")

TARGETS = [
    ("engines", "/api/v1/catalog/engines", "section=passenger&mfa_id=999999001", "cache_miss"),
    ("analogs", "/api/v1/catalog/analogs", "section=passenger&article=ZZZMISSNOFILL001&brand=ZZZ", "cache_miss"),
    ("vin", "/api/v1/catalog/vin", "vin=ZZZMISSNOFILLVIN01", "vin_cache_miss"),
    ("article-brands", "/api/v1/catalog/article-brands", "section=passenger&article=ZZZMISSNOFILL001", "cache_miss"),
]

def write_stub(action: str, route: str, query: str, code: str) -> None:
    path = out_dir / f"aspnet-{action}-miss.json"
    if path.exists() and not overwrite:
        print(f"keep existing {path.name}")
        return
    doc = {
        "role": "aspnet-catalog-miss-sample",
        "action": action,
        "route": route,
        "query": query,
        "httpStatus": 404,
        "ok": False,
        "error": {
            "code": code,
            "message": "Contract stub miss envelope; PHP/UMAPI remains authoritative for live fills.",
        },
        "phpAuthoritative": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "capturedAt": now,
        "baseUrl": base,
        "note": "Contract stub. Re-run with ECOMAE_CATALOG_API_KEY + ECOMAE_OVERWRITE_CATALOG_MISS_SAMPLES=1.",
    }
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"wrote stub {path.name}")

def write_php_inventory() -> None:
    path = out_dir / "php-umapi-fill-inventory.json"
    if path.exists() and not overwrite:
        print(f"keep existing {path.name}")
        return
    doc = {
        "role": "php-umapi-fill-inventory",
        "phpAuthoritative": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "capturedAt": now,
        "liveFillPaths": ["api/umapi_proxy.php", "api/v1/catalog.php"],
        "alwaysLiveActions": ["articles", "engine"],
        "cacheableActionsStillPhpFill": [
            "manufacturers", "models", "modifications", "categories", "products",
            "suppliers", "article", "vin", "engines", "engine_search", "brands",
            "analogs", "article_links",
        ],
        "note": "ASP.NET exact-routes read cache only. Miss fill remains PHP.",
    }
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {path.name}")

def curl_capture(path_q: str) -> tuple[int, dict]:
    body = tempfile.NamedTemporaryFile(delete=False)
    body.close()
    headers = ["-H", f"X-API-Key: {key}", "-A", ua]
    for host in (base, public):
        proc = subprocess.run(
            ["curl", "-sS", "-m", "30", "-o", body.name, "-w", "%{http_code}", *headers, f"{host}{path_q}"],
            capture_output=True,
            text=True,
        )
        code_s = (proc.stdout or "").strip() or "000"
        try:
            status = int(code_s)
        except ValueError:
            status = 0
        try:
            payload = json.loads(Path(body.name).read_text(encoding="utf-8") or "{}")
        except Exception:
            payload = {"raw": Path(body.name).read_text(encoding="utf-8", errors="replace")[:500]}
        if status in (200, 400, 401, 403, 404):
            return status, payload if isinstance(payload, dict) else {"payload": payload}
    return status, payload if isinstance(payload, dict) else {}

def capture_live(action: str, route: str, query: str, expected_code: str) -> None:
    path = out_dir / f"aspnet-{action}-miss.json"
    status, payload = curl_capture(f"{route}?{query}")
    err = payload.get("error") if isinstance(payload.get("error"), dict) else {}
    code = str(err.get("code") or "")
    doc = {
        "role": "aspnet-catalog-miss-sample",
        "action": action,
        "route": route,
        "query": query,
        "httpStatus": status,
        "ok": bool(payload.get("ok")) if "ok" in payload else False,
        "error": err or {"code": code or ("unauthorized" if status == 401 else "unknown"), "message": str(payload.get("note") or "")},
        "phpAuthoritative": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "capturedAt": now,
        "baseUrl": base,
        "expectedMissCode": expected_code,
        "note": "Live capture. 404+cache_miss/vin_cache_miss expected for cold keys; 200 means key was warm — pick colder params.",
    }
    if status == 200:
        doc["note"] += " WARN: got cache HIT; not a miss sample."
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"captured {path.name} http={status} code={doc['error'].get('code')}")

write_php_inventory()
if not key:
    for action, route, query, code in TARGETS:
        write_stub(action, route, query, code)
else:
    for action, route, query, code in TARGETS:
        if Path(out_dir / f"aspnet-{action}-miss.json").exists() and not overwrite:
            print(f"keep existing aspnet-{action}-miss.json (set ECOMAE_OVERWRITE_CATALOG_MISS_SAMPLES=1)")
            continue
        capture_live(action, route, query, code)

print("Done. Compare with: python3 scripts/compare_catalog_miss_dual_samples.py")
PY
