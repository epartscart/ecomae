---
name: testing-erp-aspnet-local
description: How to run and end-to-end test the ASP.NET EcomAE ERP (aspnet/src/EcomAE.Platform) locally against a throwaway MariaDB tenant DB, including login, ERP write endpoints, dry-run gates and DB corroboration. Use when testing /erp routes or ERP write services.
---

# Local end-to-end testing of the ASP.NET ERP

## Build & run
```bash
export PATH=$PATH:/home/ubuntu/.dotnet          # net10.0 SDK
cd /home/ubuntu/repos/ecomae/aspnet
dotnet build src/EcomAE.Platform
```
Run it in a *persistent* shell (a plain `nohup` has been observed not to survive):
```bash
export ConnectionStrings__TenantRegistry="Server=127.0.0.1;Port=3306;Database=ecomae;Uid=ecomae;Pwd=<local-test-pw>;AllowUserVariables=true;"
export EcomAE__SecretSuccession="<local test secret>"
cd aspnet/src/EcomAE.Platform && dotnet run --no-build --urls http://0.0.0.0:5080
```
Use host `http://www.ecomae.com:5080` (not localhost) — the app keys tenant resolution off the Host header.

## Login bridge gotchas
- `/erp/login` POST redirecting to `?error=bridge_not_configured` means the tenant connection
  string and/or `EcomAE__SecretSuccession` are missing, not bad credentials.
- The legacy password hash is derived from `EcomAE__SecretSuccession`; with a local test secret you
  must recompute the operator row's hash in the local DB, otherwise login always fails.
- Operator account used historically: `ecomaedxb@gmail.com`, capabilities `cp, erp, bos, api`.
- For shell probes, capture the session cookie once (`curl -c jar -d "contact=...&password=..." .../erp/login`)
  and reuse `-b jar`; the browser session is independent, so both can be active.

## Testing ERP write endpoints
- Write endpoints (`/erp/ajax/so-save|so-status|so-to-invoice|po-save|po-status`,
  `/erp/sales-orders/delete`, `/erp/purchase-orders/delete`, `/erp/cash-entries/*`) only perform real
  writes with JSON `confirmWrites: true`; without it they return the dry-run envelope
  (`writes:0`, `writesBlocked:true`, `phpAuthoritative:true`). Always regression-check both branches.
- Auth gating is not uniform: `/erp/ajax/*` returns `401` JSON for anonymous callers while
  page-form POSTs such as `/erp/purchase-orders/delete` may return `302` to `/erp/login`. Treat a
  redirect as a valid rejection, but assert no DB rows/voucher sequence were consumed.
- Business-rule failures return HTTP 200 with `status:false, writes:0` and a PHP-parity message
  (e.g. "Only draft purchase orders can be deleted — cancel posted ones instead").
- Some ASP.NET list pages are read-only digests (`/erp/purchase-orders-app` has no save/status/delete
  buttons), so those endpoints must be exercised over HTTP and verified via the list UI + DB.
  `/erp/sales-orders-app` does have a New form and per-row Confirm/Cancel/Invoice/Delete buttons.
- The platform does not call `UseStaticFiles`; wwwroot ERP scripts are explicitly allowlisted in
  `Erp/ErpAppAssets.cs`. A newly added JS file that isn't in the allowlist will 404 with
  `{"ok":false,"error":"unknown-erp-asset"}` — good UI proof the script actually loaded is to click a
  row action and see the status flip.

## Route names differ from the feature names
Always resolve the constant in `Routing/EcomAeRoutes.cs` before probing — descriptions in a PR body
may not match the URL. Observed traps:
- supplier settlement is `POST /erp/suppliers/settlement` (NOT `/erp/supplier-settlement`; a wrong path
  gives `405` when authenticated and a misleading `302` to `/erp/login` when anonymous, because the
  auth redirect happens before route matching — so a `302` alone does not prove the route exists).
- supplier payment is `POST /erp/ajax/supplier-payment`; PO receiving/conversion are
  `POST /erp/ajax/po-receive-lines` and `POST /erp/ajax/po-to-invoice`.
- `po-receive-lines` takes `receivedJson` as a JSON *string* mapping `epc_erp_po_lines.id` →
  cumulative received qty; it is cumulative (not incremental) and clamped to `qty - qty_cancelled`.
- `po-save` accepts `linesJson` (`[{"description":..,"qty":..,"unit_cost_ex_vat":..}]`) but has no
  `orderId` field — link a PO to a shop order with SQL when you need the order-guard scenarios.

## Order-completion guard (`Erp/ErpOrderCompletionGuard.cs`)
ERP postings that reference a shop order (settlement `orderId`, or a `purchaseId` whose purchase has
`order_id > 0`) are refused unless the order is complete. The legacy shop tables usually do NOT exist
in the local throwaway DB, and missing status-reference tables are swallowed → guard returns
"not complete", so *every* guarded write fails until you seed:
```sql
CREATE TABLE IF NOT EXISTS shop_orders_statuses_ref (id int PRIMARY KEY, `order` int DEFAULT 0, for_finish tinyint DEFAULT 0, name varchar(64) DEFAULT '');
CREATE TABLE IF NOT EXISTS shop_orders (id int PRIMARY KEY, successfully_created tinyint DEFAULT 0, status int DEFAULT 0);
REPLACE INTO shop_orders_statuses_ref VALUES (1,1,0,'In progress'),(5,5,1,'Completed');
REPLACE INTO shop_orders VALUES (101,1,1),(102,1,5);  -- 101 = incomplete, 102 = completed
```
With `shop_orders.status` present and order-level finish statuses seeded, the guard uses the
order-level path and never touches `shop_orders_items`. Expected message shape:
`"<context> requires order #N to be in Completed status (all lines finished in CP)."` — the context
string differs per path (`Supplier settlement linked to order` vs `... linked to purchase order`).
PO→PI conversion sets `AllowOpenOrder`, so converting a PO linked to an *open* order must still pass.

## Local DB seeding for write paths
Tenant DB `ecomae` on 127.0.0.1 (user `ecomae`). Never point the app at production ecomae.com data.
Tables/settings that have been missing and needed seeding before writes succeed:
`epc_price_settings` (company_country_code, company_trn, company_vat_registered, vat_percent),
`epc_tax_toolkits` + `epc_tax_toolkit_tenant_profile` (e.g. `AE-UAE-VAT`, 5%, `delegate_uae_vat`),
supplier rows with `vat_registered`/`trn` (VAT is gated on supplier registration — an unregistered
supplier must yield `vat_amount 0.00`), `epc_einvoice_settings`, `epc_erp_audit_log`.
Corroborate every write: `epc_erp_purchase_orders`, `epc_erp_sales_orders`,
`epc_erp_voucher_sequences` (`voucher_type`,`year`,`last_seq` — note there is no `prefix` column),
`epc_erp_audit_log` (`entity_type='purchase_order'`, actions `po_save`/`po_status`/`delete`).
Clean up seeded documents between runs so voucher numbers start predictably.
Supplier payment additionally needs an active row in `epc_erp_cash_bank_accounts` (else
"Cash/bank account not found") and `epc_erp_suppliers.active=1` (else "Supplier not found").
`epc_erp_cash_bank_entries`, `epc_erp_supplier_accounting` and `epc_erp_purchases` are created lazily
by `EnsureSchemaAsync` on the first successful write — their *absence* is itself good evidence that a
rejected/dry-run request wrote nothing.

## Divergent `CREATE TABLE` definitions cause HTTP 500s (check this first)
Several ERP tables are created lazily by *more than one* module, with **different** column sets, so
whichever write path runs first on a fresh tenant DB decides the schema. Later paths that reference a
column the winner never created throw an unhandled `MySqlException` → **HTTP 500**, not a graceful
error envelope. Known example: `epc_erp_supplier_accounting` is created by
`Erp/ErpCashWriteService.cs` **with** an `active` column and by `Erp/ErpPurchaseInvoiceWriteService.cs`
**without** it; `Erp/ErpSettlementAllocationService.cs` (`PaidSubquery`, `LoadBillAsync`) filters on
`a.active = 1`, so if a PO→PI conversion created the table first, every AP allocation payment 500s with
`Unknown column 'a.active' in 'WHERE'`. Similar class of issue on reads: the `/erp/gl-journals-app`
digest selects `j.status` from `epc_erp_gl_journals`, which `Erp/ErpGlPostingService.cs` does not create.
When testing a new write path, `SHOW CREATE TABLE` the tables it touches and diff against every
`CREATE TABLE IF NOT EXISTS` for that table in the codebase (`grep -rn "CREATE TABLE IF NOT EXISTS \`epc_..."`).
Workaround to keep testing: `ALTER TABLE ... ADD COLUMN` the missing column locally, then re-run — but
report the divergence as a real bug rather than only working around it.
Note `OpenSupplierBillsAsync` swallows `DbException` and returns `[]`, so the same schema mismatch on
the **FIFO** AP path fails silently (allocates nothing) instead of 500ing — check allocation rows, not
just the HTTP status.

## Failed writes still consume voucher numbers
Voucher sequence allocation is not rolled back with the transaction: a request that fails mid-write
(e.g. the 500 above) leaves the cash entry rolled back but `epc_erp_voucher_sequences.last_seq`
incremented, so the next successful write is `PV-2026-00002`, not `00001`. Do not hard-code expected
voucher numbers after a failed attempt; read the sequence table.

## Useful read-only UI corroboration for payables writes
No UI controls exist for receiving / conversion / payment / settlement, so drive them over HTTP and
corroborate in these digests: `/erp/purchase-orders-app` (PO status draft→partial→received),
`/erp/purchases-app` (PI voucher no + total), `/erp/cash-entries-app` (PV outflow),
`/erp/suppliers-app` (supplier balance = sum(is_credit=1) − sum(is_credit=0)).
Note `/erp/aging-app` "AP outstanding" and the dashboard "Payables" KPI have previously stayed `0.00`
even with supplier AP rows present, so do not rely on them alone; they did populate correctly once
`epc_erp_purchases` rows with non-draft status existed.
For **AR** allocation/receipt work, `/erp/aging-app` "AR outstanding" and the dashboard A/R aging block
are the best UI proof: they sum `epc_einvoice_documents.amount_due`, so a bad `amount_due` (e.g. the
MySQL left-to-right `SET` bug where `amount_due = total - (paid_amount + ?)` is evaluated *after*
`paid_amount` was already raised, double-subtracting the payment) shows up as a wrong or negative
outstanding total. `/erp/invoices-app` does **not** expose `paid_amount`/`amount_due`, so it cannot
corroborate allocations. Always assert both `paid_amount` and `amount_due` per invoice in SQL, and
re-check after a *second* partial receipt on the same invoice to prove no drift.
`/erp/cash-entries-app` is the single best digest for cash writes: it shows RV/PV/TV vouchers with
direction in/out, and a transfer voucher appears as two rows sharing one `TV-` number.

## Transfer voucher notes
`TransferVoucherAsync` pre-validates *both* accounts with `AssertAccountAsync` before allocating the TV
number or inserting either leg, so an invalid target account cannot be used to force a second-leg
rollback — it only proves pre-validation (no orphan row, no voucher consumed). Report it as such.
The paired rows link mutually via `transfer_pair_id`, `counterparty_type='internal'`.
Watch the GL on transfers: the legs have posted `Dr 6100 Operating expense / Cr 1010 Bank` (out) and
`Dr 1000 Cash / Cr 4000 Revenue` (in), which inflates expense and revenue instead of being a pure
cash-to-cash reclass — verify the journal lines, not just that `gl_journal_id > 0`.

## Document lifecycle (void / amend / delete / cancel) testing
Routes live under `/erp/<entity>/<action>` (`/erp/cash-entries/void|amend`,
`/erp/purchases/void|delete|amend`, `/erp/invoices/cancel|delete`, `/erp/sales-orders/cancel`) — never
`/erp/ajax/*`; resolve them from `Routing/EcomAeRoutes.cs` and the body records at the bottom of
`Modules/ErpModule.cs` (camelCase: `entryId`, `purchaseId`, `invoiceId`, `salesOrderId`, `amountExVat`).
Useful adversarial trick: local DBs usually lack the lifecycle columns
(`voided_at`/`void_reason`/`voided_by`/`reversal_journal_id` on cash entries + purchases,
`reversed_by_journal_id` on `epc_erp_gl_journals`). Check `SHOW COLUMNS` **after the dry-run** call — if
they appeared, the dry-run wrongly ran the schema/write path; they should only appear after the first
`confirmWrites=true` call (lazy `EnsureSchemaAsync`).
Seeding fixtures: `epc_erp_supplier_accounting` uses `time`, not `time_created`;
`epc_erp_sales_orders` uses `so_no` (not `order_no`/`order_number`). Seed one document per terminal
state (draft / confirmed / posted / submitted / invoiced) so both the allowed and the refused branch of
each lifecycle rule can be exercised in one run.
**Guard bypass to watch for:** the Sales Orders digest row buttons (Confirm/Cancel/Delete) post to the
older `/erp/ajax/so-status` → `ErpSalesOrderWriteService.SetStatusAsync`, which only validates the
status *name* and has no lifecycle guard, so the UI Cancel button can flip an `invoiced` order to
`cancelled` even when `/erp/sales-orders/cancel` correctly refuses it. Whenever a lifecycle guard is
added to a dedicated route, check whether an older `/erp/ajax/*` route writes the same column, and test
the guard from the UI as well as the API.
Distinguish pre-existing damage from the current run: query
`SELECT source_id, COUNT(*) FROM epc_erp_gl_journals WHERE source_type='adjustment' AND active=1 GROUP BY source_id HAVING COUNT(*)>1`
before and after, and attribute duplicates only if they appear during your run (a DB used before the
double-reversal guard landed keeps its old duplicate pair).

## Testing MySQL named locks (`GET_LOCK`) around reversal / write paths
GL reversal (`Erp/ErpGlLedgerWriteService.ReverseJournalAsync`) may serialize its read-then-write
duplicate guard with a per-journal named lock (`erp_gl_reverse_<journalId>`, 10s timeout) and release it
in a `finally`. Because ERP write connections come from `ErpWriteConnectionFactory` →
`ITenantDbConnectionFactory` (**pooled** `MySqlConnection`), a missed `RELEASE_LOCK` leaks into later
requests, so verify release explicitly rather than assuming:
- After each reversal/void, from a *separate* CLI session:
  `SELECT COALESCE(IS_USED_LOCK('erp_gl_reverse_<id>'),0);` → must be `0`. Non-zero = leaked lock.
- Firing two parallel `curl` requests rarely truly overlaps (each finishes in ~10-30 ms), so it proves
  only "one reversal exists", not that the lock was consulted. To force contention deterministically,
  hold the lock from an interactive `mysql` session first:
  `SELECT GET_LOCK('erp_gl_reverse_<id>', 0);` then POST the reverse/void. Expect the request to take
  ~10.0 s and return HTTP 200 with `status:false, writes:0` and the contention message (e.g.
  `Journal is being reversed by another request — retry`), then `DO RELEASE_LOCK(...)` and confirm the
  same journal reverses normally afterwards.
- Timing is the regression signal: `curl -w "%{time_total}"` every lifecycle call and grep the app log
  for `Request finished … <ms>` ≥ 9000 ms. The *only* acceptable ~10 s request is a deliberately
  contended one; any other means a lock was left held on a pooled connection.
- A transfer-pair void takes the lock twice in one request (one per leg journal). If it ever returns the
  contention message or takes ~10 s, the lock is being re-entered/held incorrectly.
Minor pre-existing quirk to expect: on a transfer-pair void both cash rows may get the *same*
`reversal_journal_id` (the first reversal id) even though each leg journal gets its own reversal — check
`epc_erp_gl_journals.reversed_by_journal_id` per leg for the accurate mapping.

## Browser vs shell auth
Shell cookie jars (`curl -c/-b /tmp/cjNN.txt`) are **not** shared with Chrome, so the browser needs its
own login at `/erp/login` before any digest corroboration; a fresh profile just redirects to
`/erp/login?returnUrl=…`. Log in via the UI once (fields `contact` / `contact_type=email` / `password`,
type the password with `xdotool type --` so it never appears in output) and keep that tab for all
digest checks.

## Devin Secrets Needed
- `ECOMAE_OPERATOR_PASSWORD` — operator login password (type via `xdotool type -- "$VAR"`, never print it).
