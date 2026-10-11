# O2C wave-2 close-out — 2026-10-11

Scope: `erp-o2c-wave2-plan-2026-10-11.md` items 1–6, all landed on `main`.

| Item | Evidence |
|---|---|
| 1. SO document workspace (header, line grid, totals, status actions, DN/invoice links, attachments/notes/audit, print) | #2079 (VAT + invoice link on list rows), #2088 (audit trail on detail), #2091 (attachments), #2092 (line grid), #2093 (printable document via `ErpO2cDocumentPrintService`); lifecycle actions (confirm/cancel with reason, guarded by ERP write endpoints) already on the page |
| 2. SI document workspace (source links, settlement history, credit-note/cancel, AR state, e-invoice status) | #2080 (Paid/Due AR columns), #2086 (settlement history), #2091 (attachments), #2092 (PINT-AE line grid), #2093 (printable tax invoice); credit-note draft + cancellation + e-invoice status already on the page |
| 3. O2C reports | #2081 (AR aging buckets 0–30/31–60/61–90/90+), #2082 (uninvoiced backlog KPIs + aged 30d+), #2083 (sales-by-customer), #2084 (sales-by-item); invoice register is the invoices list itself |
| 4. Receipts/advances polish | #2089 (advance flag, voucher, invoice link on cash entries list), #2094 (live invoice open-balance readback on the receipt-voucher form: `GET /erp/invoice-open-balance`, inline balance hint + "use open balance" fill) |
| 5. Extended throwaway-DB cycles | #2085 (O2C extended lifecycle: SO edit/cancel/reopen, partial invoice, advance-VAT reuse, concurrent knock-offs) |
| 6. Per-page UX screenshots | Captured locally against the throwaway tenant: sales-orders list (backlog + sales-by-customer/item KPIs), SO detail (header, lifecycle, line grid, totals), invoices list (AR aging), invoice detail (lines, settlements, siblings, print), cash-entries (live balance hint: `SI-FIXTURE-002 — open 210.00 AED (validated, paid 50.00) use open balance`), printable tax invoice (Print/Save-as-PDF). Fixture rows removed; residue count 0. |

## Bug found and fixed in-flight
`ErpDeliveryNoteDocumentService` (and the new print service) used named `@id` params with `ErpDb.AddParameters`, which binds positionally — the DN print route always 404'd. Fixed by switching to `@p0`-style parameters (#2093).

## Board after close
- O2C bands: Functional ~90% · UI-UX ~80% · Reports ~70% · Writes ~90% · Testing ~55% (all higher than the wave-2 start of 85/65/40/85/45).
- Weighted headline unchanged ~20.4% (0/15 processes accepted — acceptance still gated on owner items: production deploy evidence, second tenant DB, backup/restore window, human visual acceptance, `epc_erp_syncron_policy.php`, ERP-only staff role).
- Next wave per plan: P2P document workspaces.
