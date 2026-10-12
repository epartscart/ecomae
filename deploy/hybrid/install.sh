#!/usr/bin/env bash
# ecomae ERP — Hybrid installer (on-premises ERP + outbound HTTPS sync agent)
# Usage:
#   ./install.sh --site-key acme --cloud-url https://www.ecomae.com \
#                --enrollment enr_XXX --installation inst_XXX \
#                [--license KEY] [--domain erp.company.com] [--dsn mysql://...] \
#                [--erp-dir /opt/ecomae] [--skip-erp]
#
# Does not invent a MySQL user or a send. LOCAL_DSN is used only when you pass --dsn
# or when the existing ERP .env already has a non-empty DB_PASSWORD.

set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'
log()  { echo -e "${GREEN}[hybrid]${NC} $1"; }
warn() { echo -e "${YELLOW}[warn]${NC} $1"; }
err()  { echo -e "${RED}[error]${NC} $1" >&2; }

SITE_KEY=""
CLOUD_URL=""
ENROLLMENT=""
INSTALLATION=""
LICENSE_KEY=""
DOMAIN=""
LOCAL_DSN=""
ERP_DIR="/opt/ecomae"
SKIP_ERP=0
CONFIRM_WRITES=0
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

while [[ $# -gt 0 ]]; do
    case $1 in
        --site-key)      SITE_KEY="$2"; shift 2 ;;
        --cloud-url)     CLOUD_URL="$2"; shift 2 ;;
        --enrollment)    ENROLLMENT="$2"; shift 2 ;;
        --installation)  INSTALLATION="$2"; shift 2 ;;
        --license)       LICENSE_KEY="$2"; shift 2 ;;
        --domain)        DOMAIN="$2"; shift 2 ;;
        --dsn)           LOCAL_DSN="$2"; shift 2 ;;
        --erp-dir|--dir) ERP_DIR="$2"; shift 2 ;;
        --skip-erp)      SKIP_ERP=1; shift ;;
        --confirm-writes) CONFIRM_WRITES=1; shift ;;
        --help|-h)
            echo "Usage: ./install.sh --site-key KEY --cloud-url https://www.ecomae.com --enrollment enr_XXX --installation inst_XXX [--license KEY] [--dsn DSN] [--skip-erp]"
            exit 0 ;;
        *) err "Unknown option: $1"; exit 1 ;;
    esac
done

if [[ -z "$SITE_KEY" || -z "$CLOUD_URL" || -z "$ENROLLMENT" || -z "$INSTALLATION" ]]; then
    err "Required: --site-key --cloud-url --enrollment --installation"
    err "Register the site first from Super CP POST /cp/hybrid-sync/register (confirmWrites=true)."
    exit 1
fi

if [[ "$CLOUD_URL" != https://* ]]; then
    err "CLOUD URL must be HTTPS. The hybrid agent is outbound HTTPS only."
    exit 1
fi

if [[ "$ENROLLMENT" != enr_* ]]; then
    err "Enrollment token must start with enr_. Do not invent one."
    exit 1
fi

echo ""
echo -e "${BLUE}╔══════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║     ecomae ERP — Hybrid Installer        ║${NC}"
echo -e "${BLUE}╚══════════════════════════════════════════╝${NC}"
echo ""

if [[ "$SKIP_ERP" -eq 0 ]]; then
    ONPREM=""
    ASPNET_PACK=0
    for candidate in \
        "$SCRIPT_DIR/../on-premises-aspnet/install.sh" \
        "$SCRIPT_DIR/../on-premises-aspnet/ecomae-setup" \
        "$ERP_DIR/ecomae-setup" \
        "$ERP_DIR/install.sh" \
        "$SCRIPT_DIR/../on-premises/install.sh" \
        "$PWD/on-premises-aspnet/install.sh" \
        "$PWD/on-premises/install.sh"
    do
        if [[ -x "$candidate" || -f "$candidate" ]]; then
            ONPREM="$candidate"
            if [[ "$candidate" == *on-premises-aspnet* || "$candidate" == *ecomae-setup ]]; then
                ASPNET_PACK=1
            fi
            break
        fi
    done
    if [[ -z "$ONPREM" ]]; then
        err "On-premises setup not found. Download the platform pack from Super CP"
        err "  /cp/deployment-models/setup-pack?kind=on-premises-aspnet"
        err "or re-run with --skip-erp if ERP is already installed in $ERP_DIR."
        exit 1
    fi
    log "Installing on-premises ERP via $ONPREM"
    if [[ "$ASPNET_PACK" -eq 1 ]]; then
        if [[ -z "$LOCAL_DSN" && -z "${DB_PASSWORD:-}" ]]; then
            err "Platform pack needs --dsn or an existing ERP password. The installer will not invent one."
            exit 1
        fi
        bash "$ONPREM" --host "${DOMAIN:-erp.local}" --dsn "$LOCAL_DSN" --dir "$ERP_DIR"
    else
        if [[ -z "$LICENSE_KEY" ]]; then
            err "Leftover pack needs --license KEY or use the platform pack / --skip-erp."
            exit 1
        fi
        bash "$ONPREM" --license "$LICENSE_KEY" --domain "$DOMAIN" --dir "$ERP_DIR"
    fi
fi

HYBRID_DIR="$ERP_DIR/hybrid"
log "Installing sync agent into $HYBRID_DIR"
mkdir -p "$HYBRID_DIR/state" "$HYBRID_DIR/sync-agent" "$HYBRID_DIR/systemd"
cp -a "$SCRIPT_DIR/sync-agent/." "$HYBRID_DIR/sync-agent/"
cp -a "$SCRIPT_DIR/systemd/." "$HYBRID_DIR/systemd/" 2>/dev/null || true
cp -a "$SCRIPT_DIR/docker-compose.hybrid.yml" "$HYBRID_DIR/" 2>/dev/null || true
cp -a "$SCRIPT_DIR/README.md" "$HYBRID_DIR/" 2>/dev/null || true

if [[ -z "$LOCAL_DSN" && -f "$ERP_DIR/.env" ]]; then
    # Use the ERP install's own password when present. Never invent from empty.
    set +u
    # shellcheck disable=SC1091
    source "$ERP_DIR/.env"
    set -u
    if [[ -n "${DB_PASSWORD:-}" && "${DB_PASSWORD}" != CHANGE_ME* && -n "${DB_USERNAME:-}" && -n "${DB_DATABASE:-}" ]]; then
        LOCAL_DSN="mysql://${DB_USERNAME}:${DB_PASSWORD}@127.0.0.1:${DB_EXTERNAL_PORT:-3306}/${DB_DATABASE}"
        log "Using DSN from existing ERP .env (password not printed)."
    else
        warn "ERP .env has no usable DB_PASSWORD — agent will use ./state JSON files."
        LOCAL_DSN=""
    fi
fi

cat > "$HYBRID_DIR/.env" << ENVEOF
SITE_KEY=${SITE_KEY}
INSTALLATION_ID=${INSTALLATION}
ENROLLMENT_REQUEST_ID=${ENROLLMENT}
CLOUD_BASE_URL=${CLOUD_URL}
LOCAL_DSN=${LOCAL_DSN}
CONFIRM_WRITES=${CONFIRM_WRITES}
CYCLE_SECONDS=300
STATE_DIR=${HYBRID_DIR}/state
ENVEOF
chmod 600 "$HYBRID_DIR/.env"

if [[ -n "$LOCAL_DSN" ]]; then
    if command -v mysql >/dev/null 2>&1; then
        log "Applying local inbox/outbox schema (operator DSN)."
        python3 - "$LOCAL_DSN" "$HYBRID_DIR/sync-agent/local-schema.sql" <<'PY' || warn "Could not apply local-schema.sql — agent will use JSON state."
import sys, subprocess, urllib.parse
dsn, sql_path = sys.argv[1], sys.argv[2]
u = urllib.parse.urlparse(dsn)
if u.scheme not in ("mysql", "mysql+pymysql"):
    raise SystemExit("LOCAL_DSN must be mysql://user:pass@host:port/db")
host = u.hostname or "127.0.0.1"
port = str(u.port or 3306)
user = urllib.parse.unquote(u.username or "")
password = urllib.parse.unquote(u.password or "")
db = (u.path or "").lstrip("/")
if not user or not password or not db:
    raise SystemExit("LOCAL_DSN is missing user, password, or database — refusing to invent one.")
cmd = ["mysql", "-h", host, "-P", port, "-u", user, db, "--password=" + password]
with open(sql_path, "r", encoding="utf-8") as fh:
    subprocess.run(cmd, input=fh.read(), text=True, check=True)
PY
    else
        warn "mysql client not installed — skip local-schema.sql. JSON state still works."
    fi
fi

if command -v python3 >/dev/null 2>&1; then
    log "Running first hybrid cycle (pull then ingest only if the outbox has rows)."
    ( cd "$HYBRID_DIR" && python3 sync-agent/sync-agent.py cycle ) || warn "First cycle did not finish — check enrollment and HTTPS."
else
    err "python3 is required for the sync agent."
    exit 1
fi

if command -v systemctl >/dev/null 2>&1 && [[ "$(id -u)" -eq 0 ]]; then
    sed "s|/opt/ecomae/hybrid|$HYBRID_DIR|g" "$HYBRID_DIR/systemd/ecomae-hybrid-sync.service" \
        > /etc/systemd/system/ecomae-hybrid-sync.service
    systemctl daemon-reload
    systemctl enable --now ecomae-hybrid-sync.service
    log "systemd unit ecomae-hybrid-sync.service enabled."
else
    warn "Not root or no systemd — start the agent with:"
    warn "  cd $HYBRID_DIR && python3 sync-agent/sync-agent.py cycle --loop"
fi

echo ""
echo -e "${GREEN}Hybrid agent installed.${NC}"
echo "  Site key:    $SITE_KEY"
echo "  Cloud:       $CLOUD_URL"
echo "  Agent dir:   $HYBRID_DIR"
echo "  State:       $HYBRID_DIR/state"
echo ""
log "Conflicts stay queued. This installer does not invent a send."
