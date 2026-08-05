# ASP.NET Core architecture — protocol inline review

**Confirmed model:** ASP.NET Core is the **live product-primary** runtime. PHP stays **reference-only** under `/php-reference/*` until ASP.NET is fully functional (interactive writes, dual-sample green, decommission gate).

Locks (do not invent green):

| Flag | Value |
| --- | --- |
| `cutoverAllowed` | `false` |
| `readyForPhpRemoval` | `false` |
| `RequirePhpFallback` | `true` until dual-sample-green per exact route |
| `aspNetInteractiveComplete` | `0` until human module-function PASS |
| `KeepPhpProjectAvailable` | `true` |
| Mode | `aspnet-primary-php-reference` |
| Product `/bos` | Super-CP only (`www.ecomae.com` / `ecomae.com` / `cp.ecomae.com`); tenants **404** |

## Runtime map

```
Browser product URL (/ /cp /erp /bos?/storefront)
        │
        ▼
nginx classic-entry ──proxy──► Kestrel :5100 (ASP.NET Blazor + digests)
        │
        ├── /php-reference/* ──rewrite──► index.php?epc_php_reference=… (PHP shells)
        ├── /en/shop/… (interim PHP full pages for catalog/search/cart until apps green)
        └── stub /storefront/*-app ──302──► /en/… PHP (edge + ASP.NET middleware)

Workers / cron: PHP authoritative; EcomAE.Workers = dry-run only
Writes: PHP authoritative until dual-sample + module-function PASS
```

## What is aligned

- Classic-entry nginx: product URLs → `:5100`; PHP compare only `/php-reference/*`
- Middleware: product remap, stub→PHP canonical, BOS host gate, cutover headers (`X-EcomAE-Target-Runtime`, `X-EcomAE-PHP-Fallback`)
- Blazor chrome clones PHP look; nav primary hrefs are ASP.NET; reference links use `/php-reference/*`
- Module endpoints: read digests + write dry-runs; interactive complete = 0
- Reporters hard-code `cutoverAllowed=false` / `readyForPhpRemoval=false`

## Gaps fixed in this change set

1. **`epc_php_reference` router** — `content/general_pages/epc_php_reference_router.php` boots CP/ERP/BOS/storefront PHP shells in-process (no bounce to product `/cp|/erp|/bos`).
2. **Nginx reference locations** — internal `rewrite … last` so `/php-reference/*` stays the browser URL.
3. **Hybrid iframe** — only loads `/php-reference/*` (rewrites legacy `/CP|/ERP|/BOS` deeplinks).

## Remaining until “fully functional”

1. Republish `:5100` on CloudPanel after every chrome/protocol change (`FORCE_LIVE` → `RESULT=PASS`).
2. Storefront apps dual-sample green → retire `StorefrontPhpCanonical` interim `/en/…` redirects.
3. Live-smoke 7/7 + presentation `status=pass` + `MODULE_FUNCTION_TEST_PASS.md`.
4. Per-route `RequirePhpFallback=false` only when dual-sample green.
5. Separate approval before `ReadyToRemovePhp` / PHP source deletion.
6. Reconcile stale boards that still say “tenants PHP-primary” (`LiveSurfaceLinkReporter`, old compare docs).

## Operator compare

- Product: `https://www.epartscart.com/` `/cp` `/erp` (ASP.NET)
- PHP reference: `https://www.epartscart.com/php-reference/storefront|cp|erp`
- Board: `GET /migration/php-reference-mode` · `/migration/compare`
