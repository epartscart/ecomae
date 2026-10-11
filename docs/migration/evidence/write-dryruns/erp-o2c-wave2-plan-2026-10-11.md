# Wave 2 — Order-to-Cash inspection and build plan (2026-10-11)

Reconciled after the six-finding close-out (#2074–#2076 merged; finding
resolutions recorded in `ERP_OWNERSHIP_AUDIT_2026-10-07.md`). Wave order per
`ERP_COMPLETION_DIRECTIVE.md`: Wave 1 shared foundation → **Wave 2 O2C** →
Wave 3 P2P → Wave 4 Inventory/WMS → Wave 5 Finance → Wave 6 rest (Jewellery,
Fit-Out). Board row: O2C Functional 85% / UI-UX 65% / Reports 40% / Writes 85%
/ Testing 45% — still **OPEN** (isolation, rollback, UAT, production evidence
blocked on owner items 1–6 in `ERP_STEP7_INTAKE_2026-10-11.md`).

## PHP reference surface (minimum spec)

- `content/shop/finance/epc_erp_vouchers.php` — the O2C document engine:
  `epc_erp_sales_order_parse_lines`, `epc_erp_sales_order_save`,
  `epc_erp_sales_order_set_status`, `epc_erp_sales_order_delete`,
  `epc_erp_so_convert_to_invoice`, `epc_erp_receipt_voucher`,
  `epc_erp_manual_sales_orders_list`, plus the shared voucher
  prefix/pad/type map.
- `content/shop/finance/epc_erp_invoices.php` + `epc_erp_einvoice.php` —
  sales invoice document, FTA e-invoice lifecycle, credit notes.
- `content/shop/finance/epc_erp_credit.php` / `epc_credit_limit.php` /
  `epc_erp_advances.php` — credit holds, limits, customer advances.
- `content/shop/finance/epc_erp_order_fulfillment.php` /
  `epc_order_erp_pipeline.php` — shop-order → ERP SO bootstrap and
  fulfilment states (ASP.NET service already live, #1985).

## Current ASP.NET coverage

- Writes live: `so-save`, `so-status`, `so-to-invoice` (atomic, Dr 1100 /
  Cr 4000 / Cr 2100, idempotent retry), `invoice-from-order` for legacy
  `shop_orders`, `customer-create`/`save_contact`/`sync_contacts`,
  `receipt-voucher` (invoice-scoped knock-off, non-advance),
  `sales-order/cancel` guarded lifecycle, `order_fulfillment` family (7
  routes), `rtl-pos-sale` (GL + stock in one transaction).
- Pages: `ErpSalesOrdersApp.razor` (`/erp/sales-orders-app`, 568 lines),
  `ErpInvoicesApp.razor` (`/erp/invoices` family, 323 lines),
  `ErpSalesQuotationApp` document workspace (persisted lines),
  `ErpEinvoiceApp` credit-note/submit/poll flows.
- Rehearsal evidence: consolidated O2C cycle (#1996), receipt denial +
  allocation cap (#1843), voucher browser parity (#1852), tenant isolation
  (#1883), recovery contract (#1929).

## Remaining O2C gaps to close in this wave (build order)

1. **SO document workspace to the UX bar** — upgrade `ErpSalesOrdersApp`
   from list+status to a full document workspace on `ErpDocumentWorkspace`:
   header fields, editable line grid (parse_lines parity: qty/price/discount/
   tax per line), totals panel, status actions (confirm/ship/invoice/cancel),
   delivery-note and invoice linkage, attachments/notes/audit tab, print.
2. **SI document workspace** — same shell treatment on `ErpInvoicesApp`:
   SO/delivery-note source links, settlement history, credit-note and
   cancellation controls, AR balance/collection state, e-invoice status.
3. **O2C reports** — AR aging, sales-by-customer/item, open-order backlog,
   invoice register; lift Reports band from ~40%.
4. **Receipts/advances workspace polish** — receipt voucher with invoice
   picker + open-balance readback (already guarded), advances list.
5. **Testing band** — extend the throwaway-DB cycles: SO edit→cancel→reopen,
   partial-invoice flows, advance VAT credit reuse, concurrent knock-offs.
6. **UX evidence per page** — PHP vs ASP.NET screenshots per the UX bar
   rules (no dropped PHP action/field/message; before/after in checkpoint).

Not blocked by owner items: all build/test work above. Still blocked:
physical second-tenant isolation, production rollback rehearsal, human
visual acceptance, which keep the process at 0/15 accepted regardless of
functional progress.

## Acceptance for the wave

O2C locks only when the SO and SI pages pass the field/line/action/
permission/DB/browser/visual bars, the four report surfaces exist, the
extended cycle evidence is recorded, and the owner-side gates land.
