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
  # shellcheck disable=SC1091
  source "$REPO/scripts/cloudpanel_repair_smoke_cookie_env.sh"
  printf 'Loaded env file: %s (values redacted)\n' "$ENV_FILE"
else
  printf 'WARN: %s missing; public probes only.\n' "$ENV_FILE"
fi

ECOMAE_PRICE_LOOKUP_API_KEY="${ECOMAE_PRICE_LOOKUP_API_KEY:-${PRICE_LOOKUP_API_KEY:-}}"
ECOMAE_CATALOG_API_KEY="${ECOMAE_CATALOG_API_KEY:-${CATALOG_API_KEY:-}}"

printf '\n-- Smoke env preflight (values redacted) --\n'
bash "$REPO/scripts/cloudpanel_validate_final_gate_env.sh" || true

# Fail fast when secrets were never filled (avoids long public-probe loops that still skip smoke).
if [[ -z "${ECOMAE_PRICE_LOOKUP_API_KEY:-}" || -z "${ECOMAE_CATALOG_API_KEY:-}" || ( -z "${ECOMAE_ADMIN_COOKIE_HEADER:-}" && -z "${ECOMAE_ADMIN_COOKIE_JAR:-}" ) ]]; then
  if [[ "${ECOMAE_ALLOW_PUBLIC_ONLY_CAPTURE:-0}" != "1" ]]; then
    printf '\nBLOCKED: smoke secrets still MISSING in %s\n' "$ENV_FILE"
    printf 'Preferred path on this server (does not print secrets):\n'
    printf '  bash scripts/cloudpanel_diagnose_smoke_db.sh\n'
    printf '  ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES bash scripts/cloudpanel_apply_epc_api_clients_ddl.sh\n'
    printf '  # or: ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES bash scripts/cloudpanel_align_tenant_registry_to_php_db.sh\n'
    printf '  ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n'
    printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
    printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
    printf 'If that reports no admin session: log into https://www.ecomae.com/CP/ once, then re-run issue.\n'
    printf 'Or: bash scripts/cloudpanel_prepare_smoke_secrets.sh\n'
    printf 'Then:\n'
    printf '  source %s\n' "$ENV_FILE"
    printf '  bash scripts/cloudpanel_validate_final_gate_env.sh\n'
    printf '  bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'
    printf 'Or set ECOMAE_ALLOW_PUBLIC_ONLY_CAPTURE=1 to capture public probes only.\n'
    exit 2
  fi
  printf 'WARN: continuing public-only capture (ECOMAE_ALLOW_PUBLIC_ONLY_CAPTURE=1); authenticated smoke will SKIP.\n'
fi

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

printf '\n-- Wait for ASP.NET loopback health --\n'
health_ready=1
if ! ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE" bash "$REPO/scripts/wait_for_aspnet_health.sh"; then
  health_ready=0
  printf 'ERROR ASP.NET loopback not ready; aborting authenticated smoke (public probes still run).\n'
  printf 'Fix: systemctl status ecomae-platform.service --no-pager\n'
  printf '     journalctl -u ecomae-platform.service -n 80 --no-pager\n'
  printf '     bash scripts/cloudpanel_find_and_redeploy.sh\n'
  ECOMAE_PRICE_LOOKUP_API_KEY=""
  ECOMAE_CATALOG_API_KEY=""
  ECOMAE_ADMIN_COOKIE_HEADER=""
  ECOMAE_ADMIN_COOKIE_JAR=""
fi

printf '\n-- Public / loopback diagnostics --\n'
health_code="$(curl -sS -m 20 -o "$PUBLIC_DIR/loopback-health.txt" -w '%{http_code}' "$ASPNET_BASE/health" || true)"
if [[ "$health_code" == "200" ]]; then
  printf 'OK   %s/health -> %s\n' "$ASPNET_BASE" "$PUBLIC_DIR/loopback-health.txt"
else
  printf 'WARN %s/health -> HTTP %s\n' "$ASPNET_BASE" "$health_code"
fi

capture_json "$ASPNET_BASE/migration/zero-php-completion" "$PUBLIC_DIR/loopback-zero-php-completion.json" || true
capture_json "$ASPNET_BASE/migration/php-decommission-readiness" "$PUBLIC_DIR/loopback-php-decommission-readiness.json" || true
capture_json "$ASPNET_BASE/migration/presentation-parity" "$PUBLIC_DIR/loopback-presentation-parity.json" || true
capture_json "$ASPNET_BASE/migration/live-surface-links" "$PUBLIC_DIR/loopback-live-surface-links.json" || true
capture_json "$ASPNET_BASE/migration/surface-field-parity" "$PUBLIC_DIR/loopback-surface-field-parity.json" || true

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
# Do not leave failed/stale smoke JSON that the checklist would mis-count as attached.
rm -f \
  "$SMOKE_DIR/price-lookup-aspnet.json" \
  "$SMOKE_DIR/ecomae-aspnet-price-lookup.json" \
  "$SMOKE_DIR/catalog-status-aspnet.json" \
  "$SMOKE_DIR/ecomae-aspnet-catalog-status.json" \
  "$SMOKE_DIR/surface-digests-aspnet.json" \
  "$SMOKE_DIR/ecomae-aspnet-surface-digests.json"

if [[ "$health_ready" -ne 1 ]]; then
  printf 'SKIP all authenticated smoke: loopback ASP.NET health not ready.\n'
elif [[ -n "$ECOMAE_PRICE_LOOKUP_API_KEY" ]]; then
  if [[ "$ECOMAE_PRICE_LOOKUP_API_KEY" != epc_pricepro_* ]]; then
    printf 'SKIP price lookup smoke: ECOMAE_PRICE_LOOKUP_API_KEY BAD_FORMAT (expect epc_pricepro_ prefix).\n'
  else
    export RUN_PRICE_LOOKUP_SMOKE=1
    export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
    export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
    if bash tests/live_smoke/run_price_lookup_exact_route_smoke.sh; then
      smoke_price=1
    else
      rm -f "$SMOKE_DIR/price-lookup-aspnet.json" "$SMOKE_DIR/ecomae-aspnet-price-lookup.json"
    fi
  fi
else
  printf 'SKIP price lookup smoke: set ECOMAE_PRICE_LOOKUP_API_KEY in %s or the environment.\n' "$ENV_FILE"
fi

if [[ "$health_ready" -eq 1 && -n "$ECOMAE_CATALOG_API_KEY" ]]; then
  if [[ "$ECOMAE_CATALOG_API_KEY" != epc_catalog_* ]]; then
    printf 'SKIP catalog status smoke: ECOMAE_CATALOG_API_KEY BAD_FORMAT (expect epc_catalog_ prefix).\n'
  else
    export RUN_CATALOG_STATUS_SMOKE=1
    export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
    export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
    if bash tests/live_smoke/run_catalog_status_exact_route_smoke.sh; then
      smoke_catalog=1
    else
      rm -f "$SMOKE_DIR/catalog-status-aspnet.json" "$SMOKE_DIR/ecomae-aspnet-catalog-status.json"
    fi
  fi
elif [[ "$health_ready" -eq 1 ]]; then
  printf 'SKIP catalog status smoke: set ECOMAE_CATALOG_API_KEY in %s or the environment.\n' "$ENV_FILE"
fi

if [[ "$health_ready" -eq 1 && ( -n "${ECOMAE_ADMIN_COOKIE_HEADER:-}" || -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ) ]]; then
  printf '%s\n' '-- Admin session preflight /auth/session/probe --'
  probe_tmp="$(mktemp)"
  auth_args=()
  if [[ -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
    auth_args+=(-b "$ECOMAE_ADMIN_COOKIE_JAR")
  else
    auth_args+=(-H "Cookie: ${ECOMAE_ADMIN_COOKIE_HEADER}")
  fi
  probe_code="$(curl -sS -m 20 -o "$probe_tmp" -w '%{http_code}' "${auth_args[@]}" \
    "$ASPNET_BASE/auth/session/probe" || true)"
  probe_ok=0
  if [[ "$probe_code" == "200" ]]; then
    if python3 - "$probe_tmp" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
kind = doc.get("Kind") if doc.get("Kind") is not None else doc.get("kind")
auth = doc.get("IsAuthenticated")
if auth is None:
    auth = doc.get("isAuthenticated")
backend = doc.get("has_backend_access")
print(f"kind={kind!r} isAuthenticated={auth!r} has_backend_access={backend!r}")
# JSON may serialize LegacySessionKind as string "Admin" or int 2.
is_admin = kind in ("Admin", 2) or str(kind) == "2"
if not is_admin or auth is False:
    raise SystemExit(1)
PY
    then
      probe_ok=1
      printf 'OK   admin session probe\n'
    else
      printf 'FAIL admin session probe: not an authenticated Admin session (HTTP %s)\n' "$probe_code"
      python3 -c 'import json,sys; d=json.load(open(sys.argv[1],encoding="utf-8")); print({k:d.get(k) for k in ("Kind","kind","IsAuthenticated","isAuthenticated","has_backend_access","UserId","userId")})' "$probe_tmp" || true
      printf '%s\n' 'HINT: log into Super CP, DevTools → copy Request Cookie with admin_session=... and admin_u_id=<digits>, set ECOMAE_ADMIN_COOKIE_HEADER (no quotes inside the value), re-run.'
    fi
  else
    printf 'FAIL admin session probe HTTP %s\n' "$probe_code"
    printf '%s\n' 'HINT: cookie missing/expired, or TenantRegistry DB cannot validate admin sessions.'
  fi
  rm -f "$probe_tmp"

  if [[ "$probe_ok" -eq 1 ]]; then
    export RUN_SURFACE_DIGEST_SMOKE=1
    export ECOMAE_REQUIRE_AUTHENTICATED_DIGEST_200=1
    export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
    export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
    if bash tests/live_smoke/run_surface_digest_exact_route_smoke.sh; then
      smoke_surfaces=1
    else
      rm -f "$SMOKE_DIR/surface-digests-aspnet.json" "$SMOKE_DIR/ecomae-aspnet-surface-digests.json"
    fi
  else
    printf 'SKIP surface digest smoke: fix ECOMAE_ADMIN_COOKIE_HEADER/JAR so /auth/session/probe returns Kind=Admin.\n'
  fi
elif [[ "$health_ready" -eq 1 ]]; then
  printf 'SKIP surface digest smoke: set ECOMAE_ADMIN_COOKIE_HEADER or ECOMAE_ADMIN_COOKIE_JAR.\n'
fi

# Optional storefront customer digests (promotion aid; not required for ReadyToRemovePhp).
smoke_storefront=0
if [[ "$health_ready" -eq 1 && ( -n "${ECOMAE_CUSTOMER_COOKIE_HEADER:-}" || -n "${ECOMAE_CUSTOMER_COOKIE_JAR:-}" ) ]]; then
  printf '\n-- Optional storefront customer digest smoke --\n'
  export RUN_STOREFRONT_DIGEST_SMOKE=1
  export ECOMAE_REQUIRE_AUTHENTICATED_DIGEST_200=1
  export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
  export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
  if bash tests/live_smoke/run_storefront_digest_exact_route_smoke.sh; then
    smoke_storefront=1
  else
    rm -f "$SMOKE_DIR/storefront-digests-aspnet.json" "$SMOKE_DIR/ecomae-aspnet-storefront-digests.json"
    printf 'WARN storefront digest smoke failed (optional — admin/surface smoke can still proceed)\n'
  fi
elif [[ "$health_ready" -eq 1 ]]; then
  printf 'SKIP storefront digest smoke: set ECOMAE_CUSTOMER_COOKIE_HEADER or ECOMAE_CUSTOMER_COOKIE_JAR (session=...; u_id=...).\n'
fi

printf '\n-- Smoke artifact summary --\n'
for name in price-lookup-aspnet.json catalog-status-aspnet.json surface-digests-aspnet.json storefront-digests-aspnet.json; do
  path="$SMOKE_DIR/$name"
  if [[ -s "$path" ]]; then
    printf 'OK   %s (%s bytes)\n' "$path" "$(wc -c <"$path" | tr -d ' ')"
  else
    printf 'MISS %s\n' "$path"
  fi
done
printf 'Captured this run: price=%s catalog=%s surfaces=%s storefront=%s\n' \
  "$smoke_price" "$smoke_catalog" "$smoke_surfaces" "$smoke_storefront"

# Prevent capture's RUN_* exports from re-running failing live smoke inside the checklist.
unset RUN_PRICE_LOOKUP_SMOKE RUN_CATALOG_STATUS_SMOKE RUN_SURFACE_DIGEST_SMOKE RUN_STOREFRONT_DIGEST_SMOKE || true

printf '\n-- Final gate checklist --\n'
bash scripts/run_zero_php_final_gate_checklist.sh || true

if [[ "$smoke_price" -eq 1 && "$smoke_catalog" -eq 1 && "$smoke_surfaces" -eq 1 ]]; then
  printf '\nAll three smoke artifacts written. Commit + push from the server:\n'
  printf '  bash scripts/cloudpanel_commit_final_gate_smoke.sh\n'
else
  printf '\nSmoke incomplete — preferred CloudPanel recovery:\n'
  printf '  1) Wait for health: bash scripts/wait_for_aspnet_health.sh\n'
  printf '  2) Ensure table + issue keys/cookie (never invent secrets):\n'
  printf '       bash scripts/cloudpanel_diagnose_smoke_db.sh\n'
  printf '       ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES bash scripts/cloudpanel_apply_epc_api_clients_ddl.sh\n'
  printf '       # or align: ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES bash scripts/cloudpanel_align_tenant_registry_to_php_db.sh\n'
  printf '       ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n'
  printf '       ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
  printf '         bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
  printf '       (or bash scripts/cloudpanel_prepare_smoke_secrets.sh for manual guidance)\n'
  printf '  3) If admin cookie still missing/401: login https://www.ecomae.com/CP/ then re-issue; probe:\n'
  printf '       source %s\n' "$ENV_FILE"
  printf '       curl -sS -H "Cookie: \$ECOMAE_ADMIN_COOKIE_HEADER" http://127.0.0.1:5100/auth/session/probe; echo\n'
  printf '  4) Re-run:\n'
  printf '       bash scripts/cloudpanel_validate_final_gate_env.sh\n'
  printf '       bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'
  printf '       bash scripts/cloudpanel_commit_final_gate_smoke.sh\n'
fi
printf 'Then open a PR and add RELEASE_OWNER_APPROVAL.md only after human approval.\n'
printf 'Do NOT remove PHP from this script.\n'
