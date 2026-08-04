# Live surface links (Super CP / tenant / ERP / frontend)

Operator chrome and storefronts remain on **PHP** until exact-route staging smoke + release-owner approval. ASP.NET Core already serves allowlisted diagnostics and price lookup.

**Live tenants:** frontend, CP, and ERP presentation/functionality must not change because of ASP.NET migration. Shadows default to `www.ecomae.com` only — see `docs/migration/TENANT_MIGRATION_SAFETY.md` and `bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh`.

Live JSON catalog after deploy: `https://www.ecomae.com/migration/live-surface-links`  
Human side-by-side compare board: `https://www.ecomae.com/migration/compare` (after this branch deploys)

## Honest meters (post-#772 tip vs live www)

| Meter | Wired in tip | Live on www.ecomae.com (probe 2026-08-04) |
| --- | --- | --- |
| Catalog digest-contract | **725 / 726** (~99.9%; `cp-debug-console` php-only by design) | Catalog APIs still allowlisted; not full tip redeploy |
| Surface digests | **127** | **127 / 127** healthy (unauth **401**) |
| Presentation apps | **144** | **142 / 144** **200** |
| Storefront digests | **6** | **6 / 6** healthy (unauth **401**) |
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
| Frontend / marketing | https://www.ecomae.com/ | PHP-primary until ASP.NET `/marketing/app` cutover (`epm-hub`) |
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
| CP / ERP / BOS / storefront / marketing shells | `/cp/app` · `/erp/app` · `/bos/app` · `/storefront/app` · `/marketing/app` | Hybrid preview (not cutover) |
| Marketing presentation lock | https://www.ecomae.com/migration/marketing-presentation-lock | Parity gate — target ASP.NET; live `/` PHP-primary until cutover |
| ASP.NET zero-PHP path | https://www.ecomae.com/migration/aspnet-zero-php-path | Phase board toward 100% ASP.NET / 0 PHP |
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
There is **no** `industry.ecomae.com` host — use `[slug].ecomae.com`. Trading → `wholesale.ecomae.com`.

**Live stack today:** PHP product chrome on every industry host (same look source).  
**ASP.NET compare (www only):** `/marketing/industries` + `/marketing/app` exact-route shadows (install required).  
**Gate:** `bash scripts/run_industry_ecomae_frontend_parity.sh` · `ECOMAE_INDUSTRY_LIVE=1` for probes.

| Host | Frontend | CP | ERP |
| --- | --- | --- | --- |
| agriculture | https://agriculture.ecomae.com/ | https://agriculture.ecomae.com/CP/ | https://agriculture.ecomae.com/ERP/ |
| automotive | https://automotive.ecomae.com/ | https://automotive.ecomae.com/CP/ | https://automotive.ecomae.com/ERP/ |
| beauty | https://beauty.ecomae.com/ | https://beauty.ecomae.com/CP/ | https://beauty.ecomae.com/ERP/ |
| cleaning | https://cleaning.ecomae.com/ | https://cleaning.ecomae.com/CP/ | https://cleaning.ecomae.com/ERP/ |
| construction | https://construction.ecomae.com/ | https://construction.ecomae.com/CP/ | https://construction.ecomae.com/ERP/ |
| education | https://education.ecomae.com/ | https://education.ecomae.com/CP/ | https://education.ecomae.com/ERP/ |
| electronics | https://electronics.ecomae.com/ | https://electronics.ecomae.com/CP/ | https://electronics.ecomae.com/ERP/ |
| energy | https://energy.ecomae.com/ | https://energy.ecomae.com/CP/ | https://energy.ecomae.com/ERP/ |
| fashion | https://fashion.ecomae.com/ | https://fashion.ecomae.com/CP/ | https://fashion.ecomae.com/ERP/ |
| finance | https://finance.ecomae.com/ | https://finance.ecomae.com/CP/ | https://finance.ecomae.com/ERP/ |
| food | https://food.ecomae.com/ | https://food.ecomae.com/CP/ | https://food.ecomae.com/ERP/ |
| healthcare | https://healthcare.ecomae.com/ | https://healthcare.ecomae.com/CP/ | https://healthcare.ecomae.com/ERP/ |
| homeliving | https://homeliving.ecomae.com/ | https://homeliving.ecomae.com/CP/ | https://homeliving.ecomae.com/ERP/ |
| hospitality | https://hospitality.ecomae.com/ | https://hospitality.ecomae.com/CP/ | https://hospitality.ecomae.com/ERP/ |
| jewellery | https://jewellery.ecomae.com/ | https://jewellery.ecomae.com/CP/ | https://jewellery.ecomae.com/ERP/ |
| logistics | https://logistics.ecomae.com/ | https://logistics.ecomae.com/CP/ | https://logistics.ecomae.com/ERP/ |
| manufacturing | https://manufacturing.ecomae.com/ | https://manufacturing.ecomae.com/CP/ | https://manufacturing.ecomae.com/ERP/ |
| media | https://media.ecomae.com/ | https://media.ecomae.com/CP/ | https://media.ecomae.com/ERP/ |
| nonprofit | https://nonprofit.ecomae.com/ | https://nonprofit.ecomae.com/CP/ | https://nonprofit.ecomae.com/ERP/ |
| pet | https://pet.ecomae.com/ | https://pet.ecomae.com/CP/ | https://pet.ecomae.com/ERP/ |
| printing | https://printing.ecomae.com/ | https://printing.ecomae.com/CP/ | https://printing.ecomae.com/ERP/ |
| professional | https://professional.ecomae.com/ | https://professional.ecomae.com/CP/ | https://professional.ecomae.com/ERP/ |
| rental | https://rental.ecomae.com/ | https://rental.ecomae.com/CP/ | https://rental.ecomae.com/ERP/ |
| retail | https://retail.ecomae.com/ | https://retail.ecomae.com/CP/ | https://retail.ecomae.com/ERP/ |
| security | https://security.ecomae.com/ | https://security.ecomae.com/CP/ | https://security.ecomae.com/ERP/ |
| sports | https://sports.ecomae.com/ | https://sports.ecomae.com/CP/ | https://sports.ecomae.com/ERP/ |
| technology | https://technology.ecomae.com/ | https://technology.ecomae.com/CP/ | https://technology.ecomae.com/ERP/ |
| wholesale | https://wholesale.ecomae.com/ | https://wholesale.ecomae.com/CP/ | https://wholesale.ecomae.com/ERP/ |

## Other dedicated tenant / brand hosts

| Tenant | Frontend | CP | ERP |
| --- | --- | --- | --- |
| Electronicae | https://www.electronicae.com/ | https://www.electronicae.com/CP/ | https://www.electronicae.com/ERP/ |
| Style N Look | https://www.stylenlook.com/ | https://www.stylenlook.com/CP/ | https://www.stylenlook.com/ERP/ |
| The Jewellery Trend | https://www.thejewellerytrend.com/ | https://www.thejewellerytrend.com/CP/ | https://www.thejewellerytrend.com/ERP/ |
| Taxofin CA | https://www.taxofinca.com/ | https://www.taxofinca.com/CP/ | https://www.taxofinca.com/ERP/ |

## Exact-route shadows on www (installed)

Surface digests **128/128**, storefront digests **7 wired** (search/cart/checkout/orders/garage/profile/account-summary; live auth-gate count may lag until CloudPanel install), presentation apps **~142/144** after `:5080→:5100` repair.

Re-probe anytime:

```bash
cd /opt/ecomae-aspnet-source
bash scripts/cloudpanel_probe_surface_digest_shadows.sh      # expect 127× 401
bash scripts/cloudpanel_probe_storefront_digest_shadows.sh   # expect 7× 401
```

Never broad `/api|/cp|/erp|/bos|/storefront`. Never tenant vhosts without `ECOMAE_CONFIRM_TENANT_HOST_SHADOW=YES`.

## Final PHP cutover gate

Weighted meter still **95% / 5%** (PHP runtime decommission residual). That is **not** “95% of routes live on www.” Catalog contract **725/726** on tip; live www shadows lag. Remaining work: install remaining exact-route shadows + dual samples + human `RELEASE_OWNER_APPROVAL.md`. Do not remove PHP-FPM/cron/rewrites until `/migration/php-decommission-readiness` reports ready with approval attached.
