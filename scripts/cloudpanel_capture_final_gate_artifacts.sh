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
health_code="$(curl -sS -m 20 -o "$PUBLIC_DIR/loopback-health.txt" -w '%{http_code}' "$ASPNET_BASE/health" || true)"
if [[ "$health_code" == "200" ]]; then
  printf 'OK   %s/health -> %s\n' "$ASPNET_BASE" "$PUBLIC_DIR/loopback-health.txt"
else
  printf 'WARN %s/health -> HTTP %s\n' "$ASPNET_BASE" "$health_code"
fi

printf '\n-- Opt-in authenticated smoke (requires keys/cookies in env) --\n'
if [[ -n "$ECOMAE_PRICE_LOOKUP_API_KEY" ]]; then
  export RUN_PRICE_LOOKUP_SMOKE=1
  export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
  export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
  bash tests/live_smoke/run_price_lookup_exact_route_smoke.sh
else
  printf 'SKIP price lookup smoke: set ECOMAE_PRICE_LOOKUP_API_KEY in %s or the environment.\n' "$ENV_FILE"
fi

if [[ -n "$ECOMAE_CATALOG_API_KEY" ]]; then
  export RUN_CATALOG_STATUS_SMOKE=1
  export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
  export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
  bash tests/live_smoke/run_catalog_status_exact_route_smoke.sh
else
  printf 'SKIP catalog status smoke: set ECOMAE_CATALOG_API_KEY in %s or the environment.\n' "$ENV_FILE"
fi

if [[ -n "${ECOMAE_ADMIN_COOKIE_HEADER:-}" || -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
  export RUN_SURFACE_DIGEST_SMOKE=1
  export ECOMAE_ASPNET_BASE_URL="$ASPNET_BASE"
  export ECOMAE_SMOKE_OUT_DIR="$SMOKE_DIR"
  bash tests/live_smoke/run_surface_digest_exact_route_smoke.sh
else
  printf 'SKIP surface digest smoke: set ECOMAE_ADMIN_COOKIE_HEADER or ECOMAE_ADMIN_COOKIE_JAR.\n'
fi

printf '\n-- Final gate checklist --\n'
bash scripts/run_zero_php_final_gate_checklist.sh || true

printf '\nNext (on server) if smoke artifacts were written:\n'
printf '  cd %s\n' "$REPO"
printf '  git checkout -b cursor/final-gate-artifacts-7b3b\n'
printf '  git add docs/migration/evidence/decommission\n'
printf '  git commit -m "Attach Zero-PHP final-gate staging smoke artifacts"\n'
printf '  git push -u origin HEAD\n'
printf 'Then open a PR and add RELEASE_OWNER_APPROVAL.md only after human approval.\n'
printf 'Do NOT remove PHP from this script.\n'
