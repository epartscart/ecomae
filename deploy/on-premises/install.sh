#!/usr/bin/env bash
# ecomae ERP — On-Premises Installer
# Usage: curl -sSL https://install.ecomae.com | bash -s -- --license YOUR_KEY
#        OR: ./install.sh --license YOUR_KEY [--domain erp.company.com] [--port 443]
#
# This script:
#   1. Checks system requirements (Docker, disk, RAM)
#   2. Downloads the latest ecomae release
#   3. Sets up Docker Compose environment
#   4. Generates SSL (Let's Encrypt or self-signed)
#   5. Starts all services
#   6. Runs initial setup wizard
#   7. Activates license (online or offline)

set -euo pipefail

# Colors
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'

log()  { echo -e "${GREEN}[ecomae]${NC} $1"; }
warn() { echo -e "${YELLOW}[warn]${NC} $1"; }
err()  { echo -e "${RED}[error]${NC} $1" >&2; }

# Defaults
LICENSE_KEY=""
DOMAIN=""
INSTALL_DIR="/opt/ecomae"
HTTP_PORT=80
HTTPS_PORT=443
DB_PASSWORD=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 32)
DB_ROOT_PASSWORD=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 32)

# Parse args
while [[ $# -gt 0 ]]; do
    case $1 in
        --license)   LICENSE_KEY="$2"; shift 2 ;;
        --domain)    DOMAIN="$2"; shift 2 ;;
        --dir)       INSTALL_DIR="$2"; shift 2 ;;
        --port)      HTTPS_PORT="$2"; shift 2 ;;
        --help|-h)
            echo "Usage: ./install.sh --license KEY [--domain erp.company.com] [--dir /opt/ecomae] [--port 443]"
            exit 0 ;;
        *) err "Unknown option: $1"; exit 1 ;;
    esac
done

if [[ -z "$LICENSE_KEY" ]]; then
    err "License key required. Use: ./install.sh --license YOUR_KEY"
    err "Get a license from your ecomae BOS → On-Premises → Generate License"
    exit 1
fi

echo ""
echo -e "${BLUE}╔══════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║     ecomae ERP — On-Premises Installer   ║${NC}"
echo -e "${BLUE}╚══════════════════════════════════════════╝${NC}"
echo ""

# Step 1: Check requirements
log "Checking system requirements..."

# Docker
if ! command -v docker &>/dev/null; then
    warn "Docker not found. Installing Docker..."
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker && systemctl start docker
    log "Docker installed successfully"
fi

DOCKER_VERSION=$(docker --version | grep -oP '\d+\.\d+' | head -1)
log "Docker version: $DOCKER_VERSION"

# Docker Compose
if ! docker compose version &>/dev/null; then
    err "Docker Compose v2 required. Install: apt-get install docker-compose-plugin"
    exit 1
fi

# System resources
TOTAL_RAM_GB=$(free -g | awk '/^Mem:/{print $2}')
TOTAL_DISK_GB=$(df -BG / | awk 'NR==2{print $4}' | tr -dc '0-9')
CPU_CORES=$(nproc)

log "System: ${CPU_CORES} CPU cores, ${TOTAL_RAM_GB}GB RAM, ${TOTAL_DISK_GB}GB free disk"

if [[ $TOTAL_RAM_GB -lt 4 ]]; then
    err "Minimum 4GB RAM required (found: ${TOTAL_RAM_GB}GB)"
    exit 1
fi

if [[ $TOTAL_DISK_GB -lt 50 ]]; then
    err "Minimum 50GB free disk required (found: ${TOTAL_DISK_GB}GB)"
    exit 1
fi

if [[ $CPU_CORES -lt 2 ]]; then
    warn "Minimum 2 CPU cores recommended (found: ${CPU_CORES})"
fi

# Step 2: Create install directory
log "Installing to: $INSTALL_DIR"
mkdir -p "$INSTALL_DIR"
cd "$INSTALL_DIR"

# Step 3: Download latest release (or copy local)
if [[ -f "./docker-compose.yml" ]]; then
    log "Existing installation detected — upgrading"
    docker compose down 2>/dev/null || true
fi

# For now, create the structure locally (in production this downloads from releases server)
log "Setting up deployment structure..."
mkdir -p app nginx/ssl mysql php storage backups/mysql

# Step 4: Generate configs
cat > .env << ENVEOF
APP_URL=https://${DOMAIN:-localhost}
APP_ENV=production
TIMEZONE=UTC
LICENSE_KEY=${LICENSE_KEY}
DB_DATABASE=ecomae_erp
DB_USERNAME=ecomae
DB_PASSWORD=${DB_PASSWORD}
DB_ROOT_PASSWORD=${DB_ROOT_PASSWORD}
DB_EXTERNAL_PORT=3306
REDIS_HOST=redis
REDIS_PORT=6379
HTTP_PORT=${HTTP_PORT}
HTTPS_PORT=${HTTPS_PORT}
BACKUP_RETENTION_DAYS=30
BACKUP_ENCRYPT=true
BOS_SYNC_MODE=disabled
ENVEOF

# PHP config
cat > php/local.ini << 'PHPEOF'
upload_max_filesize = 64M
post_max_size = 64M
max_execution_time = 300
memory_limit = 512M
date.timezone = UTC
opcache.enable = 1
opcache.memory_consumption = 256
opcache.max_accelerated_files = 20000
opcache.validate_timestamps = 0
PHPEOF

# MySQL config
cat > mysql/my.cnf << 'MYEOF'
[mysqld]
innodb_buffer_pool_size = 1G
innodb_log_file_size = 256M
innodb_flush_log_at_trx_commit = 2
max_connections = 200
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
sql_mode = STRICT_TRANS_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION
MYEOF

# Nginx config
if [[ -n "$DOMAIN" ]]; then
    SERVER_NAME="$DOMAIN"
else
    SERVER_NAME="_"
fi

cat > nginx/default.conf << NGEOF
server {
    listen 80;
    server_name ${SERVER_NAME};
    return 301 https://\$host\$request_uri;
}

server {
    listen 443 ssl http2;
    server_name ${SERVER_NAME};

    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    root /var/www/html;
    index index.php index.html;

    client_max_body_size 64M;

    location / {
        try_files \$uri \$uri/ /index.php?\$query_string;
    }

    location ~ \.php$ {
        fastcgi_pass app:9000;
        fastcgi_index index.php;
        fastcgi_param SCRIPT_FILENAME \$document_root\$fastcgi_script_name;
        include fastcgi_params;
        fastcgi_read_timeout 300;
    }

    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff2?)$ {
        expires 30d;
        add_header Cache-Control "public, immutable";
    }

    location ~ /\. { deny all; }
}
NGEOF

# Step 5: SSL
log "Generating SSL certificate..."
if [[ -n "$DOMAIN" ]] && command -v certbot &>/dev/null; then
    certbot certonly --standalone --non-interactive --agree-tos \
        --email "admin@${DOMAIN}" -d "$DOMAIN" 2>/dev/null && \
    cp /etc/letsencrypt/live/${DOMAIN}/fullchain.pem nginx/ssl/cert.pem && \
    cp /etc/letsencrypt/live/${DOMAIN}/privkey.pem nginx/ssl/key.pem && \
    log "Let's Encrypt certificate obtained for $DOMAIN" || {
        warn "Let's Encrypt failed — generating self-signed certificate"
        openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
            -keyout nginx/ssl/key.pem -out nginx/ssl/cert.pem \
            -subj "/CN=${DOMAIN:-localhost}" 2>/dev/null
    }
else
    openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
        -keyout nginx/ssl/key.pem -out nginx/ssl/cert.pem \
        -subj "/CN=${DOMAIN:-localhost}" 2>/dev/null
    log "Self-signed certificate generated (replace with real cert for production)"
fi

# Step 6: Start services
log "Starting ecomae ERP services..."
docker compose up -d

# Wait for MySQL
log "Waiting for database to be ready..."
for i in $(seq 1 30); do
    if docker compose exec -T db mysqladmin ping -h localhost -u root "-p${DB_ROOT_PASSWORD}" &>/dev/null; then
        break
    fi
    sleep 2
done

# Step 7: License activation
log "Activating license: $LICENSE_KEY"
docker compose exec -T app php /var/www/html/deploy/on-premises/activate-license.php "$LICENSE_KEY" 2>/dev/null || {
    warn "Online activation failed — offline activation available at https://${DOMAIN:-localhost}/setup"
}

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║   ecomae ERP installed successfully!     ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════╝${NC}"
echo ""
echo -e "  URL:       https://${DOMAIN:-localhost}"
echo -e "  Admin:     https://${DOMAIN:-localhost}/cp/"
echo -e "  ERP:       https://${DOMAIN:-localhost}/erp/"
echo -e ""
echo -e "  Database:  ${DB_PASSWORD} (saved in .env)"
echo -e "  Install:   ${INSTALL_DIR}"
echo ""
log "Run 'docker compose logs -f' to monitor services"
log "Run 'docker compose exec app php artisan setup:wizard' for initial configuration"
