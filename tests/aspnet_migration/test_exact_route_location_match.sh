#!/usr/bin/env bash
# Regression: exact-route presence must not treat /article as article-links/brands.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

conf="$tmp/www.ecomae.com.conf"
cat >"$conf" <<'EOF'
server {
  listen 443 ssl;
  location = /health {
    proxy_pass http://127.0.0.1:5100;
  }
  location = /api/v1/catalog/article-brands {
    proxy_pass http://127.0.0.1:5100;
  }
  location = /api/v1/catalog/article-links {
    proxy_pass http://127.0.0.1:5100;
  }
  location / {
    try_files $uri /index.php;
  }
}
EOF

python3 - "$conf" /api/v1/catalog/article <<'PY'
from pathlib import Path
import re, sys
path = Path(sys.argv[1])
route = sys.argv[2]
text = path.read_text(encoding="utf-8")
loc_re = re.compile(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{")
if loc_re.search(text):
    raise SystemExit("FAIL: /api/v1/catalog/article falsely matched article-brands/links")
print("PASS: article not present among article-brands/links")
PY

# Simulate insert of exact article location, then confirm ALREADY PRESENT.
block=$'  location = /api/v1/catalog/article {\n    proxy_pass http://127.0.0.1:5100;\n  }\n'
python3 - "$conf" /api/v1/catalog/article "$block" <<'PY'
from pathlib import Path
import re, sys
path = Path(sys.argv[1])
route = sys.argv[2]
block = sys.argv[3].rstrip() + "\n"
text = path.read_text(encoding="utf-8")
loc_re = re.compile(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{")
if loc_re.search(text):
    raise SystemExit("unexpected: article already present before insert")
marker = text.find("\n  location / {")
if marker < 0:
    raise SystemExit("missing location /")
marker += 1
path.write_text(text[:marker] + block + "\n" + text[marker:], encoding="utf-8")
text2 = path.read_text(encoding="utf-8")
if not loc_re.search(text2):
    raise SystemExit("FAIL: article location not found after insert")
print("PASS: article inserted and exact-matched")
PY

# Guard the installer source still uses the exact regex (not bare substring).
if ! grep -Fq 're.escape(route)' "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh"; then
  printf 'FAIL: installer missing re.escape(route) exact location match\n' >&2
  exit 1
fi
if grep -Fq 'if f"location = {route}" in text:' "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh"; then
  printf 'FAIL: installer still uses substring ALREADY PRESENT check\n' >&2
  exit 1
fi
printf 'PASS: installer source uses exact location match\n'
printf 'All exact-route location match checks passed.\n'
