#!/usr/bin/env bash
set -euo pipefail

REMOTE_HOST="${ECOMAE_PROD_HOST:-}"
REMOTE_USER="${ECOMAE_PROD_SSH_USER:-root}"
REMOTE_PORT="${ECOMAE_PROD_SSH_PORT:-22}"
REMOTE_REPO="${ECOMAE_PROD_REPO_URL:-}"
REMOTE_BRANCH="${ECOMAE_PROD_BRANCH:-work}"
REMOTE_WORKDIR="${ECOMAE_PROD_WORKDIR:-/opt/ecomae-aspnet-source}"
REMOTE_RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
RUN_REMOTE_DEPLOY="${ECOMAE_RUN_REMOTE_DEPLOY:-0}"
RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-0}"
RUN_NGINX_RELOAD="${ECOMAE_RUN_NGINX_RELOAD:-0}"

printf '== EcomAE remote ASP.NET foundation deploy ==\n'
printf 'Remote host: %s\n' "${REMOTE_HOST:-<unset>}"
printf 'Remote user: %s\n' "$REMOTE_USER"
printf 'Remote port: %s\n' "$REMOTE_PORT"
printf 'Remote branch: %s\n' "$REMOTE_BRANCH"
printf 'Remote workdir: %s\n' "$REMOTE_WORKDIR"
printf 'Release root: %s\n' "$REMOTE_RELEASE_ROOT"
printf 'Remote deploy enabled: %s\n' "$RUN_REMOTE_DEPLOY"
printf 'Systemd enabled: %s\n' "$RUN_SYSTEMD"
printf 'Nginx reload enabled: %s\n' "$RUN_NGINX_RELOAD"
printf 'Secrets: redacted; provide credentials only through SSH agent/server secret files.\n'

if [[ -z "$REMOTE_HOST" ]]; then
    printf 'Set ECOMAE_PROD_HOST to the production host or IP.\n' >&2
    exit 1
fi

if [[ -z "$REMOTE_REPO" ]]; then
    printf 'Set ECOMAE_PROD_REPO_URL to the Git repository URL reachable from production.\n' >&2
    exit 1
fi

if [[ "$RUN_REMOTE_DEPLOY" != "1" ]]; then
    cat <<PLAN

Dry-run plan only. Set ECOMAE_RUN_REMOTE_DEPLOY=1 to execute.

Remote commands to be executed:
  1. Install/refresh source at $REMOTE_WORKDIR from $REMOTE_REPO branch $REMOTE_BRANCH.
  2. Run bash tests/aspnet_migration/run_detailed_foundation_tests.sh.
  3. Run bash scripts/preflight_aspnet_production.sh.
  4. Run bash scripts/verify_aspnet_proxy_guardrails.sh.
  5. Run scripts/deploy_aspnet_foundation.sh with ECOMAE_ASPNET_RELEASE_ROOT=$REMOTE_RELEASE_ROOT.
  6. Leave CloudPanel/Nginx broad routes untouched; diagnostics include must be added manually or through approved config management.
PLAN
    exit 0
fi

ssh_target="$REMOTE_USER@$REMOTE_HOST"
ssh_opts=(-p "$REMOTE_PORT" -o BatchMode=yes -o StrictHostKeyChecking=accept-new)

ssh "${ssh_opts[@]}" "$ssh_target" bash -s -- "$REMOTE_REPO" "$REMOTE_BRANCH" "$REMOTE_WORKDIR" "$REMOTE_RELEASE_ROOT" "$RUN_SYSTEMD" "$RUN_NGINX_RELOAD" <<'REMOTE'
set -euo pipefail
repo_url="$1"
branch="$2"
workdir="$3"
release_root="$4"
run_systemd="$5"
run_nginx_reload="$6"

if [[ ! -d "$workdir/.git" ]]; then
    mkdir -p "$(dirname "$workdir")"
    git clone "$repo_url" "$workdir"
fi

cd "$workdir"
git fetch --all --prune
git checkout "$branch"
git pull --ff-only

bash tests/aspnet_migration/run_detailed_foundation_tests.sh
bash scripts/preflight_aspnet_production.sh
bash scripts/verify_aspnet_proxy_guardrails.sh

ECOMAE_ASPNET_RELEASE_ROOT="$release_root" \
ECOMAE_RUN_SYSTEMD="$run_systemd" \
ECOMAE_RUN_NGINX_RELOAD="$run_nginx_reload" \
bash scripts/deploy_aspnet_foundation.sh
REMOTE
