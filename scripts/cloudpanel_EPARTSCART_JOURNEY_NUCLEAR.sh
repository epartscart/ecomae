#!/usr/bin/env bash
# NUCLEAR one-shot: epartscart customer journey must leave RESULT=PASS or exit 1.
# Previous operator acks left register 404 / unbound shop DB / php-reference Archive paused.
#
# Paste as root (git-based — do NOT use curl|bash alone):
#   cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
#   git fetch origin main
#   git checkout -f main
#   git reset --hard origin/main
#   export ECOMAE_BRANCH=main ECOMAE_SKIP_LIFEOS_MP4=YES
#   bash scripts/cloudpanel_EPARTSCART_JOURNEY_NUCLEAR.sh 2>&1 | tee /root/epartscart-journey-nuclear.log
#   grep -E 'RESULT=|PREFLIGHT|GATE_|ERROR|SHA=' /root/epartscart-journey-nuclear.log | tail -80
#
# #967 merged to main but :5100 was never republished — register-app stays 404 until RESULT=PASS.
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
export ECOMAE_BRANCH ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }

printf '======== EPARTSCART JOURNEY NUCLEAR (%s) ========\n' "$ECOMAE_BRANCH"
[[ "$(id -u)" -eq 0 ]] || die "must run as root"

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  [[ -n "$d" && -d "$d/.git" ]] && REPO="$d" && break
done
[[ -n "$REPO" ]] || die "repo not found under /opt or /root"

cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$ECOMAE_BRANCH" || die "git fetch failed"
git checkout -f "$ECOMAE_BRANCH" || die "git checkout failed"
git reset --hard "origin/$ECOMAE_BRANCH" || die "git reset failed"
SHA="$(git rev-parse --short HEAD)"
FULL="$(git rev-parse HEAD)"
printf 'REPO=%s SHA=%s FULL=%s\n' "$REPO" "$SHA" "$FULL"

[[ -f aspnet/src/EcomAE.Platform/Components/Pages/StorefrontRegisterApp.razor ]] \
  || die "StorefrontRegisterApp.razor missing — wrong tree"
grep -q "IFNULL(TRIM(\`db_name\`), '') <> ''" aspnet/src/EcomAE.Platform/Data/PortalTenantSql.cs \
  || die "PortalTenantSql shop-db preference missing — wrong tree"
[[ -f aspnet/src/EcomAE.Platform/Components/Shared/PhpOAuthLoginButtons.razor ]] \
  || die "PhpOAuthLoginButtons.razor missing — merge #969 not on tree"
[[ -f aspnet/src/EcomAE.Platform/Storefront/StorefrontPriceAccess.cs ]] \
  || die "StorefrontPriceAccess.cs missing — merge #970 not on tree"
printf 'PREFLIGHT_OK SHA=%s (journey+autofill+price)\n' "$SHA"

chmod +x scripts/cloudpanel_EPARTSCART_CUSTOMER_JOURNEY_RECOVER.sh \
  scripts/cloudpanel_FORCE_LIVE_NOW.sh \
  scripts/cloudpanel_restore_php_reference_serving.sh \
  scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh 2>/dev/null || true

# Force PreferAspNet so chrome uses /storefront/register-app after republish.
ENV_FILE=/etc/ecomae-aspnet/platform.env
if [[ -f "$ENV_FILE" ]]; then
  cp -a "$ENV_FILE" "${ENV_FILE}.bak.journey-nuclear.$(date +%Y%m%d%H%M%S)"
  python3 - <<'PY'
from pathlib import Path
p = Path("/etc/ecomae-aspnet/platform.env")
keys = {
  "EcomAE__PhpReference__PreferAspNetStorefrontApps": "true",
  "EcomAE__PhpReference__KeepPhpProjectAvailable": "true",
  "EcomAE__PhpReference__Mode": "aspnet-primary-php-reference",
}
lines = p.read_text().splitlines()
out, seen = [], set()
for line in lines:
    if not line.strip() or line.lstrip().startswith("#") or "=" not in line:
        out.append(line); continue
    k = line.split("=", 1)[0].strip()
    if k in keys:
        out.append(f"{k}={keys[k]}"); seen.add(k)
    else:
        out.append(line)
for k, v in keys.items():
    if k not in seen:
        out.append(f"{k}={v}")
p.write_text("\n".join(out) + "\n")
print("platform.env PreferAspNetStorefrontApps=true")
PY
fi

# Delegate to hardened recover (tenant db + FORCE_LIVE + restore php-ref + prove).
set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_EPARTSCART_CUSTOMER_JOURNEY_RECOVER.sh 2>&1 | tee /root/epartscart-journey-nuclear-inner.log
RC=${PIPESTATUS[0]}
set -e
printf 'inner_recover_exit=%s\n' "$RC"

# Final public hard gates (must all pass).
fail=0
gate() {
  local url="$1" re="$2"
  local code body
  body="$(mktemp)"
  code="$(curl -sS -o "$body" -w '%{http_code}' --max-time 35 -k -A 'journey-nuclear' "$url" || echo 000)"
  if [[ "$code" == "200" ]] && grep -qiE "$re" "$body"; then
    printf 'GATE_OK  %s %s\n' "$code" "$url"
  else
    printf 'GATE_BAD %s %s (need ~ %s)\n' "$code" "$url" "$re"
    head -c 160 "$body"; echo
    fail=$((fail + 1))
  fi
  rm -f "$body"
}

gate "https://www.epartscart.com/storefront/register-app" "Create your account"
gate "https://www.epartscart.com/en/users/registration" "Create your account|Register"
gate "https://www.epartscart.com/en/users/login" "Customer login"
gate "https://www.epartscart.com/" "register-app|Create an account"
gate "https://www.epartscart.com/cp/login" "Continue with Google"
gate "https://www.epartscart.com/storefront/login" "Continue with Google"
gate "https://www.epartscart.com/storefront/search-app?article=OC90" "data-prices-visible=\"0\"|epc-sf-price-gate|to see prices"

B="$(mktemp)"
BC="$(curl -sS -o "$B" -w '%{http_code}' --max-time 30 -k \
  'https://www.epartscart.com/storefront/search-bunches?article=OC90' || echo 000)"
if [[ "$BC" == "200" ]] && ! grep -q 'Tenant shop database is not bound' "$B"; then
  printf 'GATE_OK  %s search-bunches bound\n' "$BC"
else
  printf 'GATE_BAD %s search-bunches still unbound\n' "$BC"
  head -c 200 "$B"; echo
  fail=$((fail + 1))
fi
rm -f "$B"

PR="$(mktemp)"
PC="$(curl -sS -o "$PR" -w '%{http_code}' --max-time 30 -k \
  'https://www.epartscart.com/php-reference/en/users/registration' || echo 000)"
if [[ "$PC" == "200" ]] && ! grep -q 'Archive paused' "$PR"; then
  printf 'GATE_OK  %s php-reference registration\n' "$PC"
else
  printf 'GATE_BAD %s php-reference still paused/failed\n' "$PC"
  head -c 120 "$PR"; echo
  fail=$((fail + 1))
fi
rm -f "$PR"

printf 'SHA=%s inner_rc=%s gate_fails=%s\n' "$SHA" "$RC" "$fail"
if [[ "$fail" -gt 0 || "$RC" -ne 0 ]]; then
  die "gates_failed=$fail inner_rc=$RC — send /root/epartscart-journey-nuclear.log"
fi
printf 'RESULT=PASS nuclear journey recover SHA=%s\n' "$SHA"
exit 0
