#!/usr/bin/env bash
# Provision a tenant operations host: {key}-operations-{ccc}.ecomae.com
# (D365-style dedicated host; ErpOperationsHost binds the tenant from it).
#
# Usage: bash scripts/cloudpanel_ops_host_provision.sh <site_key> [country3]
#   e.g. bash scripts/cloudpanel_ops_host_provision.sh abcd usa
#
# Creates /etc/nginx/sites-enabled/<key>-ops.ecomae.com.conf:
#   - :80  .well-known passthrough (Let's Encrypt HTTP-01) + https redirect
#   - :443 proxy to the ASP.NET platform on 127.0.0.1:5100
# Issues a free Let's Encrypt cert via webroot. Idempotent — safe to re-run.
set -euo pipefail

KEY="${1:?usage: $0 <site_key> [country3]}"
COUNTRY="${2:-uae}"
HOST="${KEY}-operations-${COUNTRY}.ecomae.com"
CONF="/etc/nginx/sites-enabled/${KEY}-ops.ecomae.com.conf"
WEBROOT="/home/ecomae/htdocs/www.ecomae.com"

if ! [[ "$KEY" =~ ^[a-z0-9]+$ ]]; then
  echo "site key must be [a-z0-9] only (got '$KEY')" >&2; exit 2
fi
if ! [[ "$COUNTRY" =~ ^[a-z]{3}$ ]]; then
  echo "country must be ISO alpha-3 (got '$COUNTRY')" >&2; exit 2
fi

mkdir -p "$WEBROOT/.well-known/acme-challenge"

# Port-80 block first (needed for the ACME challenge), idempotent.
if ! grep -q "listen 443" "$CONF" 2>/dev/null; then
  cat > "$CONF" <<EOF
server {
  listen 80;
  listen [::]:80;
  server_name $HOST;
  root $WEBROOT;
  location ~ /.well-known { auth_basic off; allow all; }
  location / { return 301 https://\$host\$request_uri; }
}
EOF
  nginx -t && systemctl reload nginx
fi

# Issue the origin cert (Cloudflare strict TLS needs a valid origin cert).
if [ ! -f "/etc/letsencrypt/live/$HOST/fullchain.pem" ]; then
  certbot certonly --webroot -w "$WEBROOT" -d "$HOST" \
    --non-interactive --agree-tos -m admin@ecomae.com
fi

# Append the TLS block once the cert exists.
if ! grep -q "listen 443" "$CONF"; then
  cat >> "$CONF" <<EOF
server {
  listen 443 ssl;
  listen [::]:443 ssl;
  http2 on;
  server_name $HOST;
  ssl_certificate /etc/letsencrypt/live/$HOST/fullchain.pem;
  ssl_certificate_key /etc/letsencrypt/live/$HOST/privkey.pem;
  location / {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Host \$host;
    proxy_set_header X-Forwarded-Proto \$scheme;
    proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
    proxy_set_header Cookie \$http_cookie;
    proxy_set_header X-EcomAE-Route-Cutover $KEY-ops-host;
  }
}
EOF
  nginx -t && systemctl reload nginx
fi

echo "OK — https://$HOST/erp"
