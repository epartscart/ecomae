#!/usr/bin/env bash
# Opt-in storefront customer-digest smoke against ASP.NET loopback.
# Does not enable nginx cutover. Does not remove PHP.
set -euo pipefail

if [[ "${RUN_STOREFRONT_DIGEST_SMOKE:-0}" != "1" ]]; then
  echo "WARN storefront digest smoke skipped; set RUN_STOREFRONT_DIGEST_SMOKE=1 with ECOMAE_ASPNET_BASE_URL and customer cookie."
  exit 0
fi

: "${ECOMAE_ASPNET_BASE_URL:?ECOMAE_ASPNET_BASE_URL is required}"
COOKIE_JAR="${ECOMAE_CUSTOMER_COOKIE_JAR:-}"
COOKIE_HEADER="${ECOMAE_CUSTOMER_COOKIE_HEADER:-}"
OUT_DIR="${ECOMAE_SMOKE_OUT_DIR:-/tmp}"
OUT_FILE="${OUT_DIR}/ecomae-aspnet-storefront-digests.json"
REQUIRE_DIGEST_200="${ECOMAE_REQUIRE_AUTHENTICATED_DIGEST_200:-1}"
mkdir -p "$OUT_DIR"

if [[ -z "$COOKIE_JAR" && -z "$COOKIE_HEADER" ]]; then
  echo "FAIL provide ECOMAE_CUSTOMER_COOKIE_JAR or ECOMAE_CUSTOMER_COOKIE_HEADER (session=...; u_id=<digits>)"
  exit 1
fi

auth_args=()
if [[ -n "$COOKIE_JAR" ]]; then
  auth_args+=(-b "$COOKIE_JAR")
else
  auth_args+=(-H "Cookie: ${COOKIE_HEADER}")
fi

routes=(
  "/storefront/account-summary"
  "/storefront/orders?limit=5"
  "/storefront/garage?limit=5"
  "/storefront/profile"
  "/storefront/search?article=0986424590&limit=5"
  "/storefront/cart?limit=5"
  "/storefront/checkout?limit=5"
)

pass=0
fail=0
digest_200=0
results_tmp="$(mktemp)"
: >"$results_tmp"

for route in "${routes[@]}"; do
  tmp="$(mktemp)"
  status="$(curl -sS -o "$tmp" -w '%{http_code}' "${auth_args[@]}" \
    "${ECOMAE_ASPNET_BASE_URL}${route}" || true)"
  bytes="$(wc -c <"$tmp" | tr -d ' ')"
  marker=""
  if [[ "$status" != "200" ]]; then
    marker="$(python3 - "$tmp" <<'PY'
import json, sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
try:
    doc = json.loads(raw)
    if isinstance(doc, dict):
        if isinstance(doc.get("error"), dict):
            print(str(doc["error"].get("code") or doc["error"])[:120])
        elif "message" in doc:
            print(str(doc.get("message"))[:120])
        else:
            print(raw[:120].replace("\n", " "))
    else:
        print(raw[:120].replace("\n", " "))
except Exception:
    print(raw[:120].replace("\n", " "))
PY
)"
  fi
  rm -f "$tmp"

  if [[ "$status" == "200" ]]; then
    echo "PASS ${route} HTTP $status"
    pass=$((pass + 1))
    digest_200=$((digest_200 + 1))
  elif [[ "$REQUIRE_DIGEST_200" != "1" && "$status" == "401" ]]; then
    echo "PASS ${route} HTTP $status (legacy allow-401 mode)"
    pass=$((pass + 1))
  else
    echo "FAIL ${route} returned HTTP $status (customer digest 200 required) marker=${marker}"
    fail=$((fail + 1))
  fi
  printf '%s\t%s\t%s\t%s\n' "$route" "$status" "$bytes" "${marker//$'\t'/ }" >>"$results_tmp"
done

python3 - "$OUT_FILE" "$results_tmp" "$digest_200" "$fail" <<'PY'
import json, sys
out, src, digest_200, fail = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
routes = []
with open(src, encoding="utf-8") as fh:
    for line in fh:
        parts = line.rstrip("\n").split("\t")
        route, status, bytes_ = parts[0], parts[1], parts[2]
        marker = parts[3] if len(parts) > 3 else ""
        entry = {"route": route, "status": int(status), "bytes": int(bytes_)}
        if marker:
            entry["errorMarker"] = marker
        routes.append(entry)
ok = fail == 0 and digest_200 > 0
payload = {
    "ok": ok,
    "surface": "storefront-smoke",
    "authenticatedDigest200Count": digest_200,
    "routes": routes,
    "note": "Optional customer-session smoke. Not required for ReadyToRemovePhp checklist; used for storefront exact-route promotion.",
}
with open(out, "w", encoding="utf-8") as fh:
    json.dump(payload, fh, indent=2)
    fh.write("\n")
PY
rm -f "$results_tmp"

cp -f "$OUT_FILE" "${OUT_DIR}/storefront-digests-aspnet.json"
echo "Artifact: ${OUT_DIR}/storefront-digests-aspnet.json"
echo "Passed: $pass  Failed: $fail  AuthenticatedDigest200: $digest_200"
if [[ "$fail" -gt 0 || "$digest_200" -lt 1 ]]; then
  exit 1
fi
exit 0
