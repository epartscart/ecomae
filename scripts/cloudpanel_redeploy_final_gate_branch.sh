#!/usr/bin/env bash
# Paste-safe: deploy main (PR #603 smoke unlock is merged) then capture final-gate artifacts.
#
# On CloudPanel as root, copy this ENTIRE block:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_redeploy_final_gate_branch.sh)"
#
# Or from an existing checkout:
#   bash scripts/cloudpanel_redeploy_final_gate_branch.sh
#
# Never removes PHP. Never invents API keys/cookies — you must set them in platform.env.
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
REPO="${ECOMAE_REPO:-/opt/ecomae-aspnet-source}"
ECOMAE_ASPNET_ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
ENV_FILE="${ECOMAE_ASPNET_ENV_DIR}/platform.env"

printf '== CloudPanel redeploy FINAL-GATE branch ==\n'
printf 'Branch: %s\n' "$ECOMAE_BRANCH"
printf 'Repo:   %s\n' "$REPO"
printf 'Defaults to main (PR #603 ensure→issue merged).\n'

mkdir -p "$(dirname "$REPO")"
if [[ ! -d "$REPO/.git" ]]; then
  git clone "$ECOMAE_GIT_URL" "$REPO"
fi

cd "$REPO"
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
git clean -fd

printf 'HEAD: %s\n' "$(git rev-parse --short HEAD)"
test -f scripts/cloudpanel_validate_final_gate_env.sh
test -f scripts/wait_for_aspnet_health.sh
test -f scripts/cloudpanel_capture_final_gate_artifacts.sh
printf 'Final-gate scripts present.\n'

ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_find_and_redeploy.sh

printf '\n-- Post-deploy health wait --\n'
bash scripts/wait_for_aspnet_health.sh

printf '\n-- Env preflight (values redacted) --\n'
bash scripts/cloudpanel_validate_final_gate_env.sh || true

if [[ ! -f "$ENV_FILE" ]]; then
  printf 'ERROR: %s missing after deploy.\n' "$ENV_FILE" >&2
  exit 1
fi

# shellcheck disable=SC1090
set -a
source "$ENV_FILE"
set +a

missing=0
ECOMAE_PRICE_LOOKUP_API_KEY="${ECOMAE_PRICE_LOOKUP_API_KEY:-${PRICE_LOOKUP_API_KEY:-}}"
ECOMAE_CATALOG_API_KEY="${ECOMAE_CATALOG_API_KEY:-${CATALOG_API_KEY:-}}"
if [[ -z "$ECOMAE_PRICE_LOOKUP_API_KEY" || "$ECOMAE_PRICE_LOOKUP_API_KEY" != epc_pricepro_* ]]; then
  printf 'BLOCKED: set ECOMAE_PRICE_LOOKUP_API_KEY=epc_pricepro_... in %s\n' "$ENV_FILE"
  missing=1
fi
if [[ -z "$ECOMAE_CATALOG_API_KEY" || "$ECOMAE_CATALOG_API_KEY" != epc_catalog_* ]]; then
  printf 'BLOCKED: set ECOMAE_CATALOG_API_KEY=epc_catalog_... in %s\n' "$ENV_FILE"
  missing=1
fi
if [[ -z "${ECOMAE_ADMIN_COOKIE_HEADER:-}" && -z "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
  printf 'BLOCKED: set ECOMAE_ADMIN_COOKIE_HEADER='\''admin_session=...; admin_u_id=123'\'' in %s\n' "$ENV_FILE"
  missing=1
fi

if [[ "$missing" -ne 0 ]]; then
  printf '\nBLOCKED: smoke secrets incomplete. Preferred path:\n'
  printf '  bash scripts/cloudpanel_diagnose_smoke_db.sh\n'
  printf '  ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES bash scripts/cloudpanel_apply_epc_api_clients_ddl.sh\n'
  printf '  # or: ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES bash scripts/cloudpanel_align_tenant_registry_to_php_db.sh\n'
  printf '  ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n'
  printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
  printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
  printf '  (optional manual guidance: bash scripts/cloudpanel_prepare_smoke_secrets.sh)\n'
  printf '  source %s\n' "$ENV_FILE"
  printf '  bash scripts/cloudpanel_validate_final_gate_env.sh\n'
  printf '  bash scripts/cloudpanel_capture_final_gate_artifacts.sh\n'
  printf '  bash scripts/cloudpanel_commit_final_gate_smoke.sh\n'
  printf 'Do NOT remove PHP. Stopping before capture.\n'
  exit 2
fi

printf '\n-- Admin session probe --\n'
probe_tmp="$(mktemp)"
if [[ -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
  probe_code="$(curl -sS -m 20 -o "$probe_tmp" -w '%{http_code}' -b "$ECOMAE_ADMIN_COOKIE_JAR" http://127.0.0.1:5100/auth/session/probe || true)"
else
  probe_code="$(curl -sS -m 20 -o "$probe_tmp" -w '%{http_code}' -H "Cookie: ${ECOMAE_ADMIN_COOKIE_HEADER}" http://127.0.0.1:5100/auth/session/probe || true)"
fi
printf 'HTTP %s\n' "$probe_code"
if ! python3 - "$probe_tmp" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
print({k: doc.get(k) for k in ("Kind", "kind", "IsAuthenticated", "isAuthenticated", "has_backend_access", "UserId", "userId")})
kind = doc.get("Kind") if doc.get("Kind") is not None else doc.get("kind")
auth = doc.get("IsAuthenticated")
if auth is None:
    auth = doc.get("isAuthenticated")
is_admin = kind in ("Admin", 2) or str(kind) == "2"
if not is_admin or auth is False:
    raise SystemExit(1)
print("Admin session OK")
PY
then
  rm -f "$probe_tmp"
  printf 'BLOCKED: Admin session probe failed — login https://www.ecomae.com/CP/ then re-issue cookie:\n'
  printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
  printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
  printf '  (optional: bash scripts/cloudpanel_prepare_smoke_secrets.sh)\n'
  exit 2
fi
rm -f "$probe_tmp"

printf '\n-- Capture final-gate artifacts --\n'
bash scripts/cloudpanel_capture_final_gate_artifacts.sh

printf '\n-- Commit smoke if complete --\n'
bash scripts/cloudpanel_commit_final_gate_smoke.sh || true
printf 'Done. PHP remains authoritative until ReadyToRemovePhp + release-owner approval.\n'
