# Live surface links (Super CP / tenant / ERP / frontend)

Operator chrome and storefronts remain on **PHP** until exact-route staging smoke + release-owner approval. ASP.NET Core already serves allowlisted diagnostics and price lookup.

**Live tenants:** frontend, CP, and ERP presentation/functionality must not change because of ASP.NET migration. Shadows default to `www.ecomae.com` only — see `docs/migration/TENANT_MIGRATION_SAFETY.md` and `bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh`.

Live JSON catalog after deploy: `https://www.ecomae.com/migration/live-surface-links`  
Human side-by-side compare board: `https://www.ecomae.com/migration/compare` (after this branch deploys)

## Honest meters (post-#772 tip vs live www)

| Meter | Wired in tip | Live on www.ecomae.com (probe 2026-08-04) |
| --- | --- | --- |
| Catalog digest-contract | **725 / 726** (~99.9%; `cp-debug-console` php-only by design) | Catalog APIs still allowlisted; not full tip redeploy |
| Surface digests | **127** | **35** healthy (unauth **401**); **99** still **404** (nginx shadow not installed) |
| Presentation apps | **144** | **19** **200**; rest mostly **404** pending shadow install |
| Hybrid TARGETS | **134** | Partial — shells/login + early apps live |
| Field contracts | ~153 | N/A (code/test floor) |
| `cutoverAllowed` | **false** | **false** |
| `readyForPhpRemoval` | **false** | **false** |
| `aspNetInteractiveComplete` | **0** | **0** |

Evidence:

- `docs/migration/evidence/presentation/live-super-cp-tenant-probe.json`
- `docs/migration/evidence/presentation/www-live-exact-route-probe.json`

## Super CP / platform operator (PHP authoritative)

| Surface | Live URL | Stack today |
| --- | --- | --- |
| Frontend / marketing | https://www.ecomae.com/ | PHP |
| Control Panel | https://www.ecomae.com/CP/ | PHP |
| Control Panel alias | https://www.ecomae.com/cp/ | PHP |
| ERP | https://www.ecomae.com/ERP/ | PHP |
| ERP alias | https://www.ecomae.com/erp/ | PHP |
| BOS | https://www.ecomae.com/BOS/ | PHP |
| BOS alias | https://www.ecomae.com/bos/ | PHP |
| Dedicated Super CP host | https://cp.ecomae.com/CP/ | PHP |
| Dedicated Super CP ERP | https://cp.ecomae.com/ERP/ | PHP |
| Dedicated Super CP BOS | https://cp.ecomae.com/BOS/ | PHP |

## ASP.NET Core hybrid / digests (www only — not cutover)

| Surface | Live URL | Notes |
| --- | --- | --- |
| Health | https://www.ecomae.com/health | ASP.NET |
| Zero-PHP console | https://www.ecomae.com/migration/console | Operator board |
| Human compare board | https://www.ecomae.com/migration/compare | PHP vs ASP.NET link matrix |
| Live surface links | https://www.ecomae.com/migration/live-surface-links | JSON |
| Presentation parity | https://www.ecomae.com/migration/presentation-parity | JSON |
| CP / ERP / BOS / storefront shells | `/cp/app` · `/erp/app` · `/bos/app` · `/storefront/app` | Hybrid preview |
| Sample apps live | `/cp/users-app`, `/cp/groups-app`, `/cp/orders`, `/erp/sales-orders-app`, BOS fleet apps, storefront search/cart apps | Exact-route shadows |
| Price lookup API | https://www.ecomae.com/api/v1/price/lookup | Allowlisted |

## Tenant ePartsCart (PHP only — same-to-same)

| Tenant | Frontend | CP | ERP |
| --- | --- | --- | --- |
| ePartsCart | https://epartscart.com/ | https://epartscart.com/CP/ | https://epartscart.com/ERP/ |
| ePartsCart www | https://www.epartscart.com/ | https://www.epartscart.com/CP/ | https://www.epartscart.com/ERP/ |

**Safety (confirmed probe):** `epartscart.com/cp/app` and `epartscart.com/health` → **404 PHP** (no ASP.NET cutover on tenant). Compare ASP.NET only on `www.ecomae.com`.

Tenant BOS is generally Super-CP-only; tenant hosts may still answer `/BOS/` via shared PHP routing, but privileged BOS is intended for `www.ecomae.com` / `cp.ecomae.com`.

## Industry showcase frontends (`*.ecomae.com`)

These are industry marketing/showcase hosts (not dedicated client DB tenants).

| Host | Frontend | CP | ERP |
| --- | --- | --- | --- |
| healthcare | https://healthcare.ecomae.com/ | https://healthcare.ecomae.com/CP/ | https://healthcare.ecomae.com/ERP/ |
| homeliving | https://homeliving.ecomae.com/ | https://homeliving.ecomae.com/CP/ | https://homeliving.ecomae.com/ERP/ |
| retail | https://retail.ecomae.com/ | https://retail.ecomae.com/CP/ | https://retail.ecomae.com/ERP/ |
| fashion | https://fashion.ecomae.com/ | https://fashion.ecomae.com/CP/ | https://fashion.ecomae.com/ERP/ |
| jewellery | https://jewellery.ecomae.com/ | https://jewellery.ecomae.com/CP/ | https://jewellery.ecomae.com/ERP/ |
| food | https://food.ecomae.com/ | https://food.ecomae.com/CP/ | https://food.ecomae.com/ERP/ |
| beauty | https://beauty.ecomae.com/ | https://beauty.ecomae.com/CP/ | https://beauty.ecomae.com/ERP/ |
| sports | https://sports.ecomae.com/ | https://sports.ecomae.com/CP/ | https://sports.ecomae.com/ERP/ |
| pet | https://pet.ecomae.com/ | https://pet.ecomae.com/CP/ | https://pet.ecomae.com/ERP/ |

## Other dedicated tenant / brand hosts

| Tenant | Frontend | CP | ERP |
| --- | --- | --- | --- |
| Electronicae | https://www.electronicae.com/ | https://www.electronicae.com/CP/ | https://www.electronicae.com/ERP/ |
| Style N Look | https://www.stylenlook.com/ | https://www.stylenlook.com/CP/ | https://www.stylenlook.com/ERP/ |
| The Jewellery Trend | https://www.thejewellerytrend.com/ | https://www.thejewellerytrend.com/CP/ | https://www.thejewellerytrend.com/ERP/ |
| Taxofin CA | https://www.taxofinca.com/ | https://www.taxofinca.com/CP/ | https://www.taxofinca.com/ERP/ |

## Exact-route shadows pending on www

Most Wave ~9–23 digests/apps still **404** until CloudPanel. Soft probe after installer lock fix (127 routes): surface digests **~30 PASS / 97 FAIL** on www (FAIL = nginx shadow not installed yet).

Installer/probe now lock **127** surface digest locations (was stale **110**, which blocked full Wave 9–23 install).

```bash
ECOMAE_CONFIRM_INSTALL_SURFACE_DIGEST_SHADOWS=YES bash scripts/cloudpanel_install_surface_digest_shadows.sh
bash scripts/cloudpanel_probe_surface_digest_shadows.sh   # expect 127× ASP.NET 401
ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES bash scripts/cloudpanel_install_presentation_app_shadows.sh
ECOMAE_CONFIRM_INSTALL_STOREFRONT_DIGEST_SHADOWS=YES bash scripts/cloudpanel_install_storefront_digest_shadows.sh
```

Never broad `/api|/cp|/erp|/bos|/storefront`. Never tenant vhosts without `ECOMAE_CONFIRM_TENANT_HOST_SHADOW=YES`.

## Final PHP cutover gate

Weighted meter still **95% / 5%** (PHP runtime decommission residual). That is **not** “95% of routes live on www.” Catalog contract **725/726** on tip; live www shadows lag. Remaining work: install remaining exact-route shadows + dual samples + human `RELEASE_OWNER_APPROVAL.md`. Do not remove PHP-FPM/cron/rewrites until `/migration/php-decommission-readiness` reports ready with approval attached.
