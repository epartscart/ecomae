#!/usr/bin/env bash
# Prove live tenant / industry hosts still serve PHP product chrome
# (frontend, CP, ERP) — not ASP.NET Blazor scaffolds or digest JSON.
# Never changes nginx or removes PHP.
#
# Usage:
#   bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh
#
# Optional:
#   ECOMAE_TENANT_PROBE_BASES="https://epartscart.com https://www.electronicae.com"
#   ECOMAE_PROBE_OUT_DIR=docs/migration/evidence/tenant-safety
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${ECOMAE_PROBE_OUT_DIR:-$ROOT/docs/migration/evidence/tenant-safety}"
OUT_FILE="$OUT_DIR/live-tenant-php-chrome.json"
mkdir -p "$OUT_DIR"

# Default: dedicated live tenants + ePartsCart + platform chrome control.
# Industry showcase hosts are optional (ECOMAE_INCLUDE_INDUSTRY_PROBE=1) — some return
# PHP license/domain_path plain text unrelated to ASP.NET migration.
if [[ -n "${ECOMAE_TENANT_PROBE_BASES:-}" ]]; then
  # shellcheck disable=SC2206
  BASES=($ECOMAE_TENANT_PROBE_BASES)
else
  BASES=(
    "https://epartscart.com"
    "https://www.epartscart.com"
    "https://www.electronicae.com"
    "https://www.stylenlook.com"
    "https://www.thejewellerytrend.com"
    "https://www.taxofinca.com"
    "https://www.ecomae.com"
  )
  if [[ "${ECOMAE_INCLUDE_INDUSTRY_PROBE:-0}" == "1" ]]; then
    BASES+=(
      "https://healthcare.ecomae.com"
      "https://retail.ecomae.com"
      "https://homeliving.ecomae.com"
      "https://fashion.ecomae.com"
    )
  fi
fi

PATHS=(
  "/"
  "/CP/"
  "/ERP/"
)

# Markers that mean ASP.NET product shell accidentally replaced PHP chrome.
ASPNET_BAD_MARKERS=(
  "blazor.web.js"
  "MigrationConsole"
  "php-chrome-shell"
  "CpCommandCentre"
  "ErpBosDashboard"
  "BosFleetApp"
  "StorefrontPreview"
  "StorefrontSearchApp"
  "StorefrontCartApp"
  "CpOrdersApp"
  "CpUsersApp"
  "CpGroupsApp"
  "ErpSalesOrdersApp"
  "ErpPurchaseOrdersApp"
  "ErpInvoicesApp"
  '"error":"unauthorized"'
  '"title":"Unauthorized"'
  "X-EcomAE-Route-Cutover"
)

# PHP chrome markers commonly present on live product pages.
PHP_GOOD_MARKERS=(
  "text/html"
)

pass=0
fail=0
skip=0
tmp="$(mktemp)"
: >"$tmp"

classify_body() {
  local url="$1"
  local code="$2"
  local ctype="$3"
  local body="$4"
  local stack="other"
  local reason=""

  if [[ "$code" == "000" || -z "$code" ]]; then
    echo "unreachable|network"
    return
  fi

  # Redirects to login still count as PHP chrome as long as HTML (not JSON digest).
  if grep -qi 'application/json' <<<"$ctype"; then
    # Product chrome paths (/ /CP/ /ERP/) must never be JSON digests.
    echo "aspnet-json|json-content-type"
    return
  fi

  local bad=""
  for m in "${ASPNET_BAD_MARKERS[@]}"; do
    if grep -Fq "$m" "$body" 2>/dev/null; then
      bad="$m"
      break
    fi
  done
  if [[ -n "$bad" ]]; then
    echo "aspnet-scaffold|marker:$bad"
    return
  fi

  if grep -Eiq '<!DOCTYPE|<html' "$body"; then
    # Prefer clear PHP/app markers when present; HTML alone is enough for chrome paths.
    if grep -Eiq 'wp-content|ecomae|control.?panel|/CP/|/ERP/|login|session|bootstrap|jquery|font-awesome|stylesheet' "$body"; then
      echo "php-html|html-chrome-markers"
      return
    fi
    echo "php-html|html-document"
    return
  fi

  # Pre-existing PHP runtime/license plain text (not ASP.NET cutover).
  if grep -Eiq 'license error|domain_path|php|mysql|fatal error|warning:' "$body"; then
    echo "php-plain|php-runtime-text"
    return
  fi

  echo "other|no-html"
}

printf '== Live tenant PHP chrome probe ==\n'
printf 'Out: %s\n' "$OUT_FILE"

for base in "${BASES[@]}"; do
  base="${base%/}"
  for path in "${PATHS[@]}"; do
    url="${base}${path}"
    body="$(mktemp)"
    hdr="$(mktemp)"
    code="$(curl -sS -m 25 -D "$hdr" -o "$body" -w '%{http_code}' -L \
      -A 'Mozilla/5.0 EcomAE-tenant-php-chrome-probe' \
      "$url" 2>/dev/null || echo 000)"
    ctype="$(awk -F': ' 'tolower($1)=="content-type"{print $2; exit}' "$hdr" | tr -d '\r')"
    bytes="$(wc -c <"$body" | tr -d ' ')"
    classification="$(classify_body "$url" "$code" "$ctype" "$body")"
    stack="${classification%%|*}"
    reason="${classification#*|}"

    # Pass = still PHP (HTML chrome or PHP runtime plain text), not ASP.NET scaffold/JSON.
    ok=0
    if [[ "$stack" == "php-html" || "$stack" == "php-plain" ]] && [[ "$code" =~ ^(200|301|302|303|307|308|401|403)$ ]]; then
      ok=1
    fi

    host_class="tenant"
    if [[ "$base" == *ecomae.com ]]; then
      if [[ "$base" == *www.ecomae.com* ]]; then
        host_class="platform"
      else
        host_class="industry"
      fi
    fi

    if [[ "$ok" -eq 1 ]]; then
      pass=$((pass + 1))
      printf '  PASS  %-55s %s %s (%s)\n' "$url" "$code" "$stack" "$reason"
      printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\tpass\n' "$url" "$code" "$stack" "$ctype" "$bytes" "$host_class" "$reason" >>"$tmp"
    else
      fail=$((fail + 1))
      printf '  FAIL  %-55s %s %s (%s)\n' "$url" "$code" "$stack" "$reason"
      printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\tfail\n' "$url" "$code" "$stack" "$ctype" "$bytes" "$host_class" "$reason" >>"$tmp"
    fi
    rm -f "$body" "$hdr"
  done
done

python3 - "$OUT_FILE" "$tmp" "$pass" "$fail" <<'PY'
import json, sys, datetime
out, src, pass_n, fail_n = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
rows = []
with open(src, encoding="utf-8") as fh:
    for line in fh:
        parts = line.rstrip("\n").split("\t")
        if len(parts) < 8:
            continue
        url, code, stack, ctype, bytes_, host_class, reason, result = parts[:8]
        rows.append({
            "url": url,
            "httpStatus": int(code) if code.isdigit() else 0,
            "stack": stack,
            "contentType": ctype,
            "bytes": int(bytes_) if bytes_.isdigit() else 0,
            "hostClass": host_class,
            "reason": reason,
            "result": result,
        })
status = "pass" if fail_n == 0 and pass_n > 0 else "fail"
doc = {
    "generatedAtUtc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "status": status,
    "policy": "live-tenant-product-chrome-must-remain-php",
    "passCount": pass_n,
    "failCount": fail_n,
    "probes": rows,
    "notes": [
        "ASP.NET exact-route digests/APIs on www.ecomae.com are intentional and out of scope here.",
        "This probe fails if / /CP/ /ERP/ on live tenant hosts look like Blazor scaffolds or JSON digests.",
        "php-plain (e.g. PHP license/domain_path text) is not ASP.NET cutover; industry hosts are optional.",
        "Do not remove PHP while live tenants depend on PHP chrome.",
    ],
}
with open(out, "w", encoding="utf-8") as fh:
    json.dump(doc, fh, indent=2)
    fh.write("\n")
print(f"Wrote {out} status={status}")
PY

rm -f "$tmp"
printf -- '----------------------------\n'
printf -- 'Passed: %s  Failed: %s\n' "$pass" "$fail"
if [[ "$fail" -gt 0 ]]; then
  printf -- 'FAIL: one or more live tenant chrome paths are not PHP-safe.\n' >&2
  printf -- 'Rollback any non-www shadows; keep product chrome on PHP.\n' >&2
  exit 1
fi
printf -- 'OK: live tenant frontend + CP + ERP chrome still PHP (unaffected by ASP.NET migration shadows).\n'
exit 0
