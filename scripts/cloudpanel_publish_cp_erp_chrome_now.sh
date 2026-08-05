#!/usr/bin/env bash
# Publish main so PR #869 CP/ERP chrome (topnav click, px fonts, commerce KPIs,
# NetSuite ERP dash) is live. Nginx classic-entry alone does NOT update the binary.
#
# Usage (CloudPanel root):
#   bash scripts/cloudpanel_publish_cp_erp_chrome_now.sh
# Or one-shot from GitHub after this lands on main:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_publish_cp_erp_chrome_now.sh)"
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
ROOT=""
for d in /opt/ecomae-aspnet-source /root/ecomae; do
  if [[ -d "$d/.git" ]]; then
    ROOT="$d"
    break
  fi
done
if [[ -z "$ROOT" ]]; then
  printf 'ERROR: no ecomae checkout at /opt/ecomae-aspnet-source or /root/ecomae\n' >&2
  exit 1
fi

printf '== Publish CP/ERP chrome (#869) from %s @ %s ==\n' "$ROOT" "$ECOMAE_BRANCH"

for d in /opt/ecomae-aspnet-source /root/ecomae; do
  if [[ -d "$d/.git" ]]; then
    git -C "$d" fetch origin "$ECOMAE_BRANCH"
    git -C "$d" checkout -f "$ECOMAE_BRANCH"
    git -C "$d" reset --hard "origin/$ECOMAE_BRANCH"
    printf '  %s → %s\n' "$d" "$(git -C "$d" rev-parse --short HEAD)"
  fi
done

cd "$ROOT"

# Source markers must be present before publish.
grep -q 'bindCpTopNav' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor
grep -q '<span>Control</span>' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor
grep -q 'Orders today' aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor
grep -q 'ns-dash' aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor
grep -q 'nsChartAr' aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor
grep -q 'bindErpTopNav' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor
printf 'OK source markers (#869) present\n'

# Prefer emergency publish so foundation gate noise cannot leave :5100 on an old binary.
export ECOMAE_EMERGENCY_PUBLISH="${ECOMAE_EMERGENCY_PUBLISH:-1}"
export ECOMAE_BRANCH
bash scripts/cloudpanel_find_and_redeploy.sh

systemctl restart ecomae-platform.service || true
bash scripts/wait_for_aspnet_health.sh || true

printf '\n== Prove published binary on :5100 ==\n'
CP_BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 30 http://127.0.0.1:5100/cp || true)"
ERP_BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 30 http://127.0.0.1:5100/erp || true)"

fail=0
for needle in 'bindCpTopNav' '<span>Control</span>' 'Orders today'; do
  if grep -Fq "$needle" <<<"$CP_BODY"; then
    printf 'PASS  /cp has %s\n' "$needle"
  else
    printf 'FAIL  /cp missing %s\n' "$needle"
    fail=1
  fi
done
if grep -Fq '>CONTROL<' <<<"$CP_BODY" || grep -Fq 'Admin users' <<<"$CP_BODY"; then
  printf 'FAIL  /cp still serving OLD chrome (CONTROL / Admin users)\n'
  fail=1
fi

for needle in 'bindErpTopNav' 'ns-dash' 'nsChartAr' 'chart.js@4.4.1'; do
  if grep -Fq "$needle" <<<"$ERP_BODY"; then
    printf 'PASS  /erp has %s\n' "$needle"
  else
    printf 'FAIL  /erp missing %s\n' "$needle"
    fail=1
  fi
done
if grep -Fq 'epc-erp-banner' <<<"$ERP_BODY"; then
  printf 'FAIL  /erp still serving OLD banner digest\n'
  fail=1
fi

printf '\nPublic hard-refresh:\n'
printf '  https://www.ecomae.com/cp\n'
printf '  https://www.ecomae.com/erp\n'
printf 'Tenant CP (no BOS):\n'
printf '  https://www.epartscart.com/cp\n'

if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL — binary still stale or publish missed :5100\n' >&2
  printf 'Check: systemctl status ecomae-platform.service; journalctl -u ecomae-platform.service -n 80 --no-pager\n' >&2
  exit 1
fi

printf '\nRESULT=PASS — #869 CP/ERP chrome is live on :5100\n'
exit 0
