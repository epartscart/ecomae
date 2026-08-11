#!/usr/bin/env bash
# Cut product PHP front-controllers on www.ecomae.com — ASP.NET only.
# PHP remains installed under /php-reference/* for compare (cutoverAllowed=false).
#
# CloudPanel root paste:
#   ECOMAE_BRANCH=cursor/no-product-php-urls-marketing-7b3b \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/no-product-php-urls-marketing-7b3b/scripts/cloudpanel_NO_PRODUCT_PHP_URLS_WWW_NOW.sh)" \
#     2>&1 | tee /root/no-product-php-urls-www.log
#
# After merge:
#   ECOMAE_BRANCH=main bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh)" \
#     2>&1 | tee /root/force-live-www-marketing.log
set -euo pipefail

if [[ "${ECOMAE_CONFIRM_NO_PRODUCT_PHP_URLS_WWW_NOW:-}" != "YES" ]]; then
  printf 'REFUSE: set ECOMAE_CONFIRM_NO_PRODUCT_PHP_URLS_WWW_NOW=YES\n' >&2
  exit 2
fi

ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/no-product-php-urls-marketing-7b3b}"
export ECOMAE_BRANCH
export ECOMAE_CONFIRM_STOP_PRODUCT_PHP_NOW=YES

exec bash "$(dirname "$0")/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh"
