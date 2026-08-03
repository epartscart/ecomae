# shellcheck shell=bash
# Source from cloudpanel_*_shadow installers.
# Keeps live tenant / industry presentation on PHP unless explicitly overridden.
#
# Usage:
#   # shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
#   source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"
#   ecomae_assert_nginx_shadow_target_allowed "$CONF" exact-route
#   ecomae_assert_nginx_shadow_target_allowed "$CONF" presentation

ecomae_assert_nginx_shadow_target_allowed() {
  local conf="${1:?nginx site conf path required}"
  local purpose="${2:-exact-route}"
  local root safety_py

  root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
  safety_py="$root/scripts/ecomae_nginx_site_safety.py"
  if [[ ! -f "$safety_py" ]]; then
    printf 'ERROR: missing %s\n' "$safety_py" >&2
    return 1
  fi
  python3 "$safety_py" "$conf" --purpose "$purpose"
}
