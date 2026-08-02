#!/usr/bin/env bash
# Run on the CloudPanel server after deploy to capture Zero-PHP final-gate artifacts.
# Never removes PHP. Never enables broad nginx cutover.
set -euo pipefail

ECOMAE_ASPNET_ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
ENV_FILE="${ECOMAE_ASPNET_ENV_DIR}/platform.env"
ASPNET_BASE="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '== CloudPanel final-gate artifact capture ==\n'
printf 'ASP.NET base: %s\n' "$ASPNET_BASE"
printf 'This script never removes PHP-FPM/cron/rewrites.\n'

find_repo() {
  local candidate
  for candidate in "${CANDIDATES[@]}"; do
    if [[ -n "$candidate" && -d "$candidate/.git" && -f "$candidate/scripts/run_zero_php_final_gate_checklist.sh" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  return 1
}

REPO="$(find_repo || true)"
if [[ -z "$REPO" ]]; then
  printf 'ERROR: ecomae repo with final-gate scripts not found. Run cloudpanel_find_and_redeploy.sh first.\n' >&2
  exit 1
fi
cd "$REPO"
printf 'Repo: %s\n' "$REPO"

EVIDENCE="$REPO/docs/migration/evidence/decommission"
SMOKE_DIR="$EVIDENCE/staging-smoke"
PUBLIC_DIR="$EVIDENCE/public-probes"
mkdir -p "$SMOKE_DIR" "$PUBLIC_DIR" "$EVIDENCE/parity-samples"

# Optional: load smoke keys from the environment file without printing secrets.
if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
  printf 'Loaded env file: %s (values redacted)\n' "$ENV_FILE"
else
  printf 'WARN: %s missing; public probes only.\n' "$ENV_FILE"
fi

ECOMAE_PRICE_LOOKUP_API_KEY="${ECOMAE_PRICE_LOOKUP_API_KEY:-${PRICE_LOOKUP_API_KEY:-}}"
ECOMAE_CATALOG_API_KEY="${ECOMAE_CATALOG_API_KEY:-${CATALOG_API_KEY:-}}"

capture_json() {
  local url="$1"
  local out="$2"
  local code
  code="$(curl -sS -m 20 -o "$out" -w '%{http_code}' "$url" || true)"
  if [[ "$code" != "200" ]]; then
    printf 'WARN %s -> HTTP %s\n' "$url" "$code"
    return 1
  fi
  python3 -m json.tool "$out" >"${out}.pretty" && mv "${out}.pretty" "$out"
  printf 'OK   %s -> %s\n' "$url" "$out"
}

printf '\n-- Public / loopback diagnostics --\n'
capture_json "$ASPNET_BASE/migration/zero-php-completion" "$PUBLIC_DIR/loopback-zero-php-completion.json" || true
capture_json "$ASPNET_BASE/migration/php-decommission-readiness" "$PUBLIC_DIR/loopback-php-decommission-readiness.json" || true
capture_json "$ASPNET_BASE/migration/presentation-parity" "$PUBLIC_DIR/loopback-presentation-parity.json" || true
capture_json "$ASPNET_BASE/migration/live-surface-links" "$PUBLIC_DIR/loopback-live-surface-links.json" || true
health_code="$(curl -sS -m 20 -o "$PUBLIC_DIR/loopback-health.txt" -w '%{http_code}' "$ASPNET_BASE/health" || true)"
if [[ "$health_code" == "200" ]]; then
  printf 'OK   %s/health -> %s\n' "$ASPNET_BASE" "$PUBLIC_DIR/loopback-health.txt"
else
  printf 'WARN %s/health -> HTTP %s\n' "$ASPNET_BASE" "$health_code"
fi

if [[ -x "$REPO/scripts/probe_live_surface_stack.sh" ]]; then
  printf '\n-- Public live surface stack probe (no secrets) --\n'
  ECOMAE_PROBE_OUT_DIR="$PUBLIC_DIR" bash "$REPO/scripts/probe_live_surface_stack.sh" || true
fi

printf '\n-- Unauthenticated catalog gate sample (no API key; not a smoke substitute) --\n'
PARITY_DIR="$EVIDENCE/parity-samples"
catalog_gate_tmp="$(mktemp)"
catalog_gate_code="$(curl -sS -m 20 -o "$catalog_gate_tmp" -w '%{http_code}' "$ASPNET_BASE/api/v1/catalog/status" || true)"
if [[ "$catalog_gate_code" == "401" ]] && grep -q 'missing_api_key\|unauthorized\|invalid_api_key' "$catalog_gate_tmp" 2>/dev/null; then
  python3 - "$PARITY_DIR/catalog-status-unauthenticated-gate.json" "$catalog_gate_code" "$catalog_gate_tmp" <<'PY'
import json, sys, datetime
out, code, body_path = sys.argv[1], int(sys.argv[2]), sys.argv[3]
body = open(body_path, encoding="utf-8").read()
marker = "missing_api_key"
for candidate in ("missing_api_key", "unauthorized", "invalid_api_key"):
    if candidate in body:
        marker = candidate
        break
payload = {
    "route": "/api/v1/catalog/status",
    "legacyPhpEntry": "api/v1/catalog.php?action=status",
    "aspNetRoute": "/api/v1/catalog/status",
    "capturedAtUtc": datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "comparison": "loopback-unauthenticated",
    "aspnet": {"status": code, "contentType": "application/json", "bodyMarker": marker},
    "phpExpectation": "Public host may still return PHP HTML until exact-route shadow; loopback ASP.NET returns structured JSON auth gate.",
    "result": "aspnet-loopback-unauthenticated-gate",
    "note": "Does not authorize PHP removal. Authenticated 200 smoke still required under staging-smoke/.",
}
with open(out, "w", encoding="utf-8") as fh:
    json.dump(payload, fh, indent=2)
    fh.write("\n")
print(out)
PY
else
  printf 'WARN catalog unauthenticated gate sample skipped (HTTP %s)\n' "$catalog_gate_code"
fi
rm -f "$catalog_gate_tmp"

printf '\n-- Opt-in authenticated smoke (requires keys/cookies in env) --\n'
smoke_price=0
smoke_catalog=0
smoke_surfaces=0
if [[ -n "$ECOMAE_PRICE_LOOKUP_API_KEY" ]]; then
  export RUN_PRICE_LOOKUP_SMOKE=1
  export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
  export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
  if bash tests/live_smoke/run_price_lookup_exact_route_smoke.sh; then
    smoke_price=1
  fi
else
  printf 'SKIP price lookup smoke: set ECOMAE_PRICE_LOOKUP_API_KEY in %s or the environment.\n' "$ENV_FILE"
fi

if [[ -n "$ECOMAE_CATALOG_API_KEY" ]]; then
  export RUN_CATALOG_STATUS_SMOKE=1
  export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
  export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
  if bash tests/live_smoke/run_catalog_status_exact_route_smoke.sh; then
    smoke_catalog=1
  fi
else
  printf 'SKIP catalog status smoke: set ECOMAE_CATALOG_API_KEY in %s or the environment.\n' "$ENV_FILE"
fi

if [[ -n "${ECOMAE_ADMIN_COOKIE_HEADER:-}" || -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
  export RUN_SURFACE_DIGEST_SMOKE=1
  export ECOMAE_REQUIRE_AUTHENTICATED_DIGEST_200=1
  export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
  export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
  if bash tests/live_smoke/run_surface_digest_exact_route_smoke.sh; then
    smoke_surfaces=1
  fi
else
  printf 'SKIP surface digest smoke: set ECOMAE_ADMIN_COOKIE_HEADER or ECOMAE_ADMIN_COOKIE_JAR.\n'
fi

printf '\n-- Smoke artifact summary --\n'
for name in price-lookup-aspnet.json catalog-status-aspnet.json surface-digests-aspnet.json; do
  path="$SMOKE_DIR/$name"
  if [[ -s "$path" ]]; then
    printf 'OK   %s (%s bytes)\n' "$path" "$(wc -c <"$path" | tr -d ' ')"
  else
    printf 'MISS %s\n' "$path"
  fi
done
printf 'Captured this run: price=%s catalog=%s surfaces=%s\n' "$smoke_price" "$smoke_catalog" "$smoke_surfaces"

printf '\n-- Final gate checklist --\n'
bash scripts/run_zero_php_final_gate_checklist.sh || true

if [[ "$smoke_price" -eq 1 && "$smoke_catalog" -eq 1 && "$smoke_surfaces" -eq 1 ]]; then
  printf '\nAll three smoke artifacts written. Commit + push from the server:\n'
  printf '  bash scripts/cloudpanel_commit_final_gate_smoke.sh\n'
else
  printf '\nSmoke incomplete. Add real values to %s then re-run:\n' "$ENV_FILE"
  printf '  ECOMAE_PRICE_LOOKUP_API_KEY=epc_pricepro_...\n'
  printf '  ECOMAE_CATALOG_API_KEY=epc_catalog_...\n'
  printf '  ECOMAE_ADMIN_COOKIE_HEADER='\''admin_session=...; admin_u_id=...'\''\n'
  printf '  bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'
  printf '  bash scripts/cloudpanel_commit_final_gate_smoke.sh\n'
fi
printf 'Then open a PR and add RELEASE_OWNER_APPROVAL.md only after human approval.\n'
printf 'Do NOT remove PHP from this script.\n'
