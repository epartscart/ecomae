#!/usr/bin/env bash
# ONE-SHOT: fix live CP/ERP/BOS login (empty 500 on /auth/login/admin, 400 on /cp/login).
# Run on CloudPanel as root. Never prints secrets.
#
#   bash scripts/cloudpanel_fix_login_bridge_now.sh
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '== Fix login bridge NOW (%s) ==\n' "$ECOMAE_BRANCH"

REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
if [[ -z "$REPO" ]]; then
  mkdir -p /opt
  git clone "$ECOMAE_GIT_URL" /opt/ecomae-aspnet-source
  REPO=/opt/ecomae-aspnet-source
fi

cd "$REPO"
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
git rev-parse --short HEAD

if ! grep -q 'LegacyLoginBridgeMiddleware' aspnet/src/EcomAE.Platform/Program.cs; then
  printf 'ERROR: LegacyLoginBridgeMiddleware missing on %s — wrong branch?\n' "$ECOMAE_BRANCH" >&2
  exit 1
fi
if ! grep -q 'LoginPostHref' aspnet/src/EcomAE.Platform/Components/Shared/LegacyAdminLoginForm.razor; then
  printf 'ERROR: LoginPostHref missing — forms still post to /auth/login/admin\n' >&2
  exit 1
fi

command -v dotnet >/dev/null || { printf 'ERROR: dotnet missing\n' >&2; exit 1; }
command -v systemctl >/dev/null || { printf 'ERROR: systemctl missing\n' >&2; exit 1; }

STAMP="$(date -u +%Y%m%d%H%M%S)"
RELEASE_DIR="$RELEASE_ROOT/releases/$STAMP"
PLATFORM_DIR="$RELEASE_DIR/platform"
WORKERS_DIR="$RELEASE_DIR/workers"
mkdir -p "$PLATFORM_DIR" "$WORKERS_DIR"

printf '== Publishing Platform + Workers (no foundation gates) ==\n'
dotnet restore aspnet/EcomAE.AspNetCore.sln
dotnet publish aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj -c Release -o "$PLATFORM_DIR"
dotnet publish aspnet/src/EcomAE.Workers/EcomAE.Workers.csproj -c Release -o "$WORKERS_DIR"
ln -sfn "$RELEASE_DIR" "$RELEASE_ROOT/current"
printf 'Current -> %s\n' "$RELEASE_DIR"

install -d /etc/systemd/system "$ENV_DIR"
install -m 0644 deploy/aspnet/ecomae-platform.service /etc/systemd/system/ecomae-platform.service
systemctl daemon-reload
systemctl enable ecomae-platform.service

# Sync PHP secret (same credentials) when PHP docroot is available
if [[ -x scripts/cloudpanel_sync_secret_succession_from_php.sh ]]; then
  ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES \
    bash scripts/cloudpanel_sync_secret_succession_from_php.sh || \
    printf 'WARN: secret sync failed — set EcomAE__SecretSuccession manually in %s/platform.env\n' "$ENV_DIR" >&2
fi

systemctl restart ecomae-platform.service
sleep 2
systemctl --no-pager --full status ecomae-platform.service || true

if [[ -x scripts/wait_for_aspnet_health.sh ]]; then
  bash scripts/wait_for_aspnet_health.sh || true
fi

printf '\n== Local probes (127.0.0.1:5100) ==\n'
curl -sS -o /dev/null -w 'health %{http_code}\n' http://127.0.0.1:5100/health || printf 'health FAIL (service down)\n'
curl -sS http://127.0.0.1:5100/cp/login | grep -oE 'action="[^"]+"' | sort -u | head
# Must NOT be action="/auth/login/admin"
if curl -sS http://127.0.0.1:5100/cp/login | grep -q 'action="/cp/login"'; then
  printf 'OK: CP form posts to /cp/login\n'
else
  printf 'WARN: CP form action not updated — wrong publish?\n' >&2
fi

printf '\n== Public probes ==\n'
curl -sS -D - -o /dev/null -X POST 'https://www.ecomae.com/cp/login' \
  -H 'Content-Type: application/x-www-form-urlencoded' -H 'Accept: text/html' \
  -d 'contact=x@y.com&password=wrong&contact_type=email&surface=cp&redirect=/cp' \
  | head -n 12 || true

printf '\nDone. Open https://www.ecomae.com/cp/login and use the same PHP admin email/password.\n'
printf 'Do NOT open /auth/login/admin in the browser — that was the old POST URL.\n'
printf 'If still failing: journalctl -u ecomae-platform.service -n 100 --no-pager\n'
