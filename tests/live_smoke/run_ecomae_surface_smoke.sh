#!/usr/bin/env bash
set -u

BASE_URL="${ECOMAE_BASE_URL:-https://www.ecomae.com}"
RUN_LIVE="${RUN_LIVE_ECOMAE_SMOKE:-0}"
SUPER_EMAIL="${ECOMAE_SUPER_EMAIL:-}"
SUPER_PASSWORD="${ECOMAE_SUPER_PASSWORD:-}"
TENANT_EMAIL="${ECOMAE_TENANT_EMAIL:-}"
TENANT_PASSWORD="${ECOMAE_TENANT_PASSWORD:-}"
TENANT_BASE_URL="${ECOMAE_TENANT_BASE_URL:-}"

pass=0
fail=0
skip=0

record_pass() { pass=$((pass + 1)); printf '  PASS  %s\n' "$1"; }
record_fail() { fail=$((fail + 1)); printf '  FAIL  %s\n' "$1"; }
record_skip() { skip=$((skip + 1)); printf '  SKIP  %s\n' "$1"; }

check_secret_present() {
  local label="$1"
  local value="$2"
  if [ -n "$value" ]; then
    record_pass "$label provided via environment"
  else
    record_skip "$label not provided; live auth post disabled"
  fi
}

check_url() {
  local label="$1"
  local url="$2"
  local status
  status="$(curl -k -L -sS -o /dev/null -w '%{http_code}' --connect-timeout 10 --max-time 30 "$url" 2>/dev/null || true)"
  case "$status" in
    200|301|302|303|307|308|401|403)
      record_pass "$label reachable with HTTP $status"
      ;;
    000|"")
      record_fail "$label unreachable"
      ;;
    *)
      record_fail "$label returned unexpected HTTP $status"
      ;;
  esac
}

echo "== EcomAE live surface smoke =="
echo "Base URL: $BASE_URL"
echo "Secrets: redacted; this script never prints passwords."

if [ "$RUN_LIVE" != "1" ]; then
  record_skip "set RUN_LIVE_ECOMAE_SMOKE=1 to enable live network checks"
  echo "----------------------------"
  echo "Passed: $pass  Skipped: $skip  Failed: $fail"
  exit 0
fi

check_secret_present "super user email" "$SUPER_EMAIL"
check_secret_present "super user password" "$SUPER_PASSWORD"
check_secret_present "tenant user email" "$TENANT_EMAIL"
check_secret_present "tenant user password" "$TENANT_PASSWORD"

check_url "Super CP lowercase" "$BASE_URL/cp"
check_url "Super CP trailing slash" "$BASE_URL/cp/"
check_url "Super ERP lowercase" "$BASE_URL/erp"
check_url "Super BOS lowercase" "$BASE_URL/bos"
check_url "Super BOS trailing slash" "$BASE_URL/bos/"

if [ -n "$TENANT_BASE_URL" ]; then
  check_url "Tenant CP" "$TENANT_BASE_URL/cp"
  check_url "Tenant ERP" "$TENANT_BASE_URL/erp"
else
  record_skip "ECOMAE_TENANT_BASE_URL not provided; tenant URL checks skipped"
fi

echo "----------------------------"
echo "Passed: $pass  Skipped: $skip  Failed: $fail"
exit $(( fail > 0 ? 1 : 0 ))
