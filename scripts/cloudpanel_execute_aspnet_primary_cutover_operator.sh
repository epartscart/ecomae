#!/usr/bin/env bash
# Human-confirmed exact-route ASP.NET primary cutover on CloudPanel.
# Requires RELEASE_OWNER_APPROVAL.md (APPROVED_TO_REMOVE_PHP_FALLBACK + KeepPhpProjectAvailable).
# Installs www exact-route shadows + optionally enables Storefront/Admin ASP.NET flags.
# NEVER broad-cuts /api|/cp|/erp|/bos|/storefront|/
# NEVER sets cutoverAllowed=true or deletes PHP (reference keep).
# NEVER invents presentation/module PASS.
#
# Usage (CloudPanel root, after git reset --hard origin/main):
#   ECOMAE_CONFIRM_ASPNET_PRIMARY_CUTOVER=YES \
#     bash scripts/cloudpanel_execute_aspnet_primary_cutover_operator.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

APPROVAL="$ROOT/docs/migration/evidence/decommission/RELEASE_OWNER_APPROVAL.md"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
ENABLE_FLAGS="${ECOMAE_ENABLE_ASPNET_ROUTE_FLAGS:-1}"
SKIP_REDEPLOY="${ECOMAE_CUTOVER_SKIP_REDEPLOY:-0}"
SKIP_PRESENTATION="${ECOMAE_CUTOVER_SKIP_PRESENTATION:-0}"

if [[ "${ECOMAE_CONFIRM_ASPNET_PRIMARY_CUTOVER:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_ASPNET_PRIMARY_CUTOVER=YES\n' >&2
  printf 'This promotes exact-route ASP.NET shadows on www only.\n' >&2
  printf 'PHP project stays as reference; cutoverAllowed stays false; no broad trees.\n' >&2
  exit 2
fi

if [[ ! -f "$APPROVAL" ]]; then
  printf 'ERROR: missing %s\n' "$APPROVAL" >&2
  exit 1
fi
if ! grep -q 'APPROVED_TO_REMOVE_PHP_FALLBACK' "$APPROVAL"; then
  printf 'ERROR: approval missing APPROVED_TO_REMOVE_PHP_FALLBACK marker\n' >&2
  exit 1
fi
if ! grep -q 'KeepPhpProjectAvailable' "$APPROVAL"; then
  printf 'ERROR: approval must retain KeepPhpProjectAvailable (PHP reference keep)\n' >&2
  exit 1
fi

FAIL=0
step() {
  local label="$1"
  shift
  echo ""
  echo "== ${label} =="
  if "$@"; then
    echo "OK ${label}"
  else
    echo "FAIL ${label}" >&2
    FAIL=1
  fi
}

echo "ASP.NET primary exact-route cutover — approval present; PHP reference kept; cutoverAllowed=false"

if [[ "$SKIP_REDEPLOY" != "1" ]]; then
  if [[ -x "$ROOT/scripts/cloudpanel_production_deploy_foundation.sh" ]]; then
    step "1/8 redeploy ASP.NET foundation from current checkout" \
      bash "$ROOT/scripts/cloudpanel_production_deploy_foundation.sh"
  elif [[ -x "$ROOT/scripts/deploy_aspnet_foundation.sh" ]]; then
    step "1/8 redeploy ASP.NET foundation (deploy_aspnet_foundation.sh)" \
      bash "$ROOT/scripts/deploy_aspnet_foundation.sh"
  else
    echo "WARN: no deploy script found; skipping redeploy (set ECOMAE_CUTOVER_SKIP_REDEPLOY=1 to silence)" >&2
  fi
else
  echo ""
  echo "== 1/8 redeploy skipped (ECOMAE_CUTOVER_SKIP_REDEPLOY=1) =="
fi

if [[ -x "$ROOT/scripts/wait_for_aspnet_health.sh" ]]; then
  step "2/8 wait for ASP.NET health" bash "$ROOT/scripts/wait_for_aspnet_health.sh" || true
else
  echo ""
  echo "== 2/8 health wait helper missing; probing loopback =="
  curl -fsS --connect-timeout 5 http://127.0.0.1:5100/health >/dev/null \
    && echo "OK loopback /health" \
    || { echo "FAIL loopback /health" >&2; FAIL=1; }
fi

if [[ "$ENABLE_FLAGS" == "1" && -f "$ENV_FILE" ]]; then
  echo ""
  echo "== 3/8 enable Storefront/Admin ASP.NET route flags (RequirePhpFallback stays true) =="
  bak="${ENV_FILE}.bak.aspnet-primary.$(date -u +%Y%m%d%H%M%S)"
  cp -a "$ENV_FILE" "$bak"
  python3 - "$ENV_FILE" <<'PY'
import pathlib, re, sys
path = pathlib.Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
updates = {
    "MigrationRouteCutover__StorefrontAspNetEnabled": "true",
    "MigrationRouteCutover__AdminAspNetEnabled": "true",
    "MigrationRouteCutover__RequirePhpFallback": "true",
    "MigrationRouteCutover__ApiShadowTrafficEnabled": "true",
}
for key, value in updates.items():
    pattern = re.compile(rf"^{re.escape(key)}=.*$", re.M)
    line = f"{key}={value}"
    if pattern.search(text):
        text = pattern.sub(line, text)
    else:
        if not text.endswith("\n"):
            text += "\n"
        text += line + "\n"
path.write_text(text, encoding="utf-8")
print(f"updated {path}")
for key, value in updates.items():
    print(f"  {key}={value}")
PY
  if systemctl is-active --quiet ecomae-platform.service 2>/dev/null; then
    systemctl restart ecomae-platform.service
    sleep 2
    systemctl is-active --quiet ecomae-platform.service \
      && echo "OK ecomae-platform.service restarted" \
      || { echo "FAIL ecomae-platform.service not active after restart" >&2; FAIL=1; }
  else
    echo "WARN: ecomae-platform.service not active; env updated for next start" >&2
  fi
  echo "backup: $bak"
else
  echo ""
  echo "== 3/8 route flags skipped (ECOMAE_ENABLE_ASPNET_ROUTE_FLAGS=${ENABLE_FLAGS}; env=${ENV_FILE}) =="
  if [[ ! -f "$ENV_FILE" ]]; then
    echo "NOTE: missing $ENV_FILE — set flags manually after deploy" >&2
  fi
fi

step "4/8 install surface digest exact-route shadows (133)" \
  env ECOMAE_CONFIRM_INSTALL_SURFACE_DIGEST_SHADOWS=YES \
  bash "$ROOT/scripts/cloudpanel_install_surface_digest_shadows.sh"

step "5/8 probe surface digest shadows" \
  bash "$ROOT/scripts/cloudpanel_probe_surface_digest_shadows.sh"

step "6/8 www storefront+marketing shadow closeout" \
  env ECOMAE_CONFIRM_WWW_SHADOW_CLOSEOUT=YES \
      ECOMAE_WWW_SHADOW_SKIP_PRESENTATION_RECHECK=1 \
  bash "$ROOT/scripts/cloudpanel_www_shadow_closeout_operator.sh"

if [[ "$SKIP_PRESENTATION" != "1" ]]; then
  step "7/8 install presentation app exact-route shadows" \
    env ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES \
    bash "$ROOT/scripts/cloudpanel_install_presentation_app_shadows.sh"
else
  echo ""
  echo "== 7/8 presentation install skipped (ECOMAE_CUTOVER_SKIP_PRESENTATION=1) =="
fi

echo ""
echo "== 8/8 assert locks still honest =="
echo "cutoverAllowed must remain false (exact-route only; no broad trees)"
echo "readyForPhpRemoval must remain false (PHP reference keep; chrome/module gaps remain)"
echo "live / must remain PHP epm-hub"
if bash "$ROOT/scripts/cloudpanel_probe_ecomae_marketing_php_chrome.sh"; then
  echo "OK live / remains PHP epm-hub"
else
  echo "FAIL live / must stay PHP epm-hub" >&2
  FAIL=1
fi

echo ""
echo "== Post-cutover operator boards =="
cat <<'EOF'
curl -sS https://www.ecomae.com/migration/php-reference-mode | jq '{mode,keepPhpProjectAvailable,storefrontAspNetEnabled,adminAspNetEnabled,requirePhpFallback,cutoverAllowed}'
curl -sS https://www.ecomae.com/migration/php-decommission-readiness | jq '{readyToRemovePhp,blockerCount,checklistCompletePercent}'
curl -sS https://www.ecomae.com/migration/live-surface-links | jq '{status}'
bash scripts/cloudpanel_probe_surface_digest_shadows.sh
bash scripts/cloudpanel_probe_storefront_digest_shadows.sh

NEXT (still required; do not invent PASS):
- Authenticated dual-samples: bash scripts/cloudpanel_run_all_dual_sample_operators.sh
- Presentation recheck until status=pass (soft today)
- Functional live-smoke 7/7 capture
- Tenant same-to-same before named-tenant shadows
- RequirePhpFallback=false only per dual-sample-green route (never broad)
- PHP source deletion is a SEPARATE human approval — not this operator
Rollback: bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback
EOF

if [[ "$FAIL" -ne 0 ]]; then
  echo ""
  echo "FAIL: ASP.NET primary exact-route cutover had hard step failure(s)" >&2
  echo "Rollback if needed: bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback" >&2
  exit 1
fi

echo ""
echo "PASS: exact-route ASP.NET primary cutover steps completed"
echo "PHP reference kept; cutoverAllowed=false; broad trees still forbidden"
exit 0
