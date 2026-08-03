# Live surface links (Super CP / tenant / ERP / frontend)

Operator chrome and storefronts remain on **PHP** until exact-route staging smoke + release-owner approval. ASP.NET Core already serves allowlisted diagnostics and price lookup.

**Live tenants:** frontend, CP, and ERP presentation/functionality must not change because of ASP.NET migration. Shadows default to `www.ecomae.com` only — see `docs/migration/TENANT_MIGRATION_SAFETY.md` and `bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh`.

Live JSON catalog after deploy: `https://www.ecomae.com/migration/live-surface-links`

## Super CP / platform operator

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
| Dedicated Super CP ERP | https://cp.ecomae.com/ERP/ | PHP (probe noted intermittent 404; prefer https://www.ecomae.com/ERP/) |
| Dedicated Super CP BOS | https://cp.ecomae.com/BOS/ | PHP |

## ASP.NET Core (already live, allowlisted)

| Surface | Live URL |
| --- | --- |
| Health | https://www.ecomae.com/health |
| Zero-PHP completion | https://www.ecomae.com/migration/zero-php-completion |
| PHP decommission readiness | https://www.ecomae.com/migration/php-decommission-readiness |
| Presentation parity | https://www.ecomae.com/migration/presentation-parity |
| Live surface links | https://www.ecomae.com/migration/live-surface-links |
| Surface parity | https://www.ecomae.com/migration/surface-parity |
| Surface field parity | https://www.ecomae.com/migration/surface-field-parity |
| CP / ERP / BOS / storefront parity boards | /cp/parity · /erp/parity · /bos/parity · /storefront/parity (loopback) |
| Auth / data / catalog / price parity | /auth/session/parity · /auth/api-client/parity · /migration/data-parity · /api/v1/catalog/parity · /api/v1/price/parity |
| Price lookup API | https://www.ecomae.com/api/v1/price/lookup |

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

## Dedicated tenant / brand hosts

| Tenant | Frontend | CP | ERP |
| --- | --- | --- | --- |
| Electronicae | https://www.electronicae.com/ | https://www.electronicae.com/CP/ | https://www.electronicae.com/ERP/ |
| Style N Look | https://www.stylenlook.com/ | https://www.stylenlook.com/CP/ | https://www.stylenlook.com/ERP/ |
| The Jewellery Trend | https://www.thejewellerytrend.com/ | https://www.thejewellerytrend.com/CP/ | https://www.thejewellerytrend.com/ERP/ |
| Taxofin CA | https://www.taxofinca.com/ | https://www.taxofinca.com/CP/ | https://www.taxofinca.com/ERP/ |
| ePartsCart | https://epartscart.com/ | https://epartscart.com/CP/ | https://epartscart.com/ERP/ |
| ePartsCart www | https://www.epartscart.com/ | https://www.epartscart.com/CP/ | https://www.epartscart.com/ERP/ |

Tenant BOS is generally Super-CP-only; tenant hosts may still answer `/BOS/` via shared PHP routing, but privileged BOS is intended for `www.ecomae.com` / `cp.ecomae.com`.

## Catalog exact-route shadows (www)

**Live (public unauth ASP.NET JSON 401):** price lookup + **all wired catalog API paths (18/18)** through `brand-parts` + **all 30 CP/ERP/BOS surface digests** + **all 4 storefront digests** (digest gate: `unauthorized`).

**Surface digests:** **30 / 30** live. **Storefront digests:** **4 / 4** live (`cloudpanel_install_storefront_digest_shadows.sh`; never broad `/storefront`).

**Blazor SSR console:** `/migration/console` — Zero-PHP operator board (requires `UseAntiforgery` redeploy). Not product chrome cutover.

## Exact-route digests pending nginx shadow

None for wired digest sets. Product chrome (`/`, `/CP/`, `/ERP/`, `/BOS/`) remains PHP by design until intentional shell cutover.

Never broad `/api|/cp|/erp|/bos|/storefront`.

## Final PHP cutover gate

Weighted meter still **95% / 5%** (PHP runtime decommission residual). That is **not** “95% of routes live.” Catalog **18/18**, surface digests **30/30**, storefront digests **4/4**; product chrome still PHP. Remaining work: dual samples + human `RELEASE_OWNER_APPROVAL.md`. Do not remove PHP-FPM/cron/rewrites until `/migration/php-decommission-readiness` reports ready with approval attached.
