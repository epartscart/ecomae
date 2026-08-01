#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FORMAT="${1:-markdown}"

if [[ "$FORMAT" != "markdown" && "$FORMAT" != "json" ]]; then
  echo "Usage: $0 [markdown|json]" >&2
  exit 2
fi

mapfile -t php_files < <(cd "$ROOT" && find . -type f -name '*.php' \
  -not -path './vendor/*' \
  -not -path './node_modules/*' \
  -not -path './aspnet/*' \
  -print | sed 's#^./##' | sort)

classify_surface() {
  local path="$1"
  case "$path" in
    cp/*) printf 'cp' ;;
    bos/*|*epc_boc_*) printf 'bos' ;;
    api/*|api.php|epc-api*.php|*api*.php) printf 'api' ;;
    content/shop/finance/*|*/erp/*|*erp*) printf 'erp' ;;
    content/*|templates/*|index.php) printf 'storefront' ;;
    *) printf 'platform' ;;
  esac
}

is_job_like() {
  local path="$1"
  case "$path" in
    *cron*|*import*|*sitemap*|*backup*|*cleanup*|*queue*|*worker*|*sync*) return 0 ;;
    *) return 1 ;;
  esac
}

json_escape() {
  python3 -c 'import json,sys; print(json.dumps(sys.stdin.read().rstrip("\n")))'
}

if [[ "$FORMAT" == "json" ]]; then
  python3 - "$ROOT" <<'PYJSON'
import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1])

def classify_surface(path: str) -> str:
    if path.startswith("cp/"):
        return "cp"
    if path.startswith("bos/") or "epc_boc_" in path:
        return "bos"
    if path.startswith("api/") or path == "api.php" or path.startswith("epc-api") or "api" in path:
        return "api"
    if path.startswith("content/shop/finance/") or "/erp/" in path or "erp" in path:
        return "erp"
    if path.startswith("content/") or path.startswith("templates/") or path == "index.php":
        return "storefront"
    return "platform"

def is_job_like(path: str) -> bool:
    return any(marker in path for marker in ("cron", "import", "sitemap", "backup", "cleanup", "queue", "worker", "sync"))

items = []
for file_path in sorted(root.rglob("*.php")):
    relative = file_path.relative_to(root).as_posix()
    if relative.startswith(("vendor/", "node_modules/", "aspnet/")):
        continue
    items.append({
        "path": relative,
        "surface": classify_surface(relative),
        "jobLike": is_job_like(relative),
        "migrationStatus": "php-only",
    })

print(json.dumps({
    "status": "inventory-required-for-zero-php",
    "totalPhpFiles": len(items),
    "items": items,
}, indent=2))
PYJSON
  exit 0
fi

printf '# PHP Route and Job Inventory\n\n'
printf 'Status: inventory-required-for-zero-php\n\n'
printf 'Total PHP files: %s\n\n' "${#php_files[@]}"
printf '| Path | Surface | Job-like | Migration status |\n'
printf '| --- | --- | --- | --- |\n'
for path in "${php_files[@]}"; do
  surface="$(classify_surface "$path")"
  job="no"
  if is_job_like "$path"; then job="yes"; fi
  printf '| `%s` | %s | %s | php-only |\n' "$path" "$surface" "$job"
done
