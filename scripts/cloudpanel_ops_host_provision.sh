#!/usr/bin/env bash
# Provision a tenant operations host: {key}-operations-{ccc}.ecomae.com
# (D365-style dedicated host; ErpOperationsHost binds the tenant from it).
#
# Usage: bash scripts/cloudpanel_ops_host_provision.sh <site_key> [country3]
#   e.g. bash scripts/cloudpanel_ops_host_provision.sh abcd usa
#
# Creates /etc/nginx/sites-enabled/<key>-ops-<ccc>.ecomae.com.conf:
#   - :80  .well-known passthrough (Let's Encrypt HTTP-01) + https redirect
#   - :443 proxy /erp (and bare /) to the ASP.NET platform on 127.0.0.1:5100;
#     every other path is refused — the host is tenant-bound, so unrelated
#     surfaces (CP, BOS, storefront) must not be reachable through it.
# Issues a free Let's Encrypt cert via webroot. Idempotent — safe to re-run.
set -euo pipefail

KEY="${1:?usage: $0 <site_key> [country3]}"
COUNTRY="${2:-uae}"
HOST="${KEY}-operations-${COUNTRY}.ecomae.com"
CONF="/etc/nginx/sites-enabled/${KEY}-ops-${COUNTRY}.ecomae.com.conf"
WEBROOT="/home/ecomae/htdocs/www.ecomae.com"

if ! [[ "$KEY" =~ ^[a-z0-9]+$ ]]; then
  echo "site key must be [a-z0-9] only (got '$KEY')" >&2; exit 2
fi
if ! [[ "$COUNTRY" =~ ^[a-z]{3}$ ]]; then
  echo "country must be ISO alpha-3 (got '$COUNTRY')" >&2; exit 2
fi

mkdir -p "$WEBROOT/.well-known/acme-challenge"

# Roll back on `nginx -t` failure — a failed write must not leave a
# `listen 443` marker that a re-run would trust.
cat > "${CONF}.new" <<EOF
server {
  listen 80;
  listen [::]:80;
  server_name $HOST;
  root $WEBROOT;
  location ~ /.well-known { auth_basic off; allow all; }
  location / { return 301 https://\$host\$request_uri; }
}
EOF

# Port-80 block first (needed for the ACME challenge), idempotent.
if ! grep -q "server_name $HOST" "$CONF" 2>/dev/null; then
  mv "${CONF}.new" "$CONF"
  if ! nginx -t; then
    echo "nginx -t failed after writing $CONF — rolling back" >&2
    rm -f "$CONF"
    nginx -t || true
    exit 1
  fi
  systemctl reload nginx
else
  rm -f "${CONF}.new"
fi

# Issue the origin cert (Cloudflare strict TLS needs a valid origin cert).
if [ ! -f "/etc/letsencrypt/live/$HOST/fullchain.pem" ]; then
  certbot certonly --webroot -w "$WEBROOT" -d "$HOST" \
    --non-interactive --agree-tos -m admin@ecomae.com
fi

# Append the TLS block once the cert exists. Roll back the append if `nginx -t`
# fails so the `listen 443` marker never survives a broken write.
if ! grep -q "listen 443" "$CONF"; then
  cp "$CONF" "${CONF}.pre-tls"
  cat >> "$CONF" <<EOF
server {
  listen 443 ssl;
  listen [::]:443 ssl;
  http2 on;
  server_name $HOST;
  ssl_certificate /etc/letsencrypt/live/$HOST/fullchain.pem;
  ssl_certificate_key /etc/letsencrypt/live/$HOST/privkey.pem;
  # Tenant-bound host: only the ERP surface and its own assets are proxied —
  # CP / BOS / storefront / other surfaces are refused at the edge.
  location = / {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Host \$host;
    proxy_set_header X-Forwarded-Proto \$scheme;
    proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
    proxy_set_header Cookie \$http_cookie;
    proxy_set_header X-EcomAE-Route-Cutover $KEY-ops-host;
  }
  location ~ ^/(erp|css|assets|_framework|_blazor|_content)(/|$) {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Upgrade \$http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host \$host;
    proxy_set_header X-Forwarded-Proto \$scheme;
    proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
    proxy_set_header Cookie \$http_cookie;
    proxy_set_header X-EcomAE-Route-Cutover $KEY-ops-host;
  }
  location / { return 404; }
}
EOF
  if ! nginx -t; then
    echo "nginx -t failed after TLS append — restoring pre-TLS config" >&2
    mv "${CONF}.pre-tls" "$CONF"
    nginx -t || true
    exit 1
  fi
  rm -f "${CONF}.pre-tls"
  systemctl reload nginx
fi

echo "OK — https://$HOST/erp"
