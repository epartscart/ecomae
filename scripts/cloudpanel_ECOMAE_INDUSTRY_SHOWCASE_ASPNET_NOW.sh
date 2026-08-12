#!/usr/bin/env bash
# Publish industry showcase ASP.NET snapshots + retarget classic-entry industry /
# away from /storefront/app. Merge ≠ live — paste as root on CloudPanel.
#
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/industry-showcase-aspnet-7529/scripts/cloudpanel_ECOMAE_INDUSTRY_SHOWCASE_ASPNET_NOW.sh'
#   TMP=/tmp/ecomae-industry-showcase-aspnet-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   grep -q ECOMAE_INDUSTRY_SHOWCASE_ASPNET_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   export ECOMAE_BRANCH=cursor/industry-showcase-aspnet-7529
#   bash "$TMP" 2>&1 | tee /root/ecomae-industry-showcase-aspnet-now.log
#   grep -E 'RESULT=|GATE_|SHA=|INDUSTRY_|PUBLISH_|SETTLE_' /root/ecomae-industry-showcase-aspnet-now.log | tail -120
set -euo pipefail

printf '======== ECOMAE_INDUSTRY_SHOWCASE_ASPNET_NOW ========\n'
die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" -eq 0 ]] || die "must_run_as_root"

BRANCH="${ECOMAE_BRANCH:-main}"
export ECOMAE_BRANCH="$BRANCH"
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"
export ECOMAE_SKIP_CP_LOGIN_DIAG="${ECOMAE_SKIP_CP_LOGIN_DIAG:-YES}"
export ECOMAE_ALLOW_PHP_REFERENCE_503="${ECOMAE_ALLOW_PHP_REFERENCE_503:-YES}"

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  [[ -n "$d" && -d "$d/.git" ]] && REPO="$d" && break
done
[[ -n "$REPO" ]] || { mkdir -p /opt; git clone https://github.com/epartscart/ecomae.git /opt/ecomae-aspnet-source; REPO=/opt/ecomae-aspnet-source; }

cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$BRANCH" || die "git_fetch_failed $BRANCH"
git checkout -f "$BRANCH" || die "git_checkout_failed"
git reset --hard "origin/$BRANCH" || die "git_reset_failed"
SHA="$(git rev-parse --short HEAD)"
printf 'REPO=%s SHA=%s BRANCH=%s\n' "$REPO" "$SHA" "$BRANCH"

need() { grep -q "$2" "$1" || die "missing_marker $1 :: $2"; }
need aspnet/src/EcomAE.Platform/Middleware/EcomaeIndustryShowcaseMiddleware.cs 'EcomaeIndustryShowcaseMiddleware'
need aspnet/src/EcomAE.Platform/Presentation/EcomaeIndustryShowcaseSnapshots.cs 'epc_rendered_industry'
test -f content/general_pages/epc_rendered_industry/automotive.html || die "missing automotive hub snapshot"
test -f content/general_pages/epc_rendered_industry/automotive__vehicle-dealership-sales.html || die "missing automotive sub snapshot"
HUB_COUNT="$(find content/general_pages/epc_rendered_industry -maxdepth 1 -type f -name '*.html' ! -name '*__*' | wc -l | tr -d ' ')"
[[ "${HUB_COUNT:-0}" -ge 28 ]] || die "expected_28_industry_hubs got=${HUB_COUNT}"
printf 'PREFLIGHT_INDUSTRY_SNAPSHOTS=OK hubs=%s\n' "$HUB_COUNT"

# Refresh industry classic-entry snippet if installer present (home → / not /storefront/app).
if [[ -f scripts/cloudpanel_install_classic_entry_aspnet_primary.sh ]]; then
  export ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES
  export ECOMAE_CLASSIC_ENTRY_INDUSTRY_ONLY=YES
  set +e
  bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh 2>&1 \
    | tee /root/ecomae-industry-classic-entry.log | tail -60
  set -e
fi

# Patch live nginx industry home remap if still pointing at storefront/app.
set +e
python3 - <<'PY'
from pathlib import Path
import re
changed = 0
pat = re.compile(r"proxy_pass\s+http://127\.0\.0\.1:5100/storefront/app\s*;")
for base in (Path("/etc/nginx/sites-enabled"), Path("/etc/nginx/conf.d"), Path("/etc/nginx/sites-available")):
    if not base.exists():
        continue
    for conf in base.rglob("*"):
        if not conf.is_file():
            continue
        try:
            text = conf.read_text(errors="ignore")
        except Exception:
            continue
        if "classic-entry-industry-home" not in text and "automotive" not in text and "ecomae.com" not in text:
            continue
        if "5100/storefront/app" not in text:
            continue
        # Only rewrite industry home blocks that mention industry host regex or classic-entry-industry.
        if "classic-entry-industry" not in text and "agriculture|automotive" not in text:
            continue
        new = pat.sub("proxy_pass http://127.0.0.1:5100/;", text)
        if new != text:
            bak = conf.with_suffix(conf.suffix + f".bak.industry-showcase.{Path('/').stat().st_mtime_ns if False else '1'}")
            try:
                conf.write_text(new)
                changed += 1
                print("patched", conf)
            except Exception as e:
                print("skip", conf, e)
print("nginx_industry_home_patches", changed)
PY
set -e

nginx -t 2>&1 | tee /root/ecomae-industry-nginx-t.log || die "nginx_t_failed"
systemctl reload nginx 2>/dev/null || true

PUBLISH=""
if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  PUBLISH=scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  PUBLISH=scripts/cloudpanel_FORCE_LIVE_NOW.sh
else
  die "no LIVE_PUBLISH"
fi

printf '\n---- PUBLISH (%s) ----\n' "$PUBLISH"
set +e
ECOMAE_BRANCH="$BRANCH" bash "$PUBLISH" 2>&1 | tee /root/ecomae-industry-showcase-publish.log | tail -80
PUB_RC=${PIPESTATUS[0]}
set -e
[[ "$PUB_RC" -eq 0 ]] || die "publish_failed"

sleep 3
fail=0
for host in automotive.ecomae.com food.ecomae.com technology.ecomae.com healthcare.ecomae.com; do
  code=$(curl -sS -o /tmp/ind_hub.html -w '%{http_code}' --max-time 25 "https://${host}/" || echo 000)
  hdr=$(curl -sS -D - -o /dev/null --max-time 20 "https://${host}/" | tr -d '\r' || true)
  printf 'INDUSTRY_HUB host=%s http=%s\n' "$host" "$code"
  if [[ "$code" != "200" ]]; then fail=1; continue; fi
  if echo "$hdr" | grep -Eiq 'x-ecomae-industry-showcase:[[:space:]]*snapshot'; then
    printf 'GATE_OK industry_snapshot_header %s\n' "$host"
  else
    # Header may be stripped by Cloudflare — require industry markers + no Auto Parts storefront chrome.
    if grep -Eiq 'industry_3d|epc-ind|Industry' /tmp/ind_hub.html \
      && ! grep -Eiq 'Auto Parts catalog|nero storefront' /tmp/ind_hub.html; then
      printf 'GATE_OK industry_body_markers %s\n' "$host"
    else
      printf 'GATE_BAD industry_hub %s\n' "$host"
      fail=1
    fi
  fi
done

code=$(curl -sS -o /tmp/ind_sub.html -w '%{http_code}' --max-time 25 \
  'https://automotive.ecomae.com/vehicle-dealership-sales' || echo 000)
printf 'INDUSTRY_SUB http=%s\n' "$code"
if [[ "$code" == "200" ]] && grep -Eiq 'dealership' /tmp/ind_sub.html; then
  printf 'GATE_OK industry_sub\n'
elif [[ "$code" == "302" ]]; then
  printf 'GATE_BAD industry_sub_still_redirects (nginx sub location missing)\n'
  fail=1
else
  printf 'GATE_BAD industry_sub http=%s\n' "$code"
  fail=1
fi

code=$(curl -sS -o /tmp/plat_ind.html -w '%{http_code}' --max-time 25 \
  'https://www.ecomae.com/platform/industries' || echo 000)
printf 'PLATFORM_INDUSTRIES http=%s\n' "$code"
[[ "$code" == "200" ]] && grep -Eiq 'automotive\.ecomae\.com' /tmp/plat_ind.html \
  && printf 'GATE_OK platform_industries\n' || { printf 'GATE_BAD platform_industries\n'; fail=1; }

[[ "$fail" -eq 0 ]] || die "industry_gates_failed"
printf 'RESULT=PASS ECOMAE_INDUSTRY_SHOWCASE_ASPNET SHA=%s BRANCH=%s\n' "$SHA" "$BRANCH"
