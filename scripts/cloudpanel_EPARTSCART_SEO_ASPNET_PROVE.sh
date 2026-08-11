#!/usr/bin/env bash
# Prove ePartsCart ASP.NET-primary SEO matches PHP epc_seo_indexing signals.
# Usage (CloudPanel root — do NOT run as bash scripts/... from ~):
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/epartscart-seo-aspnet-parity-7b3b/scripts/cloudpanel_EPARTSCART_SEO_ASPNET_PROVE.sh'
#   TMP=/tmp/epartscart-seo-aspnet-prove.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP" && bash "$TMP" 2>&1 | tee /root/epartscart-seo-aspnet-prove.log
set -euo pipefail

HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
CHPU="${ECOMAE_SEO_CHPU:-/en/parts/JS%20ASAKASHI/C110J}"
UA="${ECOMAE_CURL_UA:-Mozilla/5.0 (compatible; EcomAE-SEO-Prove/1.0)}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }
has() { grep -Eiq -- "$1" "$2" 2>/dev/null; }

note "======== EPARTSCART SEO ASP.NET PROVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo local)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"

curl -sS -A "$UA" -o /tmp/epc_seo_home.html -D /tmp/epc_seo_home.hdr --max-time 45 "${BASE}/" || true
curl -sS -A "$UA" -o /tmp/epc_seo_chpu.html -D /tmp/epc_seo_chpu.hdr --max-time 45 "${BASE}${CHPU}" || true
tr -d '\r' < /tmp/epc_seo_home.hdr > /tmp/epc_seo_home.hdr.c 2>/dev/null || true
tr -d '\r' < /tmp/epc_seo_chpu.hdr > /tmp/epc_seo_chpu.hdr.c 2>/dev/null || true
mv -f /tmp/epc_seo_home.hdr.c /tmp/epc_seo_home.hdr 2>/dev/null || true
mv -f /tmp/epc_seo_chpu.hdr.c /tmp/epc_seo_chpu.hdr 2>/dev/null || true

has 'x-ecomae-platform:[[:space:]]*primary' /tmp/epc_seo_home.hdr && ok "HOME_PRIMARY=YES" || fail "HOME_PRIMARY=NO"
has 'x-ecomae-platform:[[:space:]]*primary' /tmp/epc_seo_chpu.hdr && ok "CHPU_PRIMARY=YES" || fail "CHPU_PRIMARY=NO"

has 'rel="canonical"' /tmp/epc_seo_home.html && ok "HOME_CANONICAL=YES" || fail "HOME_CANONICAL=NO"
has 'name="description"' /tmp/epc_seo_home.html && ok "HOME_DESCRIPTION=YES" || fail "HOME_DESCRIPTION=NO"
has 'application/ld\+json' /tmp/epc_seo_home.html && ok "HOME_JSONLD=YES" || fail "HOME_JSONLD=NO"
has 'hreflang="x-default"' /tmp/epc_seo_home.html && ok "HOME_HREFLANG=YES" || fail "HOME_HREFLANG=NO"

has 'Part number' /tmp/epc_seo_chpu.html && ok "CHPU_TITLE_PART_NUMBER=YES" || fail "CHPU_TITLE_PART_NUMBER=NO"
has 'Part number / article:' /tmp/epc_seo_chpu.html && ok "CHPU_DESC_PHP=YES" || fail "CHPU_DESC_PHP=NO"
has 'rel="canonical"' /tmp/epc_seo_chpu.html && ok "CHPU_CANONICAL=YES" || fail "CHPU_CANONICAL=NO"
has 'application/ld\+json' /tmp/epc_seo_chpu.html && ok "CHPU_JSONLD=YES" || fail "CHPU_JSONLD=NO"
has 'shippingDetails' /tmp/epc_seo_chpu.html && ok "CHPU_SHIPPING_SCHEMA=YES" || fail "CHPU_SHIPPING_SCHEMA=NO"
has 'hreflang="en-SA"' /tmp/epc_seo_chpu.html && ok "CHPU_HREFLANG_SA=YES" || fail "CHPU_HREFLANG_SA=NO"
has 'msvalidate\.01' /tmp/epc_seo_chpu.html && ok "CHPU_BING=YES" || fail "CHPU_BING=NO"
if has 'ajax_epc_cross_search\.php|product\.php|dp_product\.php' /tmp/epc_seo_chpu.html; then
  fail "CHPU_NO_PRODUCT_PHP=NO"
else
  ok "CHPU_NO_PRODUCT_PHP=YES"
fi

# sitemap.xml should redirect or serve index
sm_code=$(curl -sS -A "$UA" -o /tmp/epc_seo_sm.html -w '%{http_code}' --max-time 30 "${BASE}/sitemap.xml" || echo 000)
note "SITEMAP_HTTP=${sm_code}"
[[ "$sm_code" == "200" || "$sm_code" == "301" || "$sm_code" == "302" ]] && ok "SITEMAP_REACHABLE=YES" || fail "SITEMAP_REACHABLE=NO code=${sm_code}"

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS EPARTSCART_SEO_ASPNET=YES CHPU=${CHPU}"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above"
exit 1
