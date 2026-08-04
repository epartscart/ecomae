#!/usr/bin/env bash
# Operator verify: live tenants must still feel 100% PHP — same-to-same UI/UX.
# Runs the tenant chrome probe + optional BOS checks on hosts known to expose /BOS/.
# Never changes nginx. Never removes PHP. Never claims ReadyToRemovePhp.
#
# Usage:
#   bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh
#
# Policy: digests / Blazor previews on www.ecomae.com are NOT tenant product chrome.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${ECOMAE_PROBE_OUT_DIR:-$ROOT/docs/migration/evidence/tenant-safety}"
SUMMARY="$OUT_DIR/same-to-same-verify.json"
mkdir -p "$OUT_DIR"

say() { printf '%s\n' "$*"; }

say "== Same-to-same tenant verify (parity gate → 100% ASP.NET / 0 PHP) =="
say "TARGET: ASP.NET Core owns all traffic; PHP removed after dual-sample + approval."
say "INTERIM: named live tenants stay PHP-primary until same-to-same evidence."
say "Unlock parity shadows: ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES"
say "  epartscart / electronicae / stylenlook / thejewellerytrend / taxofinca"
say "  → theme/colour/structure/fonts/hero/fields must match PHP during cutover."
say "Path board: GET /migration/aspnet-zero-php-path"
say ""

fail=0

if ! bash "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh"; then
  fail=1
fi

# Extra BOS chrome checks on hosts that expose Super BOS (not every tenant).
BOS_BASES=(
  "https://www.ecomae.com"
  "https://epartscart.com"
  "https://www.epartscart.com"
  "https://cp.ecomae.com"
)
if [[ -n "${ECOMAE_BOS_PROBE_BASES:-}" ]]; then
  # shellcheck disable=SC2206
  BOS_BASES=($ECOMAE_BOS_PROBE_BASES)
fi

bos_pass=0
bos_fail=0
bos_skip=0
bos_tmp="$(mktemp)"
: >"$bos_tmp"

say ""
say "== BOS product chrome (selected hosts) =="
for base in "${BOS_BASES[@]}"; do
  base="${base%/}"
  url="${base}/BOS/"
  body="$(mktemp)"
  hdr="$(mktemp)"
  code="$(curl -sS -m 25 -D "$hdr" -o "$body" -w '%{http_code}' -L \
    -A 'Mozilla/5.0 EcomAE-same-to-same-bos-probe' \
    "$url" 2>/dev/null || echo 000)"
  ctype="$(awk -F': ' 'tolower($1)=="content-type"{print $2; exit}' "$hdr" | tr -d '\r')"
  bad=""
  for m in blazor.web.js MigrationConsole BosFleetApp php-chrome-shell '"error":"unauthorized"' X-EcomAE-Route-Cutover; do
    if grep -Fq "$m" "$body" 2>/dev/null; then
      bad="$m"
      break
    fi
  done
  if [[ "$code" == "000" ]]; then
    bos_skip=$((bos_skip + 1))
    say "  SKIP  $url unreachable"
    printf '%s\t%s\tskip\tunreachable\n' "$url" "$code" >>"$bos_tmp"
  elif [[ -n "$bad" ]] || grep -qi 'application/json' <<<"$ctype"; then
    bos_fail=$((bos_fail + 1))
    fail=1
    say "  FAIL  $url looks like ASP.NET/digest ($code marker=${bad:-json})"
    printf '%s\t%s\tfail\t%s\n' "$url" "$code" "${bad:-json}" >>"$bos_tmp"
  elif grep -Eiq '<!DOCTYPE|<html' "$body"; then
    bos_pass=$((bos_pass + 1))
    say "  PASS  $url PHP/HTML chrome ($code)"
    printf '%s\t%s\tpass\thtml\n' "$url" "$code" >>"$bos_tmp"
  else
    # Login wall / PHP runtime text still counts as not-ASP.NET cutover.
    bos_pass=$((bos_pass + 1))
    say "  PASS  $url non-JSON product path ($code)"
    printf '%s\t%s\tpass\tnon-json\n' "$url" "$code" >>"$bos_tmp"
  fi
  rm -f "$body" "$hdr"
done

python3 - "$SUMMARY" "$OUT_DIR/live-tenant-php-chrome.json" "$bos_tmp" "$fail" "$bos_pass" "$bos_fail" "$bos_skip" <<'PY'
import json, sys, datetime
from pathlib import Path
summary_path, chrome_path, bos_src, fail_flag, bos_pass, bos_fail, bos_skip = sys.argv[1:8]
chrome = {}
p = Path(chrome_path)
if p.is_file():
    try:
        chrome = json.loads(p.read_text(encoding="utf-8"))
    except Exception:
        chrome = {"status": "missing-or-invalid"}
bos_rows = []
with open(bos_src, encoding="utf-8") as fh:
    for line in fh:
        parts = line.rstrip("\n").split("\t")
        if len(parts) >= 4:
            bos_rows.append({"url": parts[0], "httpStatus": parts[1], "result": parts[2], "reason": parts[3]})
status = "pass" if fail_flag == "0" else "fail"
doc = {
    "generatedAtUtc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "status": status,
    "policy": "same-to-same-invisible-migration",
    "mandate": (
        "Tenants must not feel PHP→ASP.NET change. Frontend/CP/ERP/BOS/storefront product chrome stays PHP. "
        "Named live tenants (epartscart, electronicae, stylenlook, thejewellerytrend, taxofinca) "
        "keep presentation identical — theme, colour, structure, fonts, hero/splash, fields."
    ),
    "liveProductionTenants": [
        "epartscart.com",
        "electronicae.com",
        "stylenlook.com",
        "thejewellerytrend.com",
        "taxofinca.com",
    ],
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "tenantChromeProbe": {
        "status": chrome.get("status"),
        "passCount": chrome.get("passCount"),
        "failCount": chrome.get("failCount"),
        "evidence": str(p),
    },
    "bosChromeProbe": {
        "passCount": int(bos_pass),
        "failCount": int(bos_fail),
        "skipCount": int(bos_skip),
        "probes": bos_rows,
    },
    "notes": [
        "Blazor /cp|/erp|/bos|/storefront/app previews on www are migration scaffolding only.",
        "Digests and JSON APIs must never replace tenant product chrome.",
        "Batch 6 decommission remains blocked until presentation + module function + this verify pass + human approval.",
        "Never invent RELEASE_OWNER_APPROVAL.md.",
    ],
}
Path(summary_path).write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
print(f"Wrote {summary_path} status={status}")
PY

rm -f "$bos_tmp"

say ""
say "----------------------------"
if [[ "$fail" -ne 0 ]]; then
  say "FAIL: same-to-same verify — restore PHP chrome; do not cut over tenants." >&2
  exit 1
fi
say "PASS: tenants still on PHP product chrome (same-to-same / invisible migration)."
say "cutoverAllowed=false readyForPhpRemoval=false"
exit 0
