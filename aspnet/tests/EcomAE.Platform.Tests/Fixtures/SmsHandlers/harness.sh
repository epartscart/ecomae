#!/usr/bin/env bash
# Regenerates goldens.json from the real PHP handlers (php-cli with the curl extension, display_errors off like production).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../../../.." && pwd)"
out="$here/goldens.json"
names=$(php -r 'foreach (json_decode(file_get_contents($argv[1]), true) as $c) echo $c["name"], "\n";' "$here/cases.json")
{
  echo "{"
  first=1
  for name in $names; do
    [ $first -eq 1 ] || echo ","
    first=0
    printf '"%s": ' "$name"
    php -d display_errors=0 -d error_reporting=E_ALL "$here/harness.php" "$root" "$here/cases.json" "$name"
  done
  echo
  echo "}"
} > "$out"
php -r 'json_decode(file_get_contents($argv[1]), true, 512, JSON_THROW_ON_ERROR);' "$out"
echo "wrote $out"
