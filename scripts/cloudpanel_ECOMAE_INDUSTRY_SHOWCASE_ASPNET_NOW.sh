#!/usr/bin/env bash
# Publish industry showcase ASP.NET snapshots + retarget classic-entry industry /
# away from /storefront/app and /marketing/app. Merge ≠ live — paste as root on CloudPanel.
#
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/industry-showcase-live-7529/scripts/cloudpanel_ECOMAE_INDUSTRY_SHOWCASE_ASPNET_NOW.sh'
#   TMP=/tmp/ecomae-industry-showcase-aspnet-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   grep -q ECOMAE_INDUSTRY_SHOWCASE_ASPNET_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   grep -q PACKED_SNAPSHOTS "$TMP" || { echo RESULT=FAIL stale_download; exit 1; }
#   export ECOMAE_BRANCH=cursor/industry-showcase-live-7529
#   bash "$TMP" 2>&1 | tee /root/ecomae-industry-showcase-aspnet-now.log
#   grep -E 'RESULT=|GATE_|SHA=|INDUSTRY_|PUBLISH_|PACKED_' /root/ecomae-industry-showcase-aspnet-now.log | tail -120
set -euo pipefail

printf '======== ECOMAE_INDUSTRY_SHOWCASE_ASPNET_NOW ========\n'
die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" -eq 0 ]] || die "must_run_as_root"

# Default this live-fix branch — CHPU-only publishes omit industry middleware.
BRANCH="${ECOMAE_BRANCH:-cursor/industry-showcase-live-7529}"
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
FULL="$(git rev-parse HEAD)"
printf 'REPO=%s SHA=%s FULL=%s BRANCH=%s\n' "$REPO" "$SHA" "$FULL" "$BRANCH"

need() { grep -q "$2" "$1" || die "missing_marker $1 :: $2"; }
need aspnet/src/EcomAE.Platform/Middleware/EcomaeIndustryShowcaseMiddleware.cs 'EcomaeIndustryShowcaseMiddleware'
need aspnet/src/EcomAE.Platform/Presentation/EcomaeIndustryShowcaseSnapshots.cs 'epc_rendered_industry'
need aspnet/src/EcomAE.Platform/Presentation/PhpHomeWidgetHtml.cs 'epc_rendered_industry'
need aspnet/src/EcomAE.Platform/Program.cs 'EcomaeIndustryShowcaseMiddleware'
need scripts/cloudpanel_FORCE_LIVE_NOW.sh 'PACKED_SNAPSHOTS'
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

# Patch live nginx industry home remap if still pointing at storefront/app or marketing/app.
set +e
python3 - <<'PY'
from pathlib import Path
import re
changed = 0
pats = [
    re.compile(r"proxy_pass\s+http://127\.0\.0\.1:5100/storefront/app\s*;"),
    re.compile(r"proxy_pass\s+http://127\.0\.0\.1:5100/marketing/app\s*;"),
]
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
        if "ecomae.com" not in text and "classic-entry-industry" not in text:
            continue
        if "5100/storefront/app" not in text and "5100/marketing/app" not in text:
            continue
        # Prefer industry host blocks; also allow wildcard *.ecomae.com classic-entry.
        if (
            "classic-entry-industry" not in text
            and "agriculture|automotive" not in text
            and "server_name *.ecomae.com" not in text
            and "server_name *.ecomae.com;" not in text
        ):
            # Still patch location = / blocks that list industry hosts.
            if "automotive.ecomae.com" not in text and "electronics.ecomae.com" not in text:
                continue
        new = text
        for pat in pats:
            new = pat.sub("proxy_pass http://127.0.0.1:5100/;", new)
        if new != text:
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

# Direct FORCE_LIVE — packs epc_rendered_industry into the publish tree.
[[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]] || die "FORCE_LIVE missing"
printf '\n---- FORCE_LIVE_NOW (industry showcase publish) ----\n'
set +e
ECOMAE_BRANCH="$BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 \
  | tee /root/ecomae-industry-showcase-publish.log | tail -100
PUB_RC=${PIPESTATUS[0]}
set -e
[[ "$PUB_RC" -eq 0 ]] || die "publish_failed"

# Confirm packed snapshots + middleware landed in the live release.
RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
CUR="$(readlink -f "$RELEASE_ROOT/current" 2>/dev/null || true)"
[[ -n "$CUR" && -d "$CUR" ]] || die "missing_release_current"
AUTO_SNAP="$CUR/platform/content/general_pages/epc_rendered_industry/automotive.html"
[[ -f "$AUTO_SNAP" ]] || die "publish_missing_packed_automotive_snapshot $AUTO_SNAP"
grep -q 'Automotive' "$AUTO_SNAP" || die "packed_automotive_snapshot_empty"
if command -v strings >/dev/null 2>&1; then
  strings "$CUR/platform/EcomAE.Platform.dll" 2>/dev/null \
    | grep -Fq 'EcomaeIndustryShowcase' \
    && printf 'GATE_OK dll_industry_middleware\n' \
    || printf 'WARN: dll string scan missed EcomaeIndustryShowcase (continuing to HTTP gates)\n'
fi
PUB_SHA_FILE="$CUR/PUBLISHED_GIT_SHA.txt"
if [[ -f "$PUB_SHA_FILE" ]]; then
  PUB_FULL="$(tr -d '[:space:]' < "$PUB_SHA_FILE")"
  printf 'PUBLISHED_GIT_SHA=%s\n' "$PUB_FULL"
  [[ "$PUB_FULL" == "$FULL" ]] || die "published SHA ${PUB_FULL} != branch tip ${FULL}"
fi

sleep 3
fail=0
for host in automotive.ecomae.com food.ecomae.com technology.ecomae.com healthcare.ecomae.com electronics.ecomae.com; do
  code=$(curl -sS -o /tmp/ind_hub.html -w '%{http_code}' --max-time 25 "https://${host}/" || echo 000)
  hdr=$(curl -sS -D - -o /dev/null --max-time 20 "https://${host}/" | tr -d '\r' || true)
  printf 'INDUSTRY_HUB host=%s http=%s\n' "$host" "$code"
  if [[ "$code" != "200" ]]; then fail=1; continue; fi
  # Must NOT be the Blazor marketing hub (live bug when nginx → /marketing/app without snapshots).
  if grep -Eiq 'Unified ERP|ecomae-chrome-surface" content="marketing"|Blockchain BOS Enterprise' /tmp/ind_hub.html; then
    printf 'GATE_BAD industry_hub_still_marketing %s\n' "$host"
    fail=1
    continue
  fi
  if grep -Eiq 'Auto Parts catalog|nero storefront|eParts Cart' /tmp/ind_hub.html; then
    printf 'GATE_BAD industry_hub_storefront_chrome %s\n' "$host"
    fail=1
    continue
  fi
  if echo "$hdr" | grep -Eiq 'x-ecomae-industry-showcase:[[:space:]]*snapshot'; then
    printf 'GATE_OK industry_snapshot_header %s\n' "$host"
  elif grep -Eiq 'Industry|industry|ERP, Control Panel' /tmp/ind_hub.html; then
    printf 'GATE_OK industry_body_markers %s\n' "$host"
  else
    printf 'GATE_BAD industry_hub %s\n' "$host"
    fail=1
  fi
done

# Explicit automotive title gate (PHP snapshot — not Blazor Unified ERP).
curl -sS -o /tmp/ind_auto.html --max-time 25 'https://automotive.ecomae.com/' || true
if grep -Eiq 'Automotive[[:space:]]*&amp;[[:space:]]*Vehicles|Automotive & Vehicles' /tmp/ind_auto.html \
  && ! grep -Eiq 'Unified ERP' /tmp/ind_auto.html; then
  printf 'GATE_OK automotive_title\n'
else
  printf 'GATE_BAD automotive_title_missing\n'
  fail=1
fi

code=$(curl -sS -o /tmp/ind_sub.html -w '%{http_code}' --max-time 25 \
  'https://automotive.ecomae.com/vehicle-dealership-sales' || echo 000)
printf 'INDUSTRY_SUB http=%s\n' "$code"
if [[ "$code" == "200" ]] && grep -Eiq 'dealership' /tmp/ind_sub.html \
  && ! grep -Eiq 'Unified ERP' /tmp/ind_sub.html; then
  printf 'GATE_OK industry_sub\n'
elif [[ "$code" == "302" ]]; then
  printf 'GATE_BAD industry_sub_still_redirects (nginx sub location missing or product redirect)\n'
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
