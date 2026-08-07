#!/usr/bin/env bash
# Probe live www.ecomae.com marketing presentation — must remain PHP animated hero.
# Does NOT cut over. Fails if Blazor markers appear on live marketing home/pages.
set -euo pipefail

BASE="${ECOMAE_MARKETING_BASE:-https://www.ecomae.com}"
UA="${ECOMAE_PROBE_UA:-Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36}"
TMPDIR_PROBE="$(mktemp -d)"
trap 'rm -rf "$TMPDIR_PROBE"' EXIT

say() { printf '%s\n' "$*"; }

fetch_to() {
  local url="$1" out="$2"
  curl -fsSL -A "$UA" -L --max-time 45 "$url" -o "$out" 2>/dev/null || : >"$out"
}

http_code() {
  local url="$1"
  curl -sS -o /dev/null -w '%{http_code}' -A "$UA" -L --max-time 30 "$url" 2>/dev/null || echo "000"
}

pass=0
fail=0

check_file_contains() {
  local label="$1" file="$2" needle="$3"
  if grep -Fq -- "$needle" "$file"; then
    say "PASS  $label contains $needle"
    pass=$((pass + 1))
  else
    say "FAIL  $label missing $needle"
    fail=$((fail + 1))
  fi
}

check_file_absent() {
  local label="$1" file="$2" needle="$3"
  if grep -Fiq -- "$needle" "$file"; then
    say "FAIL  $label must NOT contain $needle"
    fail=$((fail + 1))
  else
    say "PASS  $label absent $needle"
    pass=$((pass + 1))
  fi
}

say "=== ecomae.com marketing PHP chrome probe ==="
say "BASE=$BASE"
say "Live marketing must stay PHP animated epm-hub. /marketing/app is scaffold-only."

HOME_FILE="$TMPDIR_PROBE/home.html"
fetch_to "$BASE/" "$HOME_FILE"
check_file_contains "home" "$HOME_FILE" "epm-hub"
check_file_contains "home" "$HOME_FILE" "epm-hub__orbit-spin"
check_file_contains "home" "$HOME_FILE" "epm-hub__matrix"
check_file_contains "home" "$HOME_FILE" "epm-hub-section"
check_file_contains "home" "$HOME_FILE" "ECOMAE-MARKETING-HOME"
check_file_absent "home" "$HOME_FILE" "ecomae-chrome-surface"
check_file_absent "home" "$HOME_FILE" "blazor.web.js"

# Keyframes may be inline (pre-extract) or via shared CSS endpoint (post-deploy).
if grep -Fq -- "epmHubOrbitSpin" "$HOME_FILE"; then
  say "PASS  home contains epmHubOrbitSpin keyframes"
  pass=$((pass + 1))
else
  CSS_FILE="$TMPDIR_PROBE/marketing.css"
  # Prefer ASP.NET /platform-assets bridge (survives PHP pause); fall back to PHP helper.
  fetch_to "$BASE/platform-assets/epc_ecomae_platform_marketing.css" "$CSS_FILE" || true
  if [[ ! -s "$CSS_FILE" ]]; then
    fetch_to "$BASE/content/general_pages/epc_ecomae_platform_marketing_css.php" "$CSS_FILE"
  fi
  if grep -Fq -- "epmHubOrbitSpin" "$CSS_FILE"; then
    say "PASS  marketing-css contains epmHubOrbitSpin"
    pass=$((pass + 1))
  else
    say "FAIL  home/CSS missing epmHubOrbitSpin keyframes"
    fail=$((fail + 1))
  fi
fi

# Standard marketing chrome pages (epm-topbar layout).
PAGES=(
  "/platform"
  "/platform/demo"
  "/platform/capabilities"
  "/documentation"
  "/compare"
  "/blockchain"
  "/legal"
  "/solutions"
)

for path in "${PAGES[@]}"; do
  code="$(http_code "$BASE$path")"
  page_file="$TMPDIR_PROBE/page.html"
  fetch_to "$BASE$path" "$page_file"
  if [[ "$code" == "200" && -s "$page_file" ]]; then
    say "PASS  $path HTTP 200"
    pass=$((pass + 1))
  else
    say "FAIL  $path HTTP $code want 200 with body"
    fail=$((fail + 1))
    continue
  fi
  check_file_contains "$path" "$page_file" "epm-body"
  check_file_contains "$path" "$page_file" "epm-topbar"
  check_file_absent "$path" "$page_file" "ecomae-chrome-surface"
  check_file_absent "$path" "$page_file" "blazor.web.js"
done

# Brochure is a print-oriented PHP page (no epm-topbar) — still must stay PHP.
BROCHURE_FILE="$TMPDIR_PROBE/brochure.html"
code="$(http_code "$BASE/brochure")"
fetch_to "$BASE/brochure" "$BROCHURE_FILE"
if [[ "$code" == "200" && -s "$BROCHURE_FILE" ]]; then
  say "PASS  /brochure HTTP 200"
  pass=$((pass + 1))
else
  say "FAIL  /brochure HTTP $code want 200 with body"
  fail=$((fail + 1))
fi
check_file_contains "/brochure" "$BROCHURE_FILE" "Product brochure"
check_file_absent "/brochure" "$BROCHURE_FILE" "ecomae-chrome-surface"
check_file_absent "/brochure" "$BROCHURE_FILE" "blazor.web.js"

say ""
say "PASS=$pass FAIL=$fail"
if [[ "$fail" -gt 0 ]]; then
  say "RESULT=FAIL — live marketing presentation drifted from PHP"
  exit 1
fi
say "RESULT=PASS — live ecomae.com marketing remains PHP authoritative"
exit 0
