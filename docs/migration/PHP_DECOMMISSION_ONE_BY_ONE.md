# PHP decommission — one by one (hard gates)

**Goal:** 100% ASP.NET / 0 PHP. **Look stays same-to-same; only speed/security may improve.**

## Hard locks (never invent)

- `cutoverAllowed=false` until dual-sample same-to-same + human approval
- `readyForPhpRemoval=false` / `ReadyToRemovePhp=false` until checklist complete
- Do **not** invent `RELEASE_OWNER_APPROVAL.md` or `MODULE_FUNCTION_PARITY_PASS`
- Do **not** delete PHP source while readiness is blocked

## Sequence

1. **Scaffold contract** — menus/fields/ajax goldens (done: 726/726)
2. **Install exact-route shadows (www only)** — marketing 37 + storefront digests 7/7 + login/compare routes
3. **Dual-sample** — admin + customer cookies; digests + module-ajax; presentation recheck `status=pass`
4. **Functional live-smoke** — 7/7 captured
5. **Tenant same-to-same** — then tenant exact-route (still host-by-host)
6. **Human approval** — `RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK`
7. **Runtime decommission (one confirm)**  
   `ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission.sh`  
   (only when `/migration/php-decommission-readiness` → `readyToRemovePhp=true`)
8. **Source removal** — separate human-owned PR after runtime is proven stable; not agent-invented

## Operator status

```bash
bash scripts/cloudpanel_php_decommission_gated.sh status
```

If `readyToRemovePhp=false`, PHP stays. No file deletes.
