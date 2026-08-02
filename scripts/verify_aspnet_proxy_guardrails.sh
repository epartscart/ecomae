#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONF_DIR="${1:-$ROOT/deploy/aspnet}"
failed=0
passed=0

pass() { printf '  PASS  %s\n' "$1"; passed=$((passed + 1)); }
fail() { printf '  FAIL  %s\n' "$1"; failed=$((failed + 1)); }

printf '== ASP.NET Core proxy guardrail verification ==\n'
printf 'Config directory: %s\n' "$CONF_DIR"

if [[ ! -d "$CONF_DIR" ]]; then
    printf 'Missing config directory: %s\n' "$CONF_DIR" >&2
    exit 1
fi

mapfile -t configs < <(find "$CONF_DIR" -maxdepth 1 -type f \( -name '*.conf' -o -name '*.template.conf' \) | sort)
if [[ "${#configs[@]}" -eq 0 ]]; then
    printf 'No nginx config snippets found in %s\n' "$CONF_DIR" >&2
    exit 1
fi

for file in "${configs[@]}"; do
    rel="${file#$ROOT/}"
    if grep -Eq '^[[:space:]]*location[[:space:]]+(\^~[[:space:]]+)?/(cp|CP|erp|ERP|bos|BOS)([[:space:]/{]|$)' "$file"; then
        fail "$rel contains a broad admin surface location"
    else
        pass "$rel avoids broad CP/ERP/BOS locations"
    fi

    if grep -Eq '^[[:space:]]*location[[:space:]]+(\^~[[:space:]]+)?/api([[:space:]/{]|$)' "$file"; then
        fail "$rel contains a broad API location"
    else
        pass "$rel avoids broad API locations"
    fi

    if grep -Eq '^[[:space:]]*location[[:space:]]+/(|storefront|cart|checkout)([[:space:]/{]|$)' "$file"; then
        fail "$rel contains a broad storefront/catch-all location"
    else
        pass "$rel avoids storefront/catch-all locations"
    fi

done

if grep -R --line-number -E '^[[:space:]]*location[[:space:]]+= /api/v1/catalog/status' "$CONF_DIR" >/dev/null; then
    pass 'exact API shadow route example is present'
else
    fail 'exact API shadow route example is missing'
fi

if grep -R --line-number -E '^[[:space:]]*location[[:space:]]+= /api/v1/price/lookup' "$CONF_DIR" >/dev/null; then
    pass 'exact price lookup shadow route example is present'
else
    fail 'exact price lookup shadow route example is missing'
fi

if grep -R --line-number -E '^[[:space:]]*location[[:space:]]+= /api/v1/catalog/manufacturers' "$CONF_DIR" >/dev/null; then
    pass 'exact catalog manufacturers shadow route example is present'
else
    fail 'exact catalog manufacturers shadow route example is missing'
fi

for route in models modifications brands vin engines analogs article-brands categories products engine-search article-links article articles engine brand-parts; do
    if grep -R --line-number -E "^[[:space:]]*location[[:space:]]+= /api/v1/catalog/${route}" "$CONF_DIR" >/dev/null; then
        pass "exact catalog ${route} shadow route example is present"
    else
        fail "exact catalog ${route} shadow route example is missing"
    fi
done

if grep -R --line-number -E 'allow YOUR_OFFICE_IP' "$CONF_DIR" >/dev/null; then
    pass 'diagnostic migration routes are visibly allowlisted'
else
    fail 'diagnostic migration allowlist marker is missing'
fi

printf '\n----------------------------\n'
printf 'Passed: %d  Failed: %d\n' "$passed" "$failed"

if [[ "$failed" -ne 0 ]]; then
    exit 1
fi
