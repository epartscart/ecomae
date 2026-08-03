# Chrome parity gap matrix (PHP ↔ ASP.NET)

Authoritative product chrome remains **PHP** until intentional exact-route cutover + dual-sample evidence + human `RELEASE_OWNER_APPROVAL.md`. This matrix tracks hybrid strengthen work so CP (platform + tenants), ERP, BOS, and login present/work without removing PHP.

## Status legend

| Status | Meaning |
| --- | --- |
| PHP-live | Public HTML/UX still PHP |
| ASP.NET exact-route | nginx `location =` → Kestrel |
| Hybrid | ASP.NET shell/nav + PHP modules for unported work |
| Gap | Known missing parity — do not cut over |

## Surfaces

| Surface | PHP URL | ASP.NET preview / bridge | Presentation | Functionality | Gap notes |
| --- | --- | --- | --- | --- | --- |
| Platform CP chrome | `/CP/` (login when unauth) | `/cp/login` → `/cp/app` (auth) | Hybrid; blank PhpChromeLayout | Digests ASP.NET; modules PHP via nav | Full desktop widgets / ACL menu cache still PHP |
| Tenant CP chrome | `tenant/CP/` | same `/cp/app` when shadowed on tenant vhost | Hybrid | Tenant DB via registry | Per-tenant shadow install still operator-driven |
| CP login | `/CP/` auth plugin | `/cp/login` + `POST /auth/login/admin` | Bridge UI | Writes PHP-compatible `sessions` + `admin_*` cookies when `SecretSuccession` set | Rate-limit / social / demo / shared-ERP picker still PHP |
| Platform ERP | `/ERP/` | `/erp/app` | Hybrid | Digests ASP.NET; areas PHP | Full ERP desktop + writes PHP |
| Client / platform ERP routers | `/cp/client-erp/…`, `/cp/platform-erp/` | link via PHP | PHP-live | PHP | Not cut over |
| ERP login | ERP routers / CP plugin | `/erp/login` | Bridge UI | Same admin session mint | Shared-tenant picker still PHP |
| Super BOS | `/BOS/` | `/bos/app` | Hybrid | Digests ASP.NET | **BOS `$_SESSION` ≠ MySQL admin cookies** — use `/BOS/` for full fleet UX |
| BOS login | `POST /bos/?action=login` | `/bos/login` (admin bridge) | Bridge + PHP link | Admin cookies for digests only | Native BOS PHP session login remains required for modules |
| Storefront home | `epartscart.com/` | `/storefront/app` | Preview | Digests ASP.NET | Marketing/cart/checkout PHP |
| Storefront login | PHP customer login | `/storefront/login` | Bridge UI | Customer `session`/`u_id` cookies | Full account UX PHP |
| Digests CP/ERP/BOS | n/a (JSON) | 30/30 exact-routes | n/a | Live | Contract dual-sample recorded |
| Storefront digests | n/a | 4/4 exact-routes | n/a | Live | Customer cookie for 200 |
| Catalog API | PHP UMAPI fill | 18/18 exact-routes | n/a | Live cache readers | Misses remain PHP/UMAPI |

## Strengthen rules (do not violate)

1. Never invent `RELEASE_OWNER_APPROVAL.md`.
2. Never broad-cutover `/`, `/cp`, `/erp`, `/bos`, `/storefront`.
3. Hybrid nav must link PHP for unported modules — presentation must not orphan functionality.
4. Login bridge requires `EcomAE__SecretSuccession` (= PHP `secret_succession`) in `/etc/ecomae-aspnet/platform.env`; without it, UI points to PHP login.
5. Password hash upgrade stays PHP-authoritative (ASP.NET verifies only).

## Operator install (presentation + login shadows)

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
cd /opt/ecomae-aspnet-source
git fetch origin main && git checkout -f main && git reset --hard origin/main
bash scripts/cloudpanel_find_and_redeploy.sh
# Optional for login writes:
#   echo 'EcomAE__SecretSuccession=<php secret_succession>' >> /etc/ecomae-aspnet/platform.env
ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES \
  bash scripts/cloudpanel_install_presentation_app_shadows.sh
```

## Side-by-side review

| Compare | PHP | ASP.NET |
| --- | --- | --- |
| CP | `/CP/` | `/cp/app` + `/cp/login` |
| ERP | `/ERP/` | `/erp/app` + `/erp/login` |
| BOS | `/BOS/` | `/bos/app` + `/bos/login` |
| Storefront | `https://epartscart.com/` | `/storefront/app` + `/storefront/login` |
| Parity board | — | `/migration/presentation-parity` |
