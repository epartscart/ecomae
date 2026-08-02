#!/usr/bin/env bash
# Paste-safe bootstrap when scripts/cloudpanel_find_and_redeploy.sh is missing.
# Typical cause: /opt/ecomae-aspnet-source exists but was never updated to latest main.
#
# Usage (as root on CloudPanel SSH) — copy this ENTIRE block:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_bootstrap_from_github.sh)"
# Or clone/pull first, then:
#   bash scripts/cloudpanel_bootstrap_from_github.sh
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
REPO="${ECOMAE_REPO:-/opt/ecomae-aspnet-source}"

printf '== CloudPanel bootstrap from GitHub (%s) ==\n' "$ECOMAE_BRANCH"
printf 'Target repo: %s\n' "$REPO"
printf 'Do NOT use /var/www/ecomae — that path is usually missing.\n'

mkdir -p "$(dirname "$REPO")"
if [[ ! -d "$REPO/.git" ]]; then
  printf 'Cloning fresh checkout...\n'
  rm -rf "$REPO"
  git clone "$ECOMAE_GIT_URL" "$REPO"
fi

cd "$REPO"
pwd
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
git clean -fd

printf 'HEAD: %s\n' "$(git rev-parse --short HEAD)"
test -f scripts/cloudpanel_find_and_redeploy.sh
test -f scripts/cloudpanel_production_deploy_foundation.sh
printf 'Deploy scripts present. Continuing...\n'

bash scripts/cloudpanel_find_and_redeploy.sh
