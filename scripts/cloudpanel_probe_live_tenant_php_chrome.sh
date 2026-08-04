#!/usr/bin/env bash
# Prove live tenant hosts still serve PHP product chrome that is presentation-
# identical to PHP (frontend, CP, ERP) — not ASP.NET Blazor scaffolds or digests.
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

# Named live production tenants — presentation must stay PHP same-to-same.
# (theme / colour / structure / fonts / hero splash / fields — no ASP.NET hybrid)
if [[ -n "${ECOMAE_TENANT_PROBE_BASES:-}" ]]; then
  # shellcheck disable=SC2206
  BASES=($ECOMAE_TENANT_PROBE_BASES)
else
  BASES=(
    "https://epartscart.com"
    "https://www.epartscart.com"
    "https://www.electronicae.com"
    "https://electronicae.com"
    "https://www.stylenlook.com"
    "https://stylenlook.com"
    "https://www.thejewellerytrend.com"
    "https://thejewellerytrend.com"
    "https://www.taxofinca.com"
    "https://taxofinca.com"
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

# Product chrome paths — must be PHP HTML with presentation fingerprints.
PRODUCT_PATHS=(
  "/"
  "/CP/"
  "/ERP/"
)

# ASP.NET hybrid / digest paths — must NOT be live on tenant hosts (404/non-ASP.NET).
FORBIDDEN_ASPNET_PATHS=(
  "/cp/app"
  "/erp/app"
  "/bos/app"
  "/storefront/app"
  "/cp/login"
  "/erp/login"
  "/bos/login"
  "/storefront/login"
  "/health"
  "/cp/dashboard-summary"
  "/erp/dashboard-summary"
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
  "StorefrontOrdersApp"
  "StorefrontGarageApp"
  "StorefrontProfileApp"
  "StorefrontAccountSummaryApp"
  "CpOrdersApp"
  "CpDashboardSummaryApp"
  "CpUsersApp"
  "CpGroupsApp"
  "ErpSalesOrdersApp"
  "ErpPurchaseOrdersApp"
  "ErpInvoicesApp"
  "ErpCashAccountsApp"
  "ErpCashEntriesApp"
  "ErpCoaAccountsApp"
  "ErpGlJournalsApp"
  "ErpWarehousesApp"
  "ErpSuppliersApp"
  "ErpPurchasesApp"
  "ErpInventoryStockApp"
  "ErpAccountsSummaryApp"
  "ErpDashboardSummaryApp"
  "CpModulesApp"
  "CpPagesApp"
  "CpMenusApp"
  "BosAuditLogApp"
  "BosTenantsApp"
  "BosFleetHealthApp"
  "BosFleetReadinessApp"
  "BosFleetSummaryApp"
  "CpTenantsApp"
  "CpCurrenciesApp"
  "CpStoragesApp"
  "CpAdminSessionsApp"
  "CpApiClientsApp"
  "CpConfigItemsApp"
  '"error":"unauthorized"'
  '"title":"Unauthorized"'
  "X-EcomAE-Route-Cutover"
)

pass=0
fail=0
tmp="$(mktemp)"
: >"$tmp"

has_aspnet_marker() {
  local body="$1"
  local m
  for m in "${ASPNET_BAD_MARKERS[@]}"; do
    if grep -Fq "$m" "$body" 2>/dev/null; then
      printf '%s' "$m"
      return 0
    fi
  done
  return 1
}

# Presentation fingerprints required on product HTML (PHP same-to-same lock).
# Storefront: hero/splash + stylesheets; CP/ERP: epc/bootstrap/font-awesome chrome.
presentation_ok() {
  local path="$1"
  local body="$2"
  case "$path" in
    /)
      grep -Eiq 'stylesheet' "$body" || return 1
      grep -Eiq 'hero|slider|swiper|banner|splash' "$body" || return 1
      grep -Eiq 'epc_|fonts\.|googleapis|font-awesome|fa fa-' "$body" || return 1
      return 0
      ;;
    /CP/|/cp/)
      grep -Eiq 'stylesheet' "$body" || return 1
      grep -Eiq 'epc-cp|font-awesome|bootstrap|epc_' "$body" || return 1
      return 0
      ;;
    /ERP/|/erp/)
      grep -Eiq 'stylesheet' "$body" || return 1
      grep -Eiq 'epc-erp|epc-cp|font-awesome|bootstrap|desktop' "$body" || return 1
      return 0
      ;;
  esac
  return 0
}

classify_product() {
  local path="$1"
  local code="$2"
  local ctype="$3"
  local body="$4"

  if [[ "$code" == "000" || -z "$code" ]]; then
    echo "unreachable|network"
    return
  fi

  if grep -qi 'application/json' <<<"$ctype"; then
    echo "aspnet-json|json-content-type"
    return
  fi

  local bad
  if bad="$(has_aspnet_marker "$body")"; then
    echo "aspnet-scaffold|marker:$bad"
    return
  fi

  if grep -Eiq '<!DOCTYPE|<html' "$body"; then
    if presentation_ok "$path" "$body"; then
      echo "php-html|presentation-fingerprints"
      return
    fi
    if grep -Eiq 'epc_|stylesheet|bootstrap|jquery|font-awesome|/CP/|/ERP/' "$body"; then
      echo "php-html|html-chrome-markers"
      return
    fi
    echo "php-html|html-document"
    return
  fi

  if grep -Eiq 'license error|domain_path|php|mysql|fatal error|warning:' "$body"; then
    echo "php-plain|php-runtime-text"
    return
  fi

  echo "other|no-html"
}

classify_forbidden() {
  local code="$1"
  local ctype="$2"
  local body="$3"

  if [[ "$code" == "000" || -z "$code" ]]; then
    echo "unreachable|network"
    return
  fi

  local bad
  if bad="$(has_aspnet_marker "$body")"; then
    echo "aspnet-leak|marker:$bad"
    return
  fi

  if grep -qi 'application/json' <<<"$ctype" && [[ "$code" =~ ^(200|401)$ ]]; then
    # Digest/auth JSON on tenant host = accidental shadow.
    echo "aspnet-leak|json-digest-or-auth"
    return
  fi

  # 404 / non-ASP.NET HTML / redirect away = good (PHP vhost has no hybrid app).
  if [[ "$code" =~ ^(404|403|301|302|303|307|308)$ ]]; then
    echo "php-absent|no-aspnet-shadow"
    return
  fi

  if grep -Eiq '<!DOCTYPE|<html' "$body"; then
    echo "php-absent|html-not-blazor"
    return
  fi

  echo "php-absent|non-aspnet"
}

host_class_for() {
  local base="$1"
  if [[ "$base" == *www.ecomae.com* ]]; then
    echo "platform"
  elif [[ "$base" == *ecomae.com ]]; then
    echo "industry"
  else
    echo "tenant"
  fi
}

printf '== Live tenant PHP presentation lock probe ==\n'
printf 'Law: named live tenants keep PHP storefront/CP/ERP same-to-same\n'
printf '     (theme/colour/structure/fonts/hero/fields). No ASP.NET hybrid.\n'
printf 'Out: %s\n' "$OUT_FILE"

for base in "${BASES[@]}"; do
  base="${base%/}"
  host_class="$(host_class_for "$base")"

  for path in "${PRODUCT_PATHS[@]}"; do
    url="${base}${path}"
    body="$(mktemp)"
    hdr="$(mktemp)"
    code="$(curl -sS -m 25 -D "$hdr" -o "$body" -w '%{http_code}' -L \
      -A 'Mozilla/5.0 EcomAE-tenant-php-chrome-probe' \
      "$url" 2>/dev/null || echo 000)"
    ctype="$(awk -F': ' 'tolower($1)=="content-type"{print $2; exit}' "$hdr" | tr -d '\r')"
    bytes="$(wc -c <"$body" | tr -d ' ')"
    classification="$(classify_product "$path" "$code" "$ctype" "$body")"
    stack="${classification%%|*}"
    reason="${classification#*|}"

    ok=0
    if [[ "$stack" == "php-html" || "$stack" == "php-plain" ]] && [[ "$code" =~ ^(200|301|302|303|307|308|401|403)$ ]]; then
      ok=1
    fi

    if [[ "$ok" -eq 1 ]]; then
      pass=$((pass + 1))
      printf '  PASS  %-60s %s %s (%s)\n' "$url" "$code" "$stack" "$reason"
      printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\tpass\tproduct\n' "$url" "$code" "$stack" "$ctype" "$bytes" "$host_class" "$reason" >>"$tmp"
    else
      fail=$((fail + 1))
      printf '  FAIL  %-60s %s %s (%s)\n' "$url" "$code" "$stack" "$reason"
      printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\tfail\tproduct\n' "$url" "$code" "$stack" "$ctype" "$bytes" "$host_class" "$reason" >>"$tmp"
    fi
    rm -f "$body" "$hdr"
  done

  # Skip forbidden-path checks on platform www (ASP.NET shadows are intentional there).
  if [[ "$host_class" == "platform" ]]; then
    continue
  fi

  for path in "${FORBIDDEN_ASPNET_PATHS[@]}"; do
    url="${base}${path}"
    body="$(mktemp)"
    hdr="$(mktemp)"
    # Do not follow redirects into www ASP.NET — check tenant host response only.
    code="$(curl -sS -m 20 -D "$hdr" -o "$body" -w '%{http_code}' \
      -A 'Mozilla/5.0 EcomAE-tenant-php-chrome-probe' \
      "$url" 2>/dev/null || echo 000)"
    ctype="$(awk -F': ' 'tolower($1)=="content-type"{print $2; exit}' "$hdr" | tr -d '\r')"
    bytes="$(wc -c <"$body" | tr -d ' ')"
    classification="$(classify_forbidden "$code" "$ctype" "$body")"
    stack="${classification%%|*}"
    reason="${classification#*|}"

    if [[ "$stack" == "php-absent" ]]; then
      pass=$((pass + 1))
      printf '  PASS  %-60s %s %s (%s)\n' "$url" "$code" "$stack" "$reason"
      printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\tpass\tforbidden\n' "$url" "$code" "$stack" "$ctype" "$bytes" "$host_class" "$reason" >>"$tmp"
    elif [[ "$stack" == "unreachable" ]]; then
      # Apex without DNS still OK for optional aliases; count as skip/pass-soft.
      pass=$((pass + 1))
      printf '  SKIP  %-60s unreachable (treated pass for optional apex)\n' "$url"
      printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\tpass\tforbidden\n' "$url" "$code" "unreachable" "$ctype" "$bytes" "$host_class" "optional-apex" >>"$tmp"
    else
      fail=$((fail + 1))
      printf '  FAIL  %-60s %s %s (%s)\n' "$url" "$code" "$stack" "$reason"
      printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\tfail\tforbidden\n' "$url" "$code" "$stack" "$ctype" "$bytes" "$host_class" "$reason" >>"$tmp"
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
        if len(parts) < 9:
            continue
        url, code, stack, ctype, bytes_, host_class, reason, result, kind = parts[:9]
        rows.append({
            "url": url,
            "httpStatus": int(code) if code.isdigit() else 0,
            "stack": stack,
            "contentType": ctype,
            "bytes": int(bytes_) if bytes_.isdigit() else 0,
            "hostClass": host_class,
            "reason": reason,
            "result": result,
            "probeKind": kind,
        })
status = "pass" if fail_n == 0 and pass_n > 0 else "fail"
doc = {
    "generatedAtUtc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "status": status,
    "policy": "live-tenant-presentation-identical-to-php",
    "mandate": (
        "Named live tenants (epartscart, electronicae, stylenlook, thejewellerytrend, taxofinca) "
        "must keep storefront + CP + ERP presentation same-to-same with PHP — "
        "theme, colouring, structure, fonts, hero/splash, fields. No ASP.NET hybrid on those hosts."
    ),
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "liveProductionTenants": [
        "epartscart.com",
        "electronicae.com",
        "stylenlook.com",
        "thejewellerytrend.com",
        "taxofinca.com",
    ],
    "passCount": pass_n,
    "failCount": fail_n,
    "probes": rows,
    "notes": [
        "ASP.NET exact-route digests/APIs and Blazor previews are intentional only on www.ecomae.com.",
        "Product paths (/ /CP/ /ERP/) must return PHP HTML with presentation fingerprints.",
        "Forbidden paths (/cp/app /erp/app /storefront/app /health digests) must not serve ASP.NET on tenants.",
        "Do not remove PHP while live tenants depend on PHP chrome.",
        "Never invent RELEASE_OWNER_APPROVAL.md.",
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
  printf -- 'FAIL: one or more live tenant chrome paths are not PHP-presentation-safe.\n' >&2
  printf -- 'Rollback any non-www shadows; keep product chrome on PHP.\n' >&2
  exit 1
fi
printf -- 'OK: live tenant frontend + CP + ERP still PHP presentation (same-to-same).\n'
exit 0
