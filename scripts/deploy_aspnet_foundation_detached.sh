#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="${ECOMAE_DEPLOY_LOG_DIR:-$ROOT/deploy/logs}"
STAMP="$(date -u +%Y%m%d%H%M%S)"
LOG_FILE="${ECOMAE_DEPLOY_LOG_FILE:-$LOG_DIR/aspnet-foundation-deploy-$STAMP.log}"
USE_TMUX="${ECOMAE_DEPLOY_USE_TMUX:-0}"
SESSION_NAME="${ECOMAE_DEPLOY_TMUX_SESSION:-ecomae-aspnet-deploy}"

mkdir -p "$LOG_DIR"

printf '== EcomAE detached ASP.NET deploy ==\n'
printf 'Repo: %s\n' "$ROOT"
printf 'Log file: %s\n' "$LOG_FILE"
printf 'Use tmux: %s\n' "$USE_TMUX"
printf 'PHP fallback must remain enabled; do not broad-cutover CP/ERP/BOS/API/storefront.\n'

cmd=(bash "$ROOT/scripts/deploy_aspnet_foundation.sh")

if [[ "$USE_TMUX" == "1" ]]; then
  if ! command -v tmux >/dev/null 2>&1; then
    printf 'tmux requested but not installed. Install tmux or run without ECOMAE_DEPLOY_USE_TMUX=1.\n' >&2
    exit 1
  fi
  tmux new-session -d -s "$SESSION_NAME" "cd '$ROOT' && ECOMAE_ASPNET_RELEASE_ROOT='${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}' ECOMAE_RUN_SYSTEMD='${ECOMAE_RUN_SYSTEMD:-0}' ECOMAE_RUN_NGINX_RELOAD='${ECOMAE_RUN_NGINX_RELOAD:-0}' DOTNET_CONFIGURATION='${DOTNET_CONFIGURATION:-Release}' bash scripts/deploy_aspnet_foundation.sh 2>&1 | tee '$LOG_FILE'"
  printf 'Started tmux session: %s\n' "$SESSION_NAME"
  printf 'Attach: tmux attach -t %s\n' "$SESSION_NAME"
else
  (
    cd "$ROOT"
    ECOMAE_ASPNET_RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}" \
    ECOMAE_RUN_SYSTEMD="${ECOMAE_RUN_SYSTEMD:-0}" \
    ECOMAE_RUN_NGINX_RELOAD="${ECOMAE_RUN_NGINX_RELOAD:-0}" \
    DOTNET_CONFIGURATION="${DOTNET_CONFIGURATION:-Release}" \
    nohup "${cmd[@]}" >"$LOG_FILE" 2>&1 &
    printf '%s\n' "$!" > "$LOG_FILE.pid"
  )
  printf 'Started background deploy PID: %s\n' "$(cat "$LOG_FILE.pid")"
fi

printf 'Follow logs: tail -f %s\n' "$LOG_FILE"
printf 'Check completion: grep -E "Published release|Missing required command|FAIL|Failed" %s || true\n' "$LOG_FILE"
