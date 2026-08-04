# Module function parity — live gate (honest)

**Locks:** `cutoverAllowed=false`, `readyForPhpRemoval=false`, `aspNetInteractiveComplete=0`.

This file is **not** `MODULE_FUNCTION_TEST_PASS.md`. Do **not** invent `MODULE_FUNCTION_PARITY_PASS`.

## Current truth

| Area | Scaffold | Interactive ASP.NET complete | Authoritative runtime |
|---|---|---|---|
| CP brochure / digests / hybrid | 726/726 menus; digests + hybrid apps | **0** | PHP writes/menus |
| ERP tabs / ajax_erp | 321 dry-run goldens | **0** | PHP |
| BOS modules / ajax | 231 dry-run goldens | **0** | PHP |
| Storefront digests | 7/7 wired (live shadows lag) | **0** | PHP checkout/payment |
| Functional 7-flow suite | static floors green | live-smoke **0/7 captured** | PHP |

## Required before MODULE_FUNCTION_PARITY_PASS

1. CloudPanel dual-sample (admin + customer cookies) for digests + module-ajax field parity
2. Functional live-smoke stubs → `captured` with real artifacts (warehouse, ERP report, e-invoice, CT, process flow, OMS, Super CP)
3. Same-to-same presentation recheck `php-vs-aspnet-recheck.json` → `status=pass` (live, not invented)
4. Human `RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK`

Until then: **PHP remains authoritative. Do not delete PHP files. Do not disable PHP-FPM/cron.**
