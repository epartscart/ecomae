#!/usr/bin/env bash
# Classify live URLs as aspnet / php-html / other. Never changes cutover or removes PHP.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${ECOMAE_PROBE_OUT_DIR:-$ROOT/docs/migration/evidence/decommission/public-probes}"
OUT_FILE="${OUT_DIR}/www-live-surface-stack.json"
mkdir -p "$OUT_DIR"

urls=(
  "https://www.ecomae.com/"
  "https://www.ecomae.com/CP/"
  "https://www.ecomae.com/ERP/"
  "https://www.ecomae.com/BOS/"
  "https://cp.ecomae.com/CP/"
  "https://cp.ecomae.com/ERP/"
  "https://cp.ecomae.com/BOS/"
  "https://www.ecomae.com/health"
  "https://www.ecomae.com/migration/zero-php-completion"
  "https://www.ecomae.com/migration/php-decommission-readiness"
  "https://www.ecomae.com/migration/presentation-parity"
  "https://www.ecomae.com/migration/live-surface-links"
  "https://www.ecomae.com/migration/surface-field-parity"
  "https://www.ecomae.com/migration/surface-parity"
  "https://www.ecomae.com/migration/data-parity"
  "https://www.ecomae.com/api/v1/price/lookup"
  "https://www.ecomae.com/cp/dashboard-summary"
  "https://www.ecomae.com/erp/dashboard-summary"
  "https://www.ecomae.com/bos/fleet-summary"
  "https://www.ecomae.com/api/v1/catalog/status"
  "https://www.ecomae.com/api/v1/catalog/manufacturers"
  "https://www.ecomae.com/api/v1/catalog/models"
  "https://www.ecomae.com/api/v1/catalog/modifications"
  "https://www.ecomae.com/api/v1/catalog/brands"
  "https://www.ecomae.com/api/v1/catalog/suppliers"
  "https://www.ecomae.com/api/v1/catalog/vin"
  "https://www.ecomae.com/api/v1/catalog/engines"
  "https://www.ecomae.com/api/v1/catalog/analogs"
  "https://www.ecomae.com/api/v1/catalog/article-brands"
  "https://www.ecomae.com/api/v1/catalog/categories"
  "https://www.ecomae.com/api/v1/catalog/products"
  "https://www.ecomae.com/api/v1/catalog/engine-search"
  "https://www.ecomae.com/api/v1/catalog/article-links"
  "https://www.ecomae.com/api/v1/catalog/article"
  "https://www.ecomae.com/api/v1/catalog/articles"
  "https://www.ecomae.com/api/v1/catalog/engine"
  "https://www.ecomae.com/api/v1/catalog/brand-parts"
  # Loopback-oriented parity boards (public host may still be PHP until allowlisted):
  "https://www.ecomae.com/cp/parity"
  "https://www.ecomae.com/erp/parity"
  "https://www.ecomae.com/bos/parity"
  "https://www.ecomae.com/storefront/parity"
  "https://healthcare.ecomae.com/"
  "https://healthcare.ecomae.com/CP/"
  "https://healthcare.ecomae.com/ERP/"
  "https://www.electronicae.com/"
  "https://www.electronicae.com/CP/"
  "https://www.electronicae.com/ERP/"
  "https://www.stylenlook.com/"
  "https://www.stylenlook.com/CP/"
  "https://www.stylenlook.com/ERP/"
  "https://www.thejewellerytrend.com/"
  "https://www.taxofinca.com/"
  "https://epartscart.com/"
  "https://epartscart.com/CP/"
  "https://epartscart.com/ERP/"
)

tmp="$(mktemp)"
: >"$tmp"

for url in "${urls[@]}"; do
  body="$(mktemp)"
  hdr="$(mktemp)"
  code="$(curl -sS -m 20 -D "$hdr" -o "$body" -w '%{http_code}' -L "$url" || echo 000)"
  ctype="$(awk -F': ' 'tolower($1)=="content-type"{print $2; exit}' "$hdr" | tr -d '\r')"
  stack="other"
  if grep -qi 'application/json' <<<"$ctype"; then
    stack="aspnet-json"
  elif grep -Eiq 'missing_api_key|zero-php|presentation-shell|php-decommission|live-surface' "$body"; then
    stack="aspnet-json"
  elif [[ "$url" == */health ]] && grep -Eiq 'Healthy|Degraded|Unhealthy' "$body"; then
    stack="aspnet-health"
  elif grep -Eiq '<!DOCTYPE|<html' "$body"; then
    stack="php-html"
  fi
  bytes="$(wc -c <"$body" | tr -d ' ')"
  printf '%s\t%s\t%s\t%s\t%s\n' "$url" "$code" "$stack" "$ctype" "$bytes" >>"$tmp"
  rm -f "$body" "$hdr"
  printf '%-70s %s %s\n' "$url" "$code" "$stack"
done

python3 - "$OUT_FILE" "$tmp" <<'PY'
import json, sys, datetime
out, src = sys.argv[1], sys.argv[2]
rows = []
with open(src, encoding="utf-8") as fh:
    for line in fh:
        url, code, stack, ctype, bytes_ = line.rstrip("\n").split("\t")
        rows.append({
            "url": url,
            "status": int(code) if code.isdigit() else 0,
            "stack": stack,
            "contentType": ctype,
            "bytes": int(bytes_),
        })
payload = {
    "capturedAtUtc": datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "note": "Public probe only. Does not authorize PHP removal or broad cutover.",
    "routes": rows,
}
with open(out, "w", encoding="utf-8") as fh:
    json.dump(payload, fh, indent=2)
    fh.write("\n")
print(out)
PY
rm -f "$tmp"
echo "Artifact: $OUT_FILE"
