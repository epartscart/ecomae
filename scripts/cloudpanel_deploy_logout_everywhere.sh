#!/usr/bin/env bash
# Paste-safe CloudPanel: diagnose (and optionally deploy) the HTTPS logout fix.
#
# Diagnose only (no restart, no git change):
#   ECOMAE_LOGOUT_DEPLOY=0 bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/erp-logout-everywhere-5dbd/scripts/cloudpanel_deploy_logout_everywhere.sh)"
#
# Deploy this branch, restart ASP.NET, then re-probe:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/erp-logout-everywhere-5dbd/scripts/cloudpanel_deploy_logout_everywhere.sh)"
#
# Never prints cookie values. PHP stays fallback.
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/erp-logout-everywhere-5dbd}"
ECOMAE_LOGOUT_DEPLOY="${ECOMAE_LOGOUT_DEPLOY:-1}"
REPO="${ECOMAE_REPO:-/opt/ecomae-aspnet-source}"
ASPNET_URL="${ECOMAE_ASPNET_URL:-http://127.0.0.1:5100}"

redact_headers() {
  awk '
    BEGIN { IGNORECASE = 1 }
    /^HTTP\// { print }
    /^Location:/ { print }
    /^X-EcomAE-Logout:/ { print; logout = 1 }
    /^Set-Cookie:/ {
      line = $0
      gsub(/=[^;]*/, "=***", line)
      print line
      cookies++
      if (tolower($0) ~ /secure/) secure++
      if (tolower($0) ~ /max-age=0/ || tolower($0) ~ /expires=/) expired++
      if (tolower($0) ~ /admin_session/) admin++
      if (tolower($0) ~ /\.ecomae\.com/ || tolower($0) ~ /\.epartscart\.com/) parent++
    }
    END {
      print "-- summary: set-cookie=" (cookies+0) " secure=" (secure+0) " expired=" (expired+0) " admin_session=" (admin+0) " parent-domain=" (parent+0) " x-ecomae-logout=" (logout+0)
      if ((secure+0) == 0 || (admin+0) == 0 || (logout+0) == 0) {
        print "-- RESULT: OLD logout (refresh can stay signed in). Deploy this branch."
      } else {
        print "-- RESULT: NEW logout (Secure cookies expired; refresh should stay signed out)."
      }
    }
  '
}

probe_one() {
  local host="$1" path="$2" label="$3"
  printf '\n======== %s %s %s ========\n' "$label" "$host" "$path"
  curl -sS -D - -o /dev/null \
    --max-redirs 0 \
    -H "Host: ${host}" \
    -H "X-Forwarded-Proto: https" \
    -H "Cookie: admin_session=probe-token; admin_u_id=1; session=probe-cust; u_id=2" \
    "${ASPNET_URL}${path}" 2>/dev/null | redact_headers || printf 'curl failed for %s%s\n' "$ASPNET_URL" "$path"
}

probe_all() {
  local label="$1"
  printf '\n#### Logout probe (%s) against %s ####\n' "$label" "$ASPNET_URL"
  printf 'Expected NEW: X-EcomAE-Logout: cleared, Set-Cookie admin_session=*** with Secure + expires/Max-Age, and .ecomae.com / .epartscart.com variants.\n'
  probe_one "www.ecomae.com" "/erp/logout" "$label"
  probe_one "www.ecomae.com" "/cp/logout" "$label"
  probe_one "www.epartscart.com" "/erp/logout" "$label"
  probe_one "www.epartscart.com" "/cp/logout" "$label"

  if curl -fsS -o /dev/null --max-time 8 -H "Host: www.ecomae.com" "${ASPNET_URL}/health" 2>/dev/null; then
    printf '\nhealth: %s/health OK\n' "$ASPNET_URL"
  else
    printf '\nhealth: %s/health not reachable — ASP.NET may be down or on another port.\n' "$ASPNET_URL"
  fi
}

printf '== CloudPanel logout diagnose/deploy ==\n'
printf 'Branch: %s\n' "$ECOMAE_BRANCH"
printf 'Deploy: %s\n' "$ECOMAE_LOGOUT_DEPLOY"
printf 'Repo:   %s\n' "$REPO"

probe_all "BEFORE"

if [[ "$ECOMAE_LOGOUT_DEPLOY" != "1" ]]; then
  printf '\nStopped after diagnose (ECOMAE_LOGOUT_DEPLOY=0). To deploy:\n'
  printf '  bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/%s/scripts/cloudpanel_deploy_logout_everywhere.sh)"\n' "$ECOMAE_BRANCH"
  exit 0
fi

mkdir -p "$(dirname "$REPO")"
if [[ ! -d "$REPO/.git" ]]; then
  git clone "$ECOMAE_GIT_URL" "$REPO"
fi

cd "$REPO"
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
printf 'HEAD: %s %s\n' "$(git rev-parse --short HEAD)" "$(git log -1 --pretty=%s)"

test -f scripts/cloudpanel_find_and_redeploy.sh
ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_find_and_redeploy.sh
if [[ -f scripts/wait_for_aspnet_health.sh ]]; then
  bash scripts/wait_for_aspnet_health.sh || true
fi

probe_all "AFTER"

printf '\nBrowser check after deploy:\n'
printf '  1. Open https://www.ecomae.com/erp  (signed in)\n'
printf '  2. Open https://www.ecomae.com/erp/logout\n'
printf '  3. Refresh /erp — must stay on /erp/login, not the ERP shell.\n'
printf '  Repeat on a tenant host (https://www.epartscart.com/erp/logout).\n'
printf 'PHP remains fallback. Do not enable broad nginx cutover from this script.\n'
