#!/usr/bin/env bash
# Isolation must not publish from this feature branch with the health gate off.
# Devin: merge PR #2156 to main, then deploy main through the gated path.
#
# After merge, CloudPanel root:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_deploy_main_gated.sh)"
set -euo pipefail

printf 'REFUSED: do not publish cursor/cp-erp-chrome-isolation-5dbd with the health gate disabled.\n' >&2
printf 'Devin path:\n' >&2
printf '  1. Merge https://github.com/epartscart/ecomae/pull/2156 into main\n' >&2
printf '  2. On CloudPanel, deploy main with real /health + /ready (no ProbeBypass, no emergency skip):\n' >&2
printf '     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_deploy_main_gated.sh)"\n' >&2
printf 'If shops are 500 on the isolation binary, restore the last main release until that merge+gated deploy:\n' >&2
printf '  ln -sfn /var/www/ecomae-aspnet/releases/20261011180909 /var/www/ecomae-aspnet/current\n' >&2
printf '  systemctl restart ecomae-platform.service\n' >&2
printf '  bash /opt/ecomae-aspnet-source/scripts/wait_for_aspnet_health.sh\n' >&2
exit 2
