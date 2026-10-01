# ERP dummy-data rehearsal — 2026-10-01

## Safety boundary

This rehearsal used only the local MariaDB database `ecomae` on `127.0.0.1:3306`
and the local ASP.NET process on port `5080`. No live tenant database, live
schema, production deployment, Nginx configuration, or production row was
used or changed.

The local operator authenticated through the PHP-compatible login bridge using
the configured tenant host `www.ecomae.com`. Credentials and secret values are
not included in this evidence.

## Coverage executed

- ERP write catalog inventory: 323 actions.
- Authenticated registry dry-run probes: 323/323 HTTP 200.
- Dry-run invariants: 323/323 returned `writes: 0`, `writesBlocked: true`,
  `phpAuthoritative: true`, `status: dry-run-validated`, and
  `validation_code: ok`.
- Confirmed local dummy write: one tagged sales order was created, read back
  from `epc_erp_sales_orders`, then removed.
- Confirmed-write response: HTTP 200 with `writes: 1`, persisted voucher
  `SO-2026-00001`, VAT calculation, and total calculation.
- Cleanup: the tagged sales-order row and its locally-created voucher sequence
  row were removed; the final tagged-row count was zero.
- Negative branches: invalid confirmed sales-order input and missing sales-order
  status target both returned `writes: 0` and did not create tagged rows.
- Legacy route branch: `/erp/ajax/so-status` dry-run was exercised separately
  from the dedicated lifecycle route.

## Findings

1. **Registry dry-run gate passed.** The registry is safe for broad rehearsal
   probing: it blocks writes and preserves PHP authority for every catalog
   action tested.
2. **Sales-order confirmed write passed locally.** The ASP.NET writer persisted
   the document and calculated VAT/total values against the throwaway database.
3. **Audit evidence is incomplete.** The local write did not create
   `epc_erp_audit_log`, so audit-readback is **unverified**, not passed.
4. **Dedicated dry-run metadata mismatch.** The dedicated `/erp/ajax/so-save`
   dry-run returned `writes: 0` and `writesBlocked: true`, but
   `phpAuthoritative: false`. This contradicts the migration policy and must be
   corrected before the dedicated route can satisfy the PHP-authoritative
   acceptance gate.
5. **This is not ERP completion evidence.** Registry coverage and one successful
   local write do not prove PHP functional parity, browser parity, tenant
   isolation, recovery, UAT, production readiness, or tenant promotion.

## Live-tenant migration gate

Live ERP data migration is a separate, later phase. For each tenant, the
required sequence is:

1. Freeze the tenant's migration window and capture a verified backup.
2. Inventory PHP-owned tables, schema definitions, legal entities, business
   units, departments, sites/warehouses, cost centres, financial dimensions,
   dimension values, intercompany mappings, sequences, documents, and audit
   evidence.
3. Run a no-write migration preview and produce row counts, checksums, foreign
   key/orphan checks, monetary totals, tax totals, and dimension/intercompany
   reconciliation.
4. Apply only additive, reversible, PHP-compatible migration steps.
5. Read back through both PHP and ASP.NET and compare representative documents,
   balances, statuses, reports, exports, and audit trails.
6. Run tenant isolation, permissions, browser, recovery, rollback, UAT, and
   production smoke gates.
7. Promote ASP.NET ownership only for that tenant, retaining PHP fallback and
   rollback until the post-promotion evidence is accepted.

No live tenant promotion or live data mutation is authorized by this rehearsal.
