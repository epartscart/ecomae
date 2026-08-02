#!/usr/bin/env bash
set -u

BASE_URL="${ECOMAE_BASE_URL:-https://www.ecomae.com}"
RUN_LIVE="${RUN_LIVE_ECOMAE_SMOKE:-0}"
SUPER_EMAIL="${ECOMAE_SUPER_EMAIL:-${ECOMAE_SUPER_USERNAME:-}}"
SUPER_PASSWORD="${ECOMAE_SUPER_PASSWORD:-}"
SUPER_LOGIN_PATH="${ECOMAE_SUPER_LOGIN_PATH:-/login}"
CLOUDPANEL_DASHBOARD_PATH="${ECOMAE_CLOUDPANEL_DASHBOARD_PATH:-/dashboard}"
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
  local error_file
  error_file="$(mktemp)"
  status="$(curl -k -L -sS -o /dev/null -w '%{http_code}' --connect-timeout 10 --max-time 30 "$url" 2>"$error_file" || true)"
  local error_text
  error_text="$(cat "$error_file")"
  rm -f "$error_file"
  case "$status" in
    200|301|302|303|307|308|401|403)
      record_pass "$label reachable with HTTP $status"
      ;;
    000|"")
      if printf '%s' "$error_text" | grep -Fq 'CONNECT tunnel failed'; then
        record_fail "$label blocked by outbound proxy CONNECT tunnel"
      else
        record_fail "$label unreachable"
      fi
      ;;
    *)
      record_fail "$label returned unexpected HTTP $status"
      ;;
  esac
}

check_login_page() {
  local url="$BASE_URL$SUPER_LOGIN_PATH"
  local body
  local error_file
  error_file="$(mktemp)"
  body="$(curl -k -L -sS --connect-timeout 10 --max-time 30 "$url" 2>"$error_file" || true)"
  local error_text
  error_text="$(cat "$error_file")"
  rm -f "$error_file"
  if printf '%s' "$body" | tr '[:upper:]' '[:lower:]' | grep -Eq '(login|password|csrf|username|email)'; then
    record_pass "Super login page exposes an auth form marker"
  elif [ -n "$body" ]; then
    record_fail "Super login page reachable but auth markers were not found"
  elif printf '%s' "$error_text" | grep -Fq 'CONNECT tunnel failed'; then
    record_fail "Super login page blocked by outbound proxy CONNECT tunnel"
  else
    record_fail "Super login page did not return a response body"
  fi
}

check_super_auth_post() {
  if [ -z "$SUPER_EMAIL" ] || [ -z "$SUPER_PASSWORD" ]; then
    record_skip "super login POST skipped; username/password not provided via environment"
    return
  fi

  local jar
  local status
  jar="$(mktemp)"
  status="$(curl -k -L -sS -o /dev/null -w '%{http_code}' --connect-timeout 10 --max-time 30 \
    -c "$jar" -b "$jar" \
    -X POST \
    --data-urlencode "username=$SUPER_EMAIL" \
    --data-urlencode "email=$SUPER_EMAIL" \
    --data-urlencode "password=$SUPER_PASSWORD" \
    "$BASE_URL$SUPER_LOGIN_PATH" 2>/dev/null || true)"
  rm -f "$jar"

  case "$status" in
    200|201|202|204|301|302|303|307|308|401|403|422)
      record_pass "Super login POST returned controlled HTTP $status"
      ;;
    000|"")
      record_fail "Super login POST unreachable"
      ;;
    *)
      record_fail "Super login POST returned unexpected HTTP $status"
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

check_secret_present "super user login" "$SUPER_EMAIL"
check_secret_present "super user password" "$SUPER_PASSWORD"
check_secret_present "tenant user email" "$TENANT_EMAIL"
check_secret_present "tenant user password" "$TENANT_PASSWORD"

check_login_page
check_url "CloudPanel dashboard" "$BASE_URL$CLOUDPANEL_DASHBOARD_PATH"
check_super_auth_post

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
