#!/usr/bin/env bash
# Probe www /marketing/* exact-route shadows for ASP.NET presentation apps.
# Live www / must remain PHP epm-hub until dual-sample + approval.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EXAMPLE="${1:-$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf}"
BASE="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
OUT_DIR="${ECOMAE_PROBE_OUT_DIR:-$ROOT/docs/migration/evidence/presentation}"
OUT_FILE="$OUT_DIR/www-marketing-app-shadow-probe.json"
mkdir -p "$OUT_DIR"

if [[ ! -f "$EXAMPLE" ]]; then
  echo "ERROR: missing $EXAMPLE" >&2
  exit 1
fi

mapfile -t ROUTES < <(grep -E '^location = /marketing/' "$EXAMPLE" | sed -E 's/^location = ([^ {]+).*/\1/' | sort -u)
if [[ "${#ROUTES[@]}" -ne 37 ]]; then
  echo "ERROR: expected 37 /marketing/* routes in example, found ${#ROUTES[@]}" >&2
  exit 1
fi

echo "== Marketing ASP.NET shadow probe (${#ROUTES[@]} routes) =="
echo "BASE=$BASE  (live / must stay PHP epm-hub)"

TMP_ROWS="$(mktemp)"
pass=0
fail=0
blocked=0

probe_one() {
  local route="$1" body code stack result
  body="$(mktemp)"
  code="$(curl -sS -m 20 -A 'Mozilla/5.0 EcomAE-marketing-probe' -o "$body" -w '%{http_code}' "${BASE}${route}" || echo 000)"
  stack="other"
  result="fail"
  if grep -Eiq '_framework/blazor|blazor.web.js|ecomae-chrome-surface' "$body" 2>/dev/null && [[ "$code" == "200" ]]; then
    stack="aspnet"; result="pass"; pass=$((pass + 1))
  elif [[ "$code" == "404" ]] || grep -qi 'page not found' "$body" 2>/dev/null; then
    stack="php-404"; result="blocked-awaiting-shadow-install"; blocked=$((blocked + 1))
  elif grep -qi '<!DOCTYPE\|<html' "$body" 2>/dev/null; then
    stack="php-html"; result="blocked-php-html"; blocked=$((blocked + 1))
  else
    fail=$((fail + 1))
  fi
  printf '%-40s %-6s %s\n' "$route" "$code" "$result"
  printf '{"route":"%s","httpStatus":%s,"result":"%s","stack":"%s"}\n' "$route" "$code" "$result" "$stack" >>"$TMP_ROWS"
  rm -f "$body"
}

if [[ "${ECOMAE_MARKETING_PROBE_OFFLINE:-0}" == "1" ]]; then
  for route in "${ROUTES[@]}"; do
    printf '{"route":"%s","httpStatus":null,"result":"offline-inventory","stack":"unprobed"}\n' "$route" >>"$TMP_ROWS"
    blocked=$((blocked + 1))
  done
else
  for route in "${ROUTES[@]}"; do
    probe_one "$route"
  done
fi

python3 - "$OUT_FILE" "$TMP_ROWS" "$pass" "$fail" "$blocked" "${#ROUTES[@]}" <<'PY'
import json, sys, time
out, rows_path = sys.argv[1], sys.argv[2]
pass_n, fail_n, blocked_n, total = map(int, sys.argv[3:7])
rows = []
with open(rows_path, encoding="utf-8") as fh:
    for line in fh:
        line = line.strip()
        if line:
            rows.append(json.loads(line))
doc = {
    "role": "www-marketing-app-shadow-probe",
    "generatedAtUnix": int(time.time()),
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "aspNetInteractiveComplete": 0,
    "routeCount": total,
    "passed": pass_n,
    "failed": fail_n,
    "blocked": blocked_n,
    "ok": fail_n == 0,
    "phpHomeMustRemainEpmHub": True,
    "results": rows,
    "note": (
        "Exact-route /marketing/* shadows on www only. Live / stays PHP epm-hub until dual-sample + "
        "RELEASE_OWNER_APPROVAL.md. Never invent cutover."
    ),
}
open(out, "w", encoding="utf-8").write(json.dumps(doc, indent=2) + "\n")
print(json.dumps({
    "ok": doc["ok"], "passed": pass_n, "blocked": blocked_n, "failed": fail_n,
    "routes": total, "out": out, "phpRemovalAllowed": False,
}, indent=2))
PY
rm -f "$TMP_ROWS"
echo "Artifact: $OUT_FILE"
exit "$([[ "$fail" -eq 0 ]] && echo 0 || echo 1)"
