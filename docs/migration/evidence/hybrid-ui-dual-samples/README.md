# Hybrid UI dual-sample evidence

**Policy:** ASP.NET Blazor `*-app` (and `/cp/orders`) routes are **www exact-route presentation previews** only. Live product chrome (`/`, `/CP/`, `/ERP/`, `/BOS/`, tenant hosts, epartscart.com) and all writes remain **PHP-authoritative**. `cutoverAllowed` / `readyForPhpRemoval` stay **false**. Never invent `RELEASE_OWNER_APPROVAL.md`.

## What this pack covers

| Side | Behavior |
| --- | --- |
| ASP.NET `location =` shadows on www | Blazor SSR read UI under `Php*DesktopChrome`, usually over a JSON digest |
| PHP product paths | Authoritative interactive console / edits / tenant UX |
| Digests | JSON contracts already have their own dual-sample family under `surface-parity/` |

Shells (`/cp|/erp|/bos|/storefront/app`) and login bridges use the Batch 3 login-cookie harness — not this pack.

## Still PHP-only

- Tenant and industry host product chrome
- All create/update/delete paths linked from hybrid UIs
- BOS native `$_SESSION` modules under `/BOS/`
- Storefront checkout / guest cart / garage edits on epartscart.com

## Capture / compare (CloudPanel)

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
cd /opt/ecomae-aspnet-source   # or repo root
# Optional live HTML (never print cookie values):
# export ECOMAE_ADMIN_COOKIE_HEADER='admin_session=…; admin_u_id=…'
# export ECOMAE_CUSTOMER_COOKIE_HEADER='session=…; u_id=…'
# export ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES=1
# export ECOMAE_ASPNET_BASE_URL=http://127.0.0.1:5100
bash scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh
python3 scripts/compare_hybrid_ui_dual_samples.py \
  --samples-dir docs/migration/evidence/hybrid-ui-dual-samples \
  --out docs/migration/evidence/hybrid-ui-dual-samples/compare-result.json
```

Without cookies the capture script writes **contract stubs** so foundation checks have a stable floor. Stubs are enough for CI; live marker passes require operator cookies after presentation shadows are installed.

Always keep:

```bash
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh   # expect status=pass, cutoverAllowed=false
```

## Pass meaning

- Hybrid UI samples: `phpAuthoritative=true`, `wwwPreviewOnly=true`, `tenantChromePhp=true`
- Samples must not claim `cutoverAllowed` / `readyForPhpRemoval`
- Compare result always `"cutoverAllowed": false`
- Live mode (non-stub): HTTP 200 + Blazor marker present; PHP deeplink is advisory
- Contract stubs do **not** authorize exact-route product cutover

`sf-profile` may be a contract stub until `/storefront/profile-app` lands on the deployed branch; that does not unlock Batch 6.
