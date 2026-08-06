#!/usr/bin/env bash
# Restore PHP reference / interim /en/ HTTP serving after a temporary ASP.NET deep test.
# Does not flip cutover. Keeps ASP.NET product-primary.
#
#   ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES \
#     bash scripts/cloudpanel_restore_php_reference_serving.sh
set -euo pipefail

if [[ "${ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING:-}" != "YES" ]]; then
  printf 'REFUSE: set ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES\n' >&2
  exit 2
fi

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on CloudPanel\n' >&2
  exit 1
fi

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
FLAG_ETC="/etc/ecomae-aspnet/php_serving_deactivated"
NGINX_SNIPPET=/etc/nginx/snippets/ecomae-php-serving-temporarily-deactivated.conf

printf '======== RESTORE PHP REFERENCE SERVING ========\n'

rm -f "$FLAG_ETC"
mapfile -t DOCROOTS < <(find /home /var/www -maxdepth 4 -name '.epc_php_serving_deactivated' 2>/dev/null | sed 's|/.epc_php_serving_deactivated$||' || true)
for dir in "${DOCROOTS[@]}"; do
  rm -f "$dir/.epc_php_serving_deactivated"
  printf 'removed flag %s\n' "$dir"
done

if [[ -f "$ENV_FILE" ]]; then
  cp -a "$ENV_FILE" "${ENV_FILE}.bak.temp-php-on.$(date +%Y%m%d%H%M%S)"
  python3 - <<PY
from pathlib import Path
p = Path("$ENV_FILE")
lines = p.read_text().splitlines()
keys = {
  "EcomAE__PhpReference__TemporarilyDeactivatePhpServing": "false",
  "EcomAE__PhpReference__KeepPhpProjectAvailable": "true",
  "EcomAE__PhpReference__Mode": "aspnet-primary-php-reference",
  "MigrationRouteCutover__RequirePhpFallback": "true",
}
out = []
seen = set()
for line in lines:
    if not line.strip() or line.lstrip().startswith("#") or "=" not in line:
        out.append(line)
        continue
    k = line.split("=", 1)[0].strip()
    if k in keys:
        out.append(f"{k}={keys[k]}")
        seen.add(k)
    else:
        out.append(line)
for k, v in keys.items():
    if k not in seen:
        out.append(f"{k}={v}")
p.write_text("\n".join(out) + "\n")
print("updated", p)
PY
fi

# Remove nginx include lines
python3 - <<'PY'
from pathlib import Path
marker = "# ecomae-temp-php-serving-off"
snippet = "include /etc/nginx/snippets/ecomae-php-serving-temporarily-deactivated.conf;"
for base in (Path("/etc/nginx/sites-enabled"), Path("/etc/nginx/conf.d")):
    if not base.exists():
        continue
    for conf in base.iterdir():
        if not conf.is_file():
            continue
        try:
            text = conf.read_text(errors="ignore")
        except Exception:
            continue
        if marker not in text and snippet not in text:
            continue
        lines = [ln for ln in text.splitlines() if marker not in ln and snippet not in ln]
        conf.write_text("\n".join(lines) + "\n")
        print("unpatched:", conf)
PY
rm -f "$NGINX_SNIPPET"
nginx -t && systemctl reload nginx
systemctl restart ecomae-platform.service || true

Q="epc_php_on=$(date +%s)"
code_ref="$(curl -sS -o /dev/null -w '%{http_code}' -A 'Mozilla/5.0' --max-time 20 \
  "https://www.epartscart.com/php-reference/storefront?${Q}" || true)"
printf 'php-reference/storefront http=%s (expect not 503)\n' "$code_ref"

cat <<EOF

#####################################################################
#  RESULT=RESTORED — PHP reference serving re-enabled
#  Mode back to aspnet-primary-php-reference
#  cutoverAllowed=false · readyForPhpRemoval=false · KeepPhpProjectAvailable=true
#####################################################################
EOF
exit 0
