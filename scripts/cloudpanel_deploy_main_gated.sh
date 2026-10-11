#!/usr/bin/env bash
# CloudPanel paste AFTER chrome isolation is merged to main.
# Publishes origin/main with foundation/proxy/preflight gates on and real
# /health + /ready (MapHealthChecks). Does not cut over nginx. Does not
# delete PHP. Does not print secrets.
#
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_deploy_main_gated.sh)"
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

BRANCH="${ECOMAE_BRANCH:-main}"
if [[ "$BRANCH" != "main" ]]; then
  printf 'ERROR: gated deploy is main only (got %s). Merge the PR, then deploy main.\n' "$BRANCH" >&2
  exit 2
fi

GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
REPO="${ECOMAE_REPO:-/opt/ecomae-aspnet-source}"

printf '======== MAIN GATED DEPLOY ========\n'
printf 'HOST=%s DATE_UTC=%s\n' "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf 'BRANCH=%s REPO=%s\n' "$BRANCH" "$REPO"
printf 'ECOMAE_EMERGENCY_PUBLISH=0 — foundation/proxy/preflight + unit tests run.\n'
printf 'PHP remains fallback. readyForPhpRemoval stays false.\n'

mkdir -p "$(dirname "$REPO")"
if [[ ! -d "$REPO/.git" ]]; then
  git clone "$GIT_URL" "$REPO"
fi
cd "$REPO"
git remote set-url origin "$GIT_URL" || true
git fetch origin "$BRANCH"
git checkout -f "$BRANCH"
git reset --hard "origin/$BRANCH"
printf 'HEAD=%s %s\n' "$(git rev-parse --short HEAD)" "$(git log -1 --pretty=%s)"

if [[ ! -f aspnet/src/EcomAE.Platform/Presentation/SurfaceChromePolicy.cs ]]; then
  printf 'ERROR: SurfaceChromePolicy is not on %s. Merge https://github.com/epartscart/ecomae/pull/2156 first.\n' "$BRANCH" >&2
  exit 2
fi
if grep -q 'ProbeBypassMiddleware' aspnet/src/EcomAE.Platform/Program.cs; then
  printf 'ERROR: ProbeBypassMiddleware is still registered — that disables the health gate.\n' >&2
  exit 2
fi
grep -q 'ShowErpSwitcher' aspnet/src/EcomAE.Platform/Presentation/SurfaceChromePolicy.cs
grep -q 'industry.ecomae.com' aspnet/src/EcomAE.Platform/Presentation/EcomaeIndustryShowcaseSnapshots.cs
printf 'OK source markers (isolation on main, health gate enabled)\n'

test -f scripts/cloudpanel_find_and_redeploy.sh
test -f scripts/wait_for_aspnet_health.sh

printf '\n---- publish ASP.NET (main, gated) ----\n'
ECOMAE_BRANCH="$BRANCH" \
ECOMAE_REPO="$REPO" \
ECOMAE_EMERGENCY_PUBLISH=0 \
  bash scripts/cloudpanel_find_and_redeploy.sh

if [[ -f scripts/cloudpanel_sync_secret_succession_from_php.sh ]]; then
  ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES \
  ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
    bash scripts/cloudpanel_sync_secret_succession_from_php.sh
fi

# Real gate: fail the paste if /health or /ready is not 200.
export ECOMAE_HEALTH_WAIT_SECONDS="${ECOMAE_HEALTH_WAIT_SECONDS:-180}"
export ECOMAE_HEALTH_POLL_SECONDS="${ECOMAE_HEALTH_POLL_SECONDS:-3}"
bash scripts/wait_for_aspnet_health.sh

printf '\nRESULT=OK main gated deploy (health gate on)\n'
printf 'Hard-refresh Super CP: https://www.ecomae.com/cp/app\n'
printf 'PHP remains fallback. This script does not cut over nginx.\n'
