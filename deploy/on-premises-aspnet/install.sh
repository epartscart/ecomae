#!/usr/bin/env bash
# ECOM AE ASP.NET Core on-premises installer. No PHP.
# Usage:
#   ./install.sh --host erp.company.com --db-password 'YOUR_PASSWORD'
#   ./install.sh --host erp.company.com --dsn 'Server=127.0.0.1;Port=3306;Database=ecomae;Uid=ecomae;Pwd=YOUR_PASSWORD;AllowUserVariables=true;'
#
# Will not invent a MySQL user or password. Pass --db-password or --dsn.
set -euo pipefail

GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
log()  { echo -e "${GREEN}[ecomae-aspnet]${NC} $1"; }
warn() { echo -e "${YELLOW}[warn]${NC} $1"; }
err()  { echo -e "${RED}[error]${NC} $1" >&2; }

PUBLIC_HOST=""
DB_PASSWORD=""
DSN=""
HTTP_PORT="5100"
INSTALL_DIR=""
GIT_REPO_URL="${GIT_REPO_URL:-https://github.com/epartscart/ecomae.git}"
GIT_REPO_REF="${GIT_REPO_REF:-main}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

while [[ $# -gt 0 ]]; do
    case $1 in
        --host|--domain) PUBLIC_HOST="$2"; shift 2 ;;
        --db-password)   DB_PASSWORD="$2"; shift 2 ;;
        --dsn)           DSN="$2"; shift 2 ;;
        --port)          HTTP_PORT="$2"; shift 2 ;;
        --dir)           INSTALL_DIR="$2"; shift 2 ;;
        --repo)          GIT_REPO_URL="$2"; shift 2 ;;
        --ref)           GIT_REPO_REF="$2"; shift 2 ;;
        --help|-h)
            echo "Usage: ./install.sh --host erp.company.com --db-password PASS [--port 5100] [--dsn DSN]"
            exit 0 ;;
        *) err "Unknown option: $1"; exit 1 ;;
    esac
done

if [[ -z "$PUBLIC_HOST" ]]; then
    err "Required: --host erp.company.com"
    exit 1
fi
if [[ -z "$DB_PASSWORD" && -z "$DSN" ]]; then
    err "Required: --db-password or --dsn. The installer will not invent a database password."
    exit 1
fi
if [[ -n "$DSN" && "$DSN" == *Pwd=\;* ]]; then
    err "DSN has an empty Pwd=. Pass a real password. The installer will not invent one."
    exit 1
fi

INSTALL_DIR="${INSTALL_DIR:-$SCRIPT_DIR}"
cd "$INSTALL_DIR"

if ! command -v docker >/dev/null 2>&1; then
    err "Docker is required. Install Docker, then re-run."
    exit 1
fi
if ! docker compose version >/dev/null 2>&1; then
    err "Docker Compose v2 is required (docker compose)."
    exit 1
fi

if [[ ! -d "$INSTALL_DIR/src/.git" ]]; then
    log "Fetching ASP.NET source into ./src (no PHP runtime)..."
    git clone --depth 1 --branch "$GIT_REPO_REF" "$GIT_REPO_URL" "$INSTALL_DIR/src"
fi

if [[ ! -f "$INSTALL_DIR/src/aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj" ]]; then
    err "ASP.NET project missing under ./src. Refusing to fall back to PHP."
    exit 1
fi

if [[ -z "$DB_PASSWORD" && -n "$DSN" ]]; then
    DB_PASSWORD="$(python3 -c 'import sys
dsn=sys.argv[1]
for part in dsn.split(";"):
    if "=" in part:
        k,v=part.split("=",1)
        if k.strip().lower() in ("pwd","password"):
            print(v.strip(), end="")
            break
' "$DSN")"
fi
if [[ -z "$DB_PASSWORD" ]]; then
    err "Database password is still empty. The installer will not invent one."
    exit 1
fi

umask 077
cat > "$INSTALL_DIR/.env" <<EOF
PUBLIC_HOST=${PUBLIC_HOST}
ASPNETCORE_URLS=http://0.0.0.0:5100
HTTP_PORT=${HTTP_PORT}
DB_HOST=mariadb
DB_PORT=3306
DB_NAME=ecomae
DB_USER=ecomae
DB_PASSWORD=${DB_PASSWORD}
LICENSE_KEY=
EOF
chmod 600 "$INSTALL_DIR/.env"
log "Wrote .env (mode 600). Password is not printed."

log "Starting ASP.NET Core + MariaDB (no PHP)..."
docker compose -f "$INSTALL_DIR/docker-compose.yml" --env-file "$INSTALL_DIR/.env" up -d --build

for i in $(seq 1 40); do
    if curl -fsS -m 3 "http://127.0.0.1:${HTTP_PORT}/health" >/dev/null 2>&1; then
        log "Health OK at http://127.0.0.1:${HTTP_PORT}/health"
        log "ERP login: http://${PUBLIC_HOST}:${HTTP_PORT}/erp/login"
        log "PHP was not installed. New work stays on ASP.NET Core."
        exit 0
    fi
    sleep 3
done

err "Platform did not become healthy on :${HTTP_PORT}. Check: docker compose logs platform"
exit 1
