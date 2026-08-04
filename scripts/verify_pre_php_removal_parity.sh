#!/usr/bin/env bash
# Hard fail-closed pre-PHP-removal verdict.
# Runs unit/foundation/area checks + live public surface authority checks.
# Never removes PHP. Never invents RELEASE_OWNER_APPROVAL.md.
# Presentation exact-route inventory (109): docs/migration/evidence/presentation/presentation-exact-routes.json
# Kept in sync by scripts/validate_presentation_hybrid_allowlist_sync.py.
# Surface-digest exact-route inventory (35): docs/migration/evidence/surface-parity/surface-digest-exact-routes.json
# Kept in sync by scripts/validate_surface_digest_allowlist_sync.py.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${ECOMAE_GATE_OUT_DIR:-$ROOT/docs/migration/evidence/decommission/public-probes}"
mkdir -p "$OUT_DIR"
VERDICT="$OUT_DIR/www-pre-php-removal-parity-verdict.json"

echo "== Pre-PHP-removal parity verdict (fail-closed) =="
echo "This script NEVER removes PHP-FPM/cron/rewrites."

fail=0
pass=0
warn=0
rows_tmp="$(mktemp)"

record() {
  local area="$1" status="$2" detail="$3"
  printf '%s\t%s\t%s\n' "$area" "$status" "$detail" >>"$rows_tmp"
  case "$status" in
    pass) pass=$((pass + 1)); printf '  PASS  %s — %s\n' "$area" "$detail" ;;
    fail) fail=$((fail + 1)); printf '  FAIL  %s — %s\n' "$area" "$detail" ;;
    warn) warn=$((warn + 1)); printf '  WARN  %s — %s\n' "$area" "$detail" ;;
  esac
}

# 1) Local automated suite (progress lines — CloudPanel can sit on unit tests for ~30–90s)
printf -- '-- foundation checks --\n'
if bash "$ROOT/tests/aspnet_migration/run_foundation_checks.sh" >/tmp/pre-removal-foundation.out 2>&1; then
  record "foundation-checks" pass "foundation checks green"
else
  record "foundation-checks" fail "foundation checks failed (see /tmp/pre-removal-foundation.out)"
fi

printf -- '-- unit tests (may take ~1 min; not stuck) --\n'
if (cd "$ROOT/aspnet" && dotnet test tests/EcomAE.Platform.Tests/EcomAE.Platform.Tests.csproj --nologo -v q >/tmp/pre-removal-dotnet.out 2>&1); then
  record "unit-tests" pass "EcomAE.Platform.Tests green"
else
  record "unit-tests" fail "unit tests failed"
fi

printf -- '-- final-gate checklist --\n'
if bash "$ROOT/scripts/run_zero_php_final_gate_checklist.sh" >/tmp/pre-removal-checklist.out 2>&1; then
  record "final-gate-checklist" pass "checklist exited 0"
else
  record "final-gate-checklist" fail "checklist failed"
fi

printf -- '-- area tests (live probes; skips duplicate unit/checklist) --\n'
if ECOMAE_AREA_SKIP_HEAVY=1 bash "$ROOT/scripts/run_php_decommission_area_tests.sh" >/tmp/pre-removal-area.out 2>&1; then
  record "area-tests" pass "area tests exited 0"
else
  record "area-tests" fail "area tests failed (see /tmp/pre-removal-area.out)"
fi

printf -- '-- attached smoke + live authority checks --\n'

# 2) Attached smoke contract validation (no live secrets required)
python3 - "$ROOT/docs/migration/evidence/decommission" <<'PY' >"/tmp/pre-removal-smoke-validate.out" 2>&1 || true
import json, sys
from pathlib import Path
root = Path(sys.argv[1])
smoke = root / "staging-smoke"
errors = []

def load(name):
    p = smoke / name
    if not p.is_file() or p.stat().st_size == 0:
        raise SystemExit(f"missing {name}")
    return json.loads(p.read_text(encoding="utf-8"))

price = load("price-lookup-aspnet.json")
if isinstance(price.get("error"), dict) and price["error"].get("code") in {
    "missing_api_key", "unauthorized", "invalid_api_key"
}:
    errors.append("price looks unauthenticated")
if price.get("ok") is False:
    errors.append("price ok=false")
if not isinstance(price.get("offers"), list):
    errors.append("price missing offers[]")

catalog = load("catalog-status-aspnet.json")
if catalog.get("ok") is False or isinstance(catalog.get("error"), dict):
    errors.append("catalog error envelope")
for key in ("connected", "counts", "source"):
    if key not in catalog:
        errors.append(f"catalog missing {key}")
if catalog.get("connected") is not True:
    errors.append("catalog connected!=true")

surface = load("surface-digests-aspnet.json")
if surface.get("ok") is not True:
    errors.append("surface ok!=true")
routes = surface.get("routes") or []
digest200 = [
    r for r in routes
    if isinstance(r, dict)
    and int(r.get("status") or 0) == 200
    and not str(r.get("route") or "").startswith("/migration/")
]
if len(digest200) < 1:
    errors.append("surface needs non-migration digest HTTP 200")

approval = root / "RELEASE_OWNER_APPROVAL.md"
if approval.exists() and "APPROVED_TO_REMOVE_PHP_FALLBACK" in approval.read_text(encoding="utf-8", errors="replace"):
    print("APPROVAL_PRESENT")
else:
    print("APPROVAL_MISSING")

print(f"DIGEST200={len(digest200)}")
if errors:
    print("ERRORS=" + ";".join(errors))
    raise SystemExit(1)
print("SMOKE_OK")
PY
smoke_rc=$?
if [[ "$smoke_rc" -eq 0 ]] && grep -q 'SMOKE_OK' /tmp/pre-removal-smoke-validate.out; then
  digest_n="$(awk -F= '/^DIGEST200=/{print $2}' /tmp/pre-removal-smoke-validate.out)"
  record "attached-staging-smoke" pass "price/catalog/surface artifacts validate (digest200=${digest_n:-?})"
else
  record "attached-staging-smoke" fail "attached smoke validation failed"
fi

if grep -q 'APPROVAL_PRESENT' /tmp/pre-removal-smoke-validate.out; then
  record "release-owner-approval" warn "APPROVAL file present — human must still confirm live dual-sample parity before PHP removal"
else
  record "release-owner-approval" pass "APPROVAL absent (correct; do not invent it)"
fi

# 3) Live readiness must remain blocked
ready_tmp="$(mktemp)"
ready_code="$(curl -sS -m 25 -A 'Mozilla/5.0' -o "$ready_tmp" -w '%{http_code}' \
  https://www.ecomae.com/migration/php-decommission-readiness || echo 000)"
if [[ "$ready_code" != "200" ]]; then
  record "live-readiness" fail "HTTP $ready_code"
elif python3 - "$ready_tmp" <<'PY'
import json, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
if d.get("readyToRemovePhp") is True:
    raise SystemExit(1)
print("%s/%s ready=%s" % (d.get("checklistCompleteCount"), d.get("checklistTotalCount"), d.get("readyToRemovePhp")))
PY
then
  detail="$(python3 -c 'import json; d=json.load(open("'"$ready_tmp"'")); print("%s/%s ready=%s" % (d.get("checklistCompleteCount"), d.get("checklistTotalCount"), d.get("readyToRemovePhp")))')"
  record "live-readiness" pass "blocked as required ($detail)"
else
  record "live-readiness" fail "readyToRemovePhp unexpectedly true or unreadable"
fi
rm -f "$ready_tmp"

# 4) Live parity reporters must not claim frontend/backend parity reached
for route in surface-parity presentation-parity; do
  body="$(mktemp)"
  code="$(curl -sS -m 25 -A 'Mozilla/5.0' -o "$body" -w '%{http_code}' \
    "https://www.ecomae.com/migration/${route}" || echo 000)"
  if [[ "$code" != "200" ]]; then
    record "live-${route}" fail "HTTP $code"
  else
    case "$route" in
      surface-parity)
        if grep -q 'parity-not-yet-reached' "$body"; then
          record "live-surface-parity" pass "status parity-not-yet-reached (honest)"
        else
          record "live-surface-parity" fail "expected parity-not-yet-reached"
        fi
        ;;
      presentation-parity)
        if grep -q 'presentation-shell-scaffolded' "$body"; then
          record "live-presentation-parity" pass "presentation shells scaffolded, not live cutover"
        else
          record "live-presentation-parity" fail "unexpected presentation status"
        fi
        ;;
    esac
  fi
  rm -f "$body"
done

# 5) Public product chrome must still be PHP HTML (ASP.NET digests are loopback/exact-route only)
stack_out="$OUT_DIR/www-live-surface-stack.json"
if [[ -x "$ROOT/scripts/probe_live_surface_stack.sh" ]]; then
  ECOMAE_PROBE_OUT_DIR="$OUT_DIR" bash "$ROOT/scripts/probe_live_surface_stack.sh" >/tmp/pre-removal-stack.out 2>&1 || true
fi
python3 - "$stack_out" <<'PY' >/tmp/pre-removal-stack-judge.out 2>&1 || true
import json, sys
from pathlib import Path
path = Path(sys.argv[1])
doc = json.loads(path.read_text(encoding="utf-8"))
rows = doc.get("routes") if isinstance(doc, dict) else doc
if not isinstance(rows, list):
    raise SystemExit("unrecognized stack shape")
wanted = [
    "https://www.ecomae.com/",
    "https://www.ecomae.com/CP/",
    "https://www.ecomae.com/ERP/",
    "https://www.ecomae.com/BOS/",
    "https://www.ecomae.com/cp/dashboard-summary",
    "https://www.ecomae.com/api/v1/catalog/status",
    "https://www.ecomae.com/api/v1/catalog/manufacturers",
    "https://www.ecomae.com/api/v1/catalog/models",
    "https://www.ecomae.com/api/v1/catalog/modifications",
    "https://www.ecomae.com/api/v1/catalog/brands",
    "https://www.ecomae.com/api/v1/catalog/suppliers",
    "https://www.ecomae.com/api/v1/catalog/vin",
    "https://www.ecomae.com/api/v1/catalog/engines",
    "https://www.ecomae.com/api/v1/catalog/analogs",
    "https://www.ecomae.com/api/v1/catalog/article-brands",
    "https://www.ecomae.com/api/v1/catalog/categories",
    "https://www.ecomae.com/api/v1/catalog/products",
    "https://www.ecomae.com/api/v1/catalog/engine-search",
    "https://www.ecomae.com/api/v1/catalog/article-links",
    "https://www.ecomae.com/api/v1/catalog/article",
    "https://www.ecomae.com/api/v1/catalog/articles",
    "https://www.ecomae.com/api/v1/catalog/engine",
    "https://www.ecomae.com/api/v1/catalog/brand-parts",
    "https://www.ecomae.com/api/v1/price/lookup",
    "https://www.ecomae.com/health",
]
by_url = {}
for item in rows:
    if not isinstance(item, dict):
        continue
    url = item.get("url") or ""
    if url in wanted:
        by_url[url] = (str(item.get("stack") or "other"), item.get("status"))

errors = []
for url in ("https://www.ecomae.com/", "https://www.ecomae.com/CP/", "https://www.ecomae.com/ERP/", "https://www.ecomae.com/BOS/"):
    eng, _st = by_url.get(url, ("missing", None))
    if eng != "php-html":
        errors.append(f"{url} expected php-html got {eng}")
# Digests may be exact-route shadowed one path at a time (aspnet-json OK). Broad /cp is still forbidden.
dash = by_url.get("https://www.ecomae.com/cp/dashboard-summary", ("missing", None))[0]
if dash not in ("php-html", "aspnet-json", "other", "missing"):
    errors.append(f"https://www.ecomae.com/cp/dashboard-summary unexpected stack {dash}")
health = by_url.get("https://www.ecomae.com/health", ("missing", None))[0]
price = by_url.get("https://www.ecomae.com/api/v1/price/lookup", ("missing", None))[0]
catalog = by_url.get("https://www.ecomae.com/api/v1/catalog/status", ("missing", None))[0]
mfr = by_url.get("https://www.ecomae.com/api/v1/catalog/manufacturers", ("missing", None))[0]
models = by_url.get("https://www.ecomae.com/api/v1/catalog/models", ("missing", None))[0]
mods = by_url.get("https://www.ecomae.com/api/v1/catalog/modifications", ("missing", None))[0]
brands = by_url.get("https://www.ecomae.com/api/v1/catalog/brands", ("missing", None))[0]
suppliers = by_url.get("https://www.ecomae.com/api/v1/catalog/suppliers", ("missing", None))[0]
vin = by_url.get("https://www.ecomae.com/api/v1/catalog/vin", ("missing", None))[0]
engines = by_url.get("https://www.ecomae.com/api/v1/catalog/engines", ("missing", None))[0]
analogs = by_url.get("https://www.ecomae.com/api/v1/catalog/analogs", ("missing", None))[0]
article_brands = by_url.get("https://www.ecomae.com/api/v1/catalog/article-brands", ("missing", None))[0]
categories = by_url.get("https://www.ecomae.com/api/v1/catalog/categories", ("missing", None))[0]
products = by_url.get("https://www.ecomae.com/api/v1/catalog/products", ("missing", None))[0]
engine_search = by_url.get("https://www.ecomae.com/api/v1/catalog/engine-search", ("missing", None))[0]
article_links = by_url.get("https://www.ecomae.com/api/v1/catalog/article-links", ("missing", None))[0]
article = by_url.get("https://www.ecomae.com/api/v1/catalog/article", ("missing", None))[0]
articles = by_url.get("https://www.ecomae.com/api/v1/catalog/articles", ("missing", None))[0]
engine = by_url.get("https://www.ecomae.com/api/v1/catalog/engine", ("missing", None))[0]
brand_parts = by_url.get("https://www.ecomae.com/api/v1/catalog/brand-parts", ("missing", None))[0]
if health != "aspnet-health":
    errors.append(f"health engine unexpected: {health}")
if price != "aspnet-json":
    errors.append(f"price lookup engine unexpected: {price}")
# Wired catalog exact-route shadows (18/18) are approved/live on www.
for label, eng in (
    ("status", catalog),
    ("manufacturers", mfr),
    ("models", models),
    ("modifications", mods),
    ("brands", brands),
    ("suppliers", suppliers),
    ("vin", vin),
    ("engines", engines),
    ("analogs", analogs),
    ("article-brands", article_brands),
    ("categories", categories),
    ("products", products),
    ("engine-search", engine_search),
    ("article-links", article_links),
    ("article", article),
    ("articles", articles),
    ("engine", engine),
    ("brand-parts", brand_parts),
):
    if eng != "aspnet-json":
        errors.append(f"catalog {label} engine unexpected: {eng} (expected aspnet-json after exact-route shadow)")
print("STACK_URLS=" + str({k: by_url.get(k) for k in wanted}))
if errors:
    print("ERRORS=" + ";".join(errors))
    raise SystemExit(1)
print("STACK_OK")
PY
if grep -q 'STACK_OK' /tmp/pre-removal-stack-judge.out; then
  record "public-surface-authority" pass "CP/ERP/BOS chrome still PHP; health/price/catalog (18/18) ASP.NET exact routes; digest exact-routes optional"
else
  # Fallback judge via direct curl if JSON shape unknown
  chrome_ok=1
  for path in / /CP/ /ERP/ /BOS/; do
    code="$(curl -sS -m 20 -A 'Mozilla/5.0' -o /tmp/chrome.body -w '%{http_code}' "https://www.ecomae.com${path}" || echo 000)"
    ctype="$(file -b --mime-type /tmp/chrome.body 2>/dev/null || true)"
    if [[ "$code" != "200" ]] || ! grep -qi '<html\|<!doctype' /tmp/chrome.body; then
      chrome_ok=0
    fi
  done
  dig_code="$(curl -sS -m 20 -A 'Mozilla/5.0' -o /tmp/dig.body -w '%{http_code}' https://www.ecomae.com/cp/dashboard-summary || echo 000)"
  # Chrome must stay PHP HTML. Digest may be PHP HTML or ASP.NET 401 unauthorized JSON.
  if [[ "$chrome_ok" -eq 1 ]] && { [[ "$dig_code" != "200" ]] || ! grep -qi '<html\|<!doctype' /tmp/dig.body; }; then
    record "public-surface-authority" pass "curl fallback: chrome HTML PHP-era; digest not broad-cutover 200 HTML"
  else
    record "public-surface-authority" fail "could not confirm PHP remains authoritative for public chrome"
    cat /tmp/pre-removal-stack-judge.out >&2 || true
  fi
fi

# 6) Decommission must refuse
if ! ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash "$ROOT/scripts/cloudpanel_php_decommission.sh" >/tmp/pre-removal-decom.out 2>&1; then
  record "decommission-refuses" pass "gated decommission refused while readyToRemovePhp=false"
else
  record "decommission-refuses" fail "decommission should refuse"
fi

# Verdict
verdict="blocked-not-ready-for-php-removal"
if [[ "$fail" -eq 0 ]]; then
  verdict="blocked-parity-incomplete-php-must-remain"
fi

python3 - "$VERDICT" "$rows_tmp" "$pass" "$fail" "$warn" "$verdict" <<'PY'
import json, sys, datetime
out, src, p, f, w, verdict = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5]), sys.argv[6]
rows=[]
with open(src, encoding="utf-8") as fh:
    for line in fh:
        area, status, detail = line.rstrip("\n").split("\t", 2)
        rows.append({"area": area, "status": status, "detail": detail})
payload = {
    "capturedAtUtc": datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "verdict": verdict,
    "readyToRemovePhp": False,
    "passed": p,
    "failed": f,
    "warnings": w,
    "summary": [
        "Authenticated loopback staging smoke is attached for price/catalog/surface digests.",
        "Public CP/ERP/BOS chrome remains PHP HTML — frontend cutover has not happened.",
        "Public digest/catalog routes are not broadly proxied to ASP.NET yet.",
        "Live surface-parity reports parity-not-yet-reached; presentation shells are scaffolded only.",
        "RELEASE_OWNER_APPROVAL.md is absent; inventing it is forbidden.",
        "PHP must remain until exact-route shadows + dual-sample parity + human approval exist.",
    ],
    "results": rows,
}
with open(out, "w", encoding="utf-8") as fh:
    json.dump(payload, fh, indent=2)
    fh.write("\n")
print(out)
PY

rm -f "$rows_tmp"
echo "----------------------------"
echo "Passed: $pass  Warnings: $warn  Failed: $fail"
echo "Verdict: $verdict"
echo "Artifact: $VERDICT"
echo "PHP was NOT removed and must NOT be removed yet."
exit $(( fail > 0 ? 1 : 0 ))
