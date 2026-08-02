#!/usr/bin/env bash
set -u -o pipefail

RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
PLATFORM_PORT="${ECOMAE_ASPNET_PORT:-5100}"
CHECK_LOCAL_SERVICE="${ECOMAE_CHECK_LOCAL_ASPNET:-0}"

passed=0
warned=0
failed=0

pass() { printf '  PASS  %s\n' "$1"; passed=$((passed + 1)); }
warn() { printf '  WARN  %s\n' "$1"; warned=$((warned + 1)); }
fail() { printf '  FAIL  %s\n' "$1"; failed=$((failed + 1)); }

has_command() {
    command -v "$1" >/dev/null 2>&1
}

check_command() {
    local command_name="$1"
    local required="$2"
    if has_command "$command_name"; then
        pass "command available: $command_name"
    elif [[ "$required" == "required" ]]; then
        fail "required command missing: $command_name"
    else
        warn "optional command missing: $command_name"
    fi
}

printf '== EcomAE ASP.NET Core production preflight ==\n'
printf 'Release root: %s\n' "$RELEASE_ROOT"
printf 'Environment file: %s\n' "$ENV_FILE"
printf 'Local ASP.NET Core port: %s\n' "$PLATFORM_PORT"

check_command dotnet required
check_command php required
check_command curl required
check_command nginx optional
check_command systemctl optional
check_command install required

if has_command dotnet; then
    dotnet_version="$(dotnet --version 2>/dev/null || true)"
    case "$dotnet_version" in
        10.*)
            pass "dotnet SDK/runtime is compatible: $dotnet_version"
            ;;
        "")
            fail "dotnet exists but version could not be read"
            ;;
        *)
            warn "dotnet version is $dotnet_version; repository global.json pins 10.0.100"
            ;;
    esac
fi

if [[ -d "$RELEASE_ROOT" ]]; then
    pass "release root exists"
else
    warn "release root missing; create with: sudo mkdir -p $RELEASE_ROOT/releases"
fi

if [[ -f "$ENV_FILE" ]]; then
    pass "environment file exists"
    perms="$(stat -c '%a' "$ENV_FILE" 2>/dev/null || stat -f '%Lp' "$ENV_FILE" 2>/dev/null || true)"
    if [[ "$perms" == "600" || "$perms" == "640" ]]; then
        pass "environment file permissions are restricted: $perms"
    else
        warn "environment file permissions should be 600 or 640; current=${perms:-unknown}"
    fi

    if grep -Eq 'Password=<db_password>|<from-secret-manager>|<db_user>' "$ENV_FILE"; then
        fail "environment file still contains placeholder credentials"
    else
        pass "environment file does not contain known placeholder credentials"
    fi

    if grep -Fq 'MigrationRouteCutover__RequirePhpFallback=true' "$ENV_FILE"; then
        pass "PHP fallback remains enabled"
    else
        fail "PHP fallback is not explicitly enabled"
    fi
else
    warn "environment file missing; copy deploy/aspnet/platform.env.example to $ENV_FILE"
fi

if [[ "$CHECK_LOCAL_SERVICE" == "1" ]]; then
    if curl -fsS "http://127.0.0.1:$PLATFORM_PORT/health" >/dev/null; then
        pass "local ASP.NET Core /health endpoint responded"
    else
        fail "local ASP.NET Core /health endpoint did not respond on port $PLATFORM_PORT"
    fi
else
    warn "local ASP.NET Core /health check skipped; set ECOMAE_CHECK_LOCAL_ASPNET=1 after service start"
fi

printf '\n----------------------------\n'
printf 'Passed: %d  Warnings: %d  Failed: %d\n' "$passed" "$warned" "$failed"

if [[ "$failed" -ne 0 ]]; then
    exit 1
fi
