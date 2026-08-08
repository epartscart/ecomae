#!/usr/bin/env bash
# One-shot: bind www.epartscart.com → shared shop DB `docpart` (PHP portal parity),
# status=live, restore php-reference, prove bunches + CP login path.
# No secret required — docpart is the ePartsCart Model C shop schema.
#
# Paste as root:
#   curl -fsSL 'https://raw.githubusercontent.com/epartscart/ecomae/cursor/epartscart-portal-dump-bind-7b3b/scripts/cloudpanel_EPARTSCART_BIND_DOCPART_NOW.sh' | bash
#   # or after merge: .../main/scripts/cloudpanel_EPARTSCART_BIND_DOCPART_NOW.sh
set -euo pipefail
printf '======== EPARTSCART BIND_DOCPART_NOW ========\n'
export ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/epartscart-portal-dump-bind-7b3b}"
export ECOMAE_EPARTSCART_SHOP_DB=docpart
export ECOMAE_DIAG_EMAIL="${ECOMAE_DIAG_EMAIL:-taxofin2025@gmail.com}"
[[ "$(id -u)" -eq 0 ]] || { echo RESULT=FAIL must_run_as_root; exit 1; }

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  [[ -n "$d" && -d "$d/.git" ]] && REPO="$d" && break
done
[[ -n "$REPO" ]] || { echo RESULT=FAIL repo_not_found; exit 1; }
cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
chmod +x scripts/cloudpanel_EPARTSCART_BIND_SHOP_DB_NOW.sh \
  scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh 2>/dev/null || true
grep -q 'php_parity_docpart\|ECOMAE_EPARTSCART_SHOP_DB:-docpart' scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh \
  scripts/cloudpanel_EPARTSCART_BIND_SHOP_DB_NOW.sh \
  || { echo RESULT=FAIL wrong_tree_missing_docpart_default; exit 1; }

bash scripts/cloudpanel_EPARTSCART_BIND_SHOP_DB_NOW.sh 2>&1 | tee /root/epartscart-bind-docpart-now.log
RC=${PIPESTATUS[0]}
grep -E 'RESULT=|BOUND_|CP_LOGIN_DIAG|GATE_|RESOLVER_|POST_LOGIN|resolved_shop|docpart|ERROR' /root/epartscart-bind-docpart-now.log | tail -80 || true
exit "$RC"
