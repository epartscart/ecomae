#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
SERVICE_ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-0}"
RUN_NGINX_RELOAD="${ECOMAE_RUN_NGINX_RELOAD:-0}"
DOTNET_CONFIGURATION="${DOTNET_CONFIGURATION:-Release}"
PLATFORM_PORT="${ECOMAE_ASPNET_PORT:-5100}"

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        printf 'Missing required command: %s\n' "$1" >&2
        exit 1
    fi
}

require_command dotnet
require_command install

printf '== EcomAE ASP.NET Core foundation deploy ==\n'
printf 'Repo: %s\n' "$ROOT"
printf 'Release root: %s\n' "$RELEASE_ROOT"
printf 'Systemd actions: %s\n' "$RUN_SYSTEMD"
printf 'Nginx reload: %s\n' "$RUN_NGINX_RELOAD"

"$ROOT/tests/aspnet_migration/run_detailed_foundation_tests.sh"

dotnet restore "$ROOT/aspnet/EcomAE.AspNetCore.sln"
dotnet test "$ROOT/aspnet/tests/EcomAE.Platform.Tests"

STAMP="$(date -u +%Y%m%d%H%M%S)"
RELEASE_DIR="$RELEASE_ROOT/releases/$STAMP"
PLATFORM_DIR="$RELEASE_DIR/platform"
WORKERS_DIR="$RELEASE_DIR/workers"

install -d "$PLATFORM_DIR" "$WORKERS_DIR" "$RELEASE_ROOT/releases"

dotnet publish "$ROOT/aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj" -c "$DOTNET_CONFIGURATION" -o "$PLATFORM_DIR"
dotnet publish "$ROOT/aspnet/src/EcomAE.Workers/EcomAE.Workers.csproj" -c "$DOTNET_CONFIGURATION" -o "$WORKERS_DIR"

# Pack Zero-PHP final-gate evidence/ops files into the platform ContentRoot so
# /migration/php-decommission-readiness can see attached git artifacts on the server.
# Gate checklist exact-route-shadows-only requires all four shadow examples below.
install -d \
  "$PLATFORM_DIR/docs/migration" \
  "$PLATFORM_DIR/docs/migration/evidence" \
  "$PLATFORM_DIR/deploy/aspnet" \
  "$PLATFORM_DIR/scripts"
cp -a "$ROOT/docs/migration/evidence/decommission" "$PLATFORM_DIR/docs/migration/evidence/"
install -m 0644 "$ROOT/docs/migration/PHP_DECOMMISSION_READINESS.md" "$PLATFORM_DIR/docs/migration/PHP_DECOMMISSION_READINESS.md"
install -m 0644 \
  "$ROOT/deploy/aspnet/nginx-price-lookup-shadow-example.conf" \
  "$ROOT/deploy/aspnet/nginx-api-shadow-example.conf" \
  "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" \
  "$ROOT/deploy/aspnet/nginx-storefront-digests-shadow-example.conf" \
  "$PLATFORM_DIR/deploy/aspnet/"
# Pack remaining exact-route catalog shadow examples for one-path promotion helpers.
shopt -s nullglob
for shadow in "$ROOT"/deploy/aspnet/nginx-*-shadow-example.conf; do
  install -m 0644 "$shadow" "$PLATFORM_DIR/deploy/aspnet/"
done
shopt -u nullglob
install -m 0755 \
  "$ROOT/scripts/cloudpanel_capture_final_gate_artifacts.sh" \
  "$ROOT/scripts/cloudpanel_validate_final_gate_env.sh" \
  "$ROOT/scripts/cloudpanel_repair_smoke_cookie_env.sh" \
  "$ROOT/scripts/cloudpanel_prepare_smoke_secrets.sh" \
  "$ROOT/scripts/cloudpanel_commit_final_gate_smoke.sh" \
  "$ROOT/scripts/cloudpanel_issue_smoke_credentials.sh" \
  "$ROOT/scripts/cloudpanel_ensure_epc_api_clients_table.sh" \
  "$ROOT/scripts/cloudpanel_print_epc_api_clients_ddl.sh" \
  "$ROOT/scripts/cloudpanel_extract_exact_route_shadow.sh" \
  "$ROOT/scripts/wait_for_aspnet_health.sh" \
  "$ROOT/scripts/rollback_aspnet_foundation.sh" \
  "$ROOT/scripts/run_zero_php_final_gate_checklist.sh" \
  "$ROOT/scripts/run_surface_parity_harness.sh" \
  "$PLATFORM_DIR/scripts/"
install -d "$PLATFORM_DIR/scripts/sql"
if [[ -f "$ROOT/scripts/sql/epc_api_clients.sql" ]]; then
  install -m 0644 "$ROOT/scripts/sql/epc_api_clients.sql" "$PLATFORM_DIR/scripts/sql/"
fi
# Parity compare helpers used after dual-sample capture (post-smoke promotion).
for compare in \
  compare_catalog_status_parity.py \
  compare_catalog_list_parity.py \
  compare_catalog_offline_cache_parity.py \
  compare_catalog_vin_parity.py \
  compare_catalog_brand_parts_parity.py \
  compare_price_lookup_parity.py \
  compare_digest_dual_samples.py \
  compare_surface_payload_parity.py \
  generate_migration_digest_contract_samples.py
do
  if [[ -f "$ROOT/scripts/$compare" ]]; then
    install -m 0755 "$ROOT/scripts/$compare" "$PLATFORM_DIR/scripts/"
  fi
done
# PHP issuer + ensure-table helpers (DP_Config bootstrap required by issue script).
install -d "$PLATFORM_DIR/scripts/php"
for php_helper in \
  issue_final_gate_smoke_credentials.php \
  ensure_epc_api_clients_table.php \
  _smoke_db_bootstrap.php
do
  if [[ -f "$ROOT/scripts/php/$php_helper" ]]; then
    install -m 0644 "$ROOT/scripts/php/$php_helper" "$PLATFORM_DIR/scripts/php/"
  fi
done
printf 'Packed decommission evidence into %s/docs/migration/evidence/decommission\n' "$PLATFORM_DIR"
printf 'Packed gate shadow examples (price/api/surface/storefront) into %s/deploy/aspnet\n' "$PLATFORM_DIR"
printf 'Packed smoke issuer/ensure PHP helpers into %s/scripts/php\n' "$PLATFORM_DIR"
printf 'Packed catalog/price parity compare scripts into %s/scripts\n' "$PLATFORM_DIR"

ln -sfn "$RELEASE_DIR" "$RELEASE_ROOT/current"

printf '\nPublished release: %s\n' "$RELEASE_DIR"
printf 'Current symlink: %s/current -> %s\n' "$RELEASE_ROOT" "$RELEASE_DIR"

if [[ ! -f "$SERVICE_ENV_DIR/platform.env" ]]; then
    printf '\nEnvironment file missing: %s/platform.env\n' "$SERVICE_ENV_DIR"
    printf 'Create it from deploy/aspnet/platform.env.example before starting services.\n'
fi

if [[ "$RUN_SYSTEMD" == "1" ]]; then
    install -d /etc/systemd/system "$SERVICE_ENV_DIR"
    install -m 0644 "$ROOT/deploy/aspnet/ecomae-platform.service" /etc/systemd/system/ecomae-platform.service
    install -m 0644 "$ROOT/deploy/aspnet/ecomae-workers.service" /etc/systemd/system/ecomae-workers.service
    systemctl daemon-reload
    systemctl enable ecomae-platform.service
    systemctl restart ecomae-platform.service
    systemctl status ecomae-platform.service --no-pager
    # systemd can report active before Kestrel binds :5100 — wait before callers run smoke.
    ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:${PLATFORM_PORT}}" \
      bash "$ROOT/scripts/wait_for_aspnet_health.sh"
else
    printf '\nSkipped systemd actions. Set ECOMAE_RUN_SYSTEMD=1 to install/restart services.\n'
fi

if [[ "$RUN_NGINX_RELOAD" == "1" ]]; then
    nginx -t
    systemctl reload nginx
else
    printf 'Skipped nginx reload. Add deploy/aspnet/nginx-diagnostics-only.conf to the CloudPanel site and reload manually.\n'
fi

printf '\nLocal verification command after service start:\n'
printf 'curl -i http://127.0.0.1:%s/health\n' "$PLATFORM_PORT"
