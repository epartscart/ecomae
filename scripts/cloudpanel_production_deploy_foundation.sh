#!/usr/bin/env bash
# Paste-safe CloudPanel / production-server deploy for ASP.NET Core foundation.
# Run ON the production server as root (or with sudo).
# This publishes Platform + Workers, optionally starts systemd, and keeps PHP fallback.
# It does NOT enable broad /api /cp /erp /bos /storefront cutover.
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
ECOMAE_REPO_CANDIDATES=("${ECOMAE_REPO:-}" /root/ecomae /opt/ecomae-aspnet-source /opt/ecomae)
ECOMAE_ASPNET_RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
ECOMAE_ASPNET_ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
ECOMAE_RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-1}"
ECOMAE_RUN_NGINX_RELOAD="${ECOMAE_RUN_NGINX_RELOAD:-0}"
ECOMAE_INSTALL_DIAGNOSTICS_NGINX="${ECOMAE_INSTALL_DIAGNOSTICS_NGINX:-0}"
ECOMAE_ENABLE_PRICE_LOOKUP_SHADOW="${ECOMAE_ENABLE_PRICE_LOOKUP_SHADOW:-0}"

resolve_repo() {
  for candidate in "${ECOMAE_REPO_CANDIDATES[@]}"; do
    if [[ -n "$candidate" && -f "$candidate/scripts/deploy_aspnet_foundation.sh" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  local found
  found="$(find /var/www /opt /root -maxdepth 5 -type f -path '*/scripts/deploy_aspnet_foundation.sh' -print -quit 2>/dev/null || true)"
  if [[ -n "$found" ]]; then
    printf '%s\n' "${found%/scripts/deploy_aspnet_foundation.sh}"
    return 0
  fi
  return 1
}

printf '== EcomAE CloudPanel production foundation deploy ==\n'
printf 'Branch: %s\n' "$ECOMAE_BRANCH"
printf 'Release root: %s\n' "$ECOMAE_ASPNET_RELEASE_ROOT"
printf 'Systemd: %s\n' "$ECOMAE_RUN_SYSTEMD"
printf 'Nginx reload: %s\n' "$ECOMAE_RUN_NGINX_RELOAD"
printf 'Diagnostics nginx install: %s\n' "$ECOMAE_INSTALL_DIAGNOSTICS_NGINX"
printf 'Price-lookup shadow enable: %s\n' "$ECOMAE_ENABLE_PRICE_LOOKUP_SHADOW"
printf 'Broad cutover: DISABLED (PHP remains fallback)\n'

if ! command -v git >/dev/null 2>&1; then
  printf 'git is required.\n' >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  printf 'dotnet SDK is required on the server before deploy.\n' >&2
  exit 1
fi

REPO="$(resolve_repo || true)"
if [[ -z "${REPO:-}" ]]; then
  printf 'Repository not found. Cloning %s into /root/ecomae\n' "$ECOMAE_GIT_URL"
  git clone "$ECOMAE_GIT_URL" /root/ecomae
  REPO=/root/ecomae
fi

cd "$REPO"
pwd
git fetch origin "$ECOMAE_BRANCH"
git checkout "$ECOMAE_BRANCH"
git pull --ff-only origin "$ECOMAE_BRANCH"
git status --short
git rev-parse --short HEAD

mkdir -p "$ECOMAE_ASPNET_ENV_DIR" "$ECOMAE_ASPNET_RELEASE_ROOT/releases"
if [[ ! -f "$ECOMAE_ASPNET_ENV_DIR/platform.env" ]]; then
  install -m 0600 "$REPO/deploy/aspnet/platform.env.example" "$ECOMAE_ASPNET_ENV_DIR/platform.env"
  printf 'Created %s/platform.env from example. Edit DB credentials before traffic tests.\n' "$ECOMAE_ASPNET_ENV_DIR"
  printf 'Refusing to start systemd until ConnectionStrings__TenantRegistry is filled.\n'
  if grep -Eq 'User=<db_user>|Password=<db_password>|Database=ecomae;User=<db_user>' "$ECOMAE_ASPNET_ENV_DIR/platform.env"; then
    ECOMAE_RUN_SYSTEMD=0
  fi
fi

bash "$REPO/tests/aspnet_migration/run_foundation_checks.sh"
bash "$REPO/scripts/verify_aspnet_proxy_guardrails.sh"
bash "$REPO/scripts/preflight_aspnet_production.sh"

ECOMAE_ASPNET_RELEASE_ROOT="$ECOMAE_ASPNET_RELEASE_ROOT" \
ECOMAE_ASPNET_ENV_DIR="$ECOMAE_ASPNET_ENV_DIR" \
ECOMAE_RUN_SYSTEMD="$ECOMAE_RUN_SYSTEMD" \
ECOMAE_RUN_NGINX_RELOAD=0 \
bash "$REPO/scripts/deploy_aspnet_foundation.sh"

if [[ "$ECOMAE_INSTALL_DIAGNOSTICS_NGINX" == "1" ]]; then
  install -m 0644 "$REPO/deploy/aspnet/nginx-diagnostics-only.conf" /etc/nginx/conf.d/ecomae-aspnet-diagnostics.conf
  nginx -t
  systemctl reload nginx
  printf 'Installed diagnostics-only nginx include.\n'
fi

if [[ "$ECOMAE_ENABLE_PRICE_LOOKUP_SHADOW" == "1" ]]; then
  printf 'ERROR: refusing automatic price-lookup shadow enable.\n' >&2
  printf 'Run staging smoke with ECOMAE_PRICE_LOOKUP_API_KEY first, then copy\n' >&2
  printf 'deploy/aspnet/nginx-price-lookup-shadow-example.conf manually.\n' >&2
  exit 2
fi

if [[ "$ECOMAE_RUN_NGINX_RELOAD" == "1" ]]; then
  nginx -t
  systemctl reload nginx
fi

printf '\n== Deploy complete (foundation only) ==\n'
printf 'Health check: curl -i http://127.0.0.1:5100/health\n'
printf 'PHP remains authoritative for CP/ERP/BOS/API/storefront.\n'
printf 'Do not remove PHP. Do not proxy broad /api.\n'
printf 'Next: configure ConnectionStrings__TenantRegistry, run exact-route smoke for /api/v1/price/lookup.\n'
