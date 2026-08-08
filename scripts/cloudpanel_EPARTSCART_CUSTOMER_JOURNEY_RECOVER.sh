#!/usr/bin/env bash
# Recover ePartsCart customer journey: search (tenant shop DB), register/login,
# cart→checkout→orders, and PHP reference writes under /php-reference/*.
#
# CloudPanel root paste:
#   ECOMAE_BRANCH=cursor/epartscart-customer-journey-parity-7b3b ECOMAE_SKIP_LIFEOS_MP4=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/epartscart-customer-journey-parity-7b3b/scripts/cloudpanel_EPARTSCART_CUSTOMER_JOURNEY_RECOVER.sh)" \
#     2>&1 | tee /root/epartscart-journey-recover.log
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/epartscart-customer-journey-parity-7b3b}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
printf '======== EPARTSCART CUSTOMER JOURNEY RECOVER (%s) ========\n' "$ECOMAE_BRANCH"
if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
[[ -n "$REPO" ]] || { printf 'ERROR: repo not found\n' >&2; exit 1; }

cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
printf 'REPO=%s SHA=%s\n' "$REPO" "$SHA"

chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh \
  scripts/cloudpanel_restore_php_reference_serving.sh \
  scripts/cloudpanel_fix_warmup_splash_storefront_loop.sh 2>/dev/null || true

printf '\n---- [1] FORCE_LIVE_NOW (republish :5100 with tenant SQL fix) ----\n'
set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/epartscart-journey-inner.log
FORCE_RC=${PIPESTATUS[0]}
set -e
printf 'FORCE_LIVE_NOW exit=%s\n' "$FORCE_RC"

printf '\n---- [2] Restore /php-reference/* (registration + checkout writes) ----\n'
set +e
if [[ -x scripts/cloudpanel_restore_php_reference_serving.sh ]]; then
  bash scripts/cloudpanel_restore_php_reference_serving.sh 2>&1 | tee -a /root/epartscart-journey-inner.log
fi
set -e

printf '\n---- [3] Warmup splash loop fix ----\n'
set +e
if [[ -x scripts/cloudpanel_fix_warmup_splash_storefront_loop.sh ]]; then
  ECOMAE_CONFIRM_FIX_WARMUP_SPLASH_LOOP=YES \
    bash scripts/cloudpanel_fix_warmup_splash_storefront_loop.sh 2>&1 | tee -a /root/epartscart-journey-inner.log
fi
set -e

printf '\n---- [4] Journey prove ----\n'
PROBE=/root/epartscart-journey-probe.txt
: > "$PROBE"
bad=0
prove() {
  local url="$1" want="$2"
  local code body
  body="$(mktemp)"
  code="$(curl -sS -o "$body" -w '%{http_code}' --max-time 30 -k -A 'journey-recover' "$url" || echo 000)"
  local ok=0
  if [[ "$code" == "200" ]] && grep -qiE "$want" "$body"; then ok=1; fi
  if [[ "$ok" -eq 1 ]]; then
    printf 'OK  %s %s\n' "$code" "$url" | tee -a "$PROBE"
  else
    printf 'BAD %s %s (want ~ %s)\n' "$code" "$url" "$want" | tee -a "$PROBE"
    bad=$((bad + 1))
  fi
  rm -f "$body"
}

prove "https://www.epartscart.com/" "epc-nero|Catalog of products"
prove "https://www.epartscart.com/storefront/register-app" "Create your account|registration"
prove "https://www.epartscart.com/en/users/registration" "Create your account|registration"
prove "https://www.epartscart.com/en/users/login" "Customer login"
prove "https://www.epartscart.com/storefront/search-app?article=OC90" "Part search"
prove "https://www.epartscart.com/storefront/cart-app" "Cart"
prove "https://www.epartscart.com/storefront/checkout-app" "Checkout"
prove "https://www.epartscart.com/storefront/orders-app" "Orders"
prove "https://www.epartscart.com/storefront/own-catalog-app" "catalog|Catalogue|Own"

# Bunches must not say unbound tenant after SQL prefer-db_name fix.
BUNCH_BODY="$(mktemp)"
BUNCH_CODE="$(curl -sS -o "$BUNCH_BODY" -w '%{http_code}' --max-time 30 -k \
  'https://www.epartscart.com/storefront/search-bunches?article=OC90' || echo 000)"
if [[ "$BUNCH_CODE" == "200" ]] && ! grep -q 'Tenant shop database is not bound' "$BUNCH_BODY"; then
  printf 'OK  %s search-bunches (tenant shop bound)\n' "$BUNCH_CODE" | tee -a "$PROBE"
else
  printf 'BAD %s search-bunches still unbound or failed\n' "$BUNCH_CODE" | tee -a "$PROBE"
  head -c 300 "$BUNCH_BODY" | tee -a "$PROBE"; echo | tee -a "$PROBE"
  bad=$((bad + 1))
fi
rm -f "$BUNCH_BODY"

printf '\n======== JOURNEY RECOVER DONE SHA=%s bad=%s ========\n' "$SHA" "$bad"
printf 'Probe: %s\n' "$PROBE"
if [[ "$bad" -gt 0 ]]; then
  printf 'RESULT=FAIL\n' >&2
  exit 1
fi
printf 'RESULT=PASS\n'
exit 0
