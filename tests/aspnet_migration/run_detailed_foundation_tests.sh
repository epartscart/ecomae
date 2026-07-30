#!/usr/bin/env bash
set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PASSED=0
WARNED=0
FAILED=0

pass() { printf '  PASS  %s\n' "$1"; PASSED=$((PASSED + 1)); }
warn() { printf '  WARN  %s\n' "$1"; WARNED=$((WARNED + 1)); }
fail() { printf '  FAIL  %s\n' "$1"; FAILED=$((FAILED + 1)); }

run_required() {
    local name="$1"
    shift
    printf '\n== %s ==\n' "$name"
    if "$@"; then
        pass "$name"
    else
        fail "$name"
    fi
}

run_optional() {
    local name="$1"
    shift
    printf '\n== %s ==\n' "$name"
    if "$@"; then
        pass "$name"
    else
        warn "$name"
    fi
}

printf '== ASP.NET Core migration detailed foundation verification ==\n'

run_required 'foundation wiring checks' "$ROOT/tests/aspnet_migration/run_foundation_checks.sh"
run_required 'PHP surface route alias regression checks' php "$ROOT/tests/erp_advanced/run_surface_route_alias_tests.php"

printf '\n== PHP syntax checks for migration-touched entry points ==\n'
PHP_FILES=(
    "$ROOT/content/general_pages/epc_portal_route_aliases.php"
    "$ROOT/index.php"
    "$ROOT/cp/content/shop/finance/erp/erp_dashboard.php"
    "$ROOT/cp/content/users/statistics/app.php"
    "$ROOT/epc-all-tasks-final-report.php"
    "$ROOT/epc-apai-tenant-industry-fix.php"
    "$ROOT/epc-regenerate-issues-report.php"
)
for file in "${PHP_FILES[@]}"; do
    if [[ ! -f "$file" ]]; then
        fail "missing PHP file: ${file#$ROOT/}"
        continue
    fi

    if php -l "$file" >/dev/null; then
        pass "php -l ${file#$ROOT/}"
    else
        fail "php -l ${file#$ROOT/}"
    fi
done

printf '\n== Shell syntax checks for migration scripts ==\n'
SHELL_FILES=(
    "$ROOT/tests/aspnet_migration/run_foundation_checks.sh"
    "$ROOT/tests/aspnet_migration/run_detailed_foundation_tests.sh"
    "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh"
    "$ROOT/scripts/prepare_consolidated_aspnet_pr.sh"
    "$ROOT/scripts/push_consolidated_pr_update.sh"
    "$ROOT/scripts/rebase_conflicted_pr_range.sh"
    "$ROOT/scripts/deploy_aspnet_foundation.sh"
    "$ROOT/scripts/rollback_aspnet_foundation.sh"
)
for file in "${SHELL_FILES[@]}"; do
    if [[ ! -f "$file" ]]; then
        fail "missing shell script: ${file#$ROOT/}"
        continue
    fi

    if bash -n "$file"; then
        pass "bash -n ${file#$ROOT/}"
    else
        fail "bash -n ${file#$ROOT/}"
    fi
done

if command -v dotnet >/dev/null 2>&1; then
    run_required '.NET unit tests' dotnet test "$ROOT/aspnet/tests/EcomAE.Platform.Tests"
else
    warn '.NET unit tests skipped; dotnet SDK is not installed in this environment'
fi

if [[ "${RUN_LIVE_ECOMAE_SMOKE:-}" == "1" ]]; then
    run_optional 'opt-in live smoke checks' "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh"
else
    warn 'opt-in live smoke checks skipped; set RUN_LIVE_ECOMAE_SMOKE=1 with approved environment URLs and credentials'
fi

printf '\n----------------------------\n'
printf 'Passed: %d  Warnings: %d  Failed: %d\n' "$PASSED" "$WARNED" "$FAILED"

if [[ "$FAILED" -ne 0 ]]; then
    exit 1
fi
