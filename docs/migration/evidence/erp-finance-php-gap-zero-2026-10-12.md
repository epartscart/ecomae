# ERP finance PHP gap inventory — driven to 0 (2026-10-12)

Request: "165 finance PHP gap files — 141 in `content/shop/finance/`, 24 in `cp/content/shop/finance/`. Drive this inventory to 0 (`scripts/php_reference_gap_inventory.py`, max-gap 165)."

## Baseline (origin/main `3b7cd378b`)

`python3 scripts/php_reference_gap_inventory.py --json out.json` → 732 gap files repo-wide; filtered to the two
finance trees: **149** (131 `content/shop/finance/`, 18 `cp/content/shop/finance/`). The 165 figure predates
#2136–#2142 (ops pages built) and #2145–#2153 (fresh-tenant schema twins), which already referenced 16 of them.

## Method (no PHP deleted, no flags changed)

The script counts a PHP file as a gap when nothing under `aspnet/src` (excluding catalog/inventory/dashboard-tagged
filenames) names its path, unique basename, or every function it defines, and it is not in
`docs/migration/inventory/PHP_RETIRED.tsv`. For each of the 149 files:

1. Found PHP includers (`rg` over `*.php` outside aspnet/docs/tests) to classify reachable vs catalogue-only.
2. Found the ASP.NET owner by matching the PHP file's top-level function names against identifiers in
   `aspnet/src` (`/home/ubuntu/gap_owner_candidates.json` method; e.g. `epc_erp_wms.php` → `ErpWmsWaveCreateWriteService`,
   `epc_uae_tax_compliance.php` → `ErpUaeTaxFtaLegislation`, `epc_erp_external_reports_build.php` → `ErpExternalReportingHtml`).
3. **136 files → mapped**: a `// Legacy PHP references twinned by this file` header on the verified owner (74 owner files),
   following the existing `(<c>content/shop/finance/epc_erp_gl.php</c>)` convention. Payment-gateway hops
   (`pay_for_order.php`, `payment_systems/*`) are listed on `ShopPayForOrderService` as the PHP-authoritative handoff —
   they are not ported and remain PHP per the checkout rule.
4. **13 files → retired** in `PHP_RETIRED.tsv` with a reason: catalogue-only modules referenced only by
   `epc_erp_bos_matrix.php` / one-off setup scripts (transport, plant maintenance, real estate, EHS, contingent
   workforce, PLM, datalink, collab, compliance), the unreferenced `epc_erp_audit_tools.php`, `epc_erp_bos_matrix.php`
   itself, the PHP-CP CMS installer `epc_tax_toolkit_cp_install.php`, and `cdekpay/go_to_pay_old.php`.
   `epc_erp_governance.php` was *not* retired (it has live callers via `epc_boc_kernel.php`) and is mapped instead.

## Result

```
python3 scripts/php_reference_gap_inventory.py --md docs/migration/inventory/PHP_REFERENCE_GAP_INVENTORY.md
finance gaps remaining: 0        (content/shop/finance 0, cp/content/shop/finance 0)
repo-wide gap files: 732 → 583   (the remainder is Cursor-owned CP/storefront/BOS surface)
```

`--max-gap 165` passes; the finance ratchet is now 0. This is a reference-coverage inventory, not acceptance:
formal ERP process acceptance stays 0/15, `readyForPhpRemoval` / `cutoverAllowed` stay false, PHP stays on disk.
