# ERP O2C extended cycle rehearsal (2026-10-11)

Extended Order-to-Cash lifecycle rehearsal on the local ASP.NET ERP
(`http://www.ecomae.com:5080`, tenant `ecomae`), all writes via JSON posts with
`confirmWrites: true` + `X-Csrf-Guard-Key` on a live admin session.

## Cycle executed

| Step | Route | Result |
| --- | --- | --- |
| Create draft SO | `POST /erp/ajax/so-save` | id 35, `SO-2026-00019`, ex-VAT 100, VAT 5, total 105, status `draft` |
| Confirm SO | `POST /erp/ajax/so-status` | status → `confirmed` |
| Convert to tax invoice | `POST /erp/ajax/so-to-invoice` | `SI-2026-00015` (doc 49), journal `GV-2026-00153`, ledger 41 |
| Partial receipt (40/105) | `POST /erp/cash-entries/receipt-voucher` `salesInvoiceId:49` | `RV-2026-00009`, allocated 40, paid_amount 40, amount_due 65 |
| Over-payment receipt (100 vs 65 due) | same | `RV-2026-00010`, allocated 65, unallocated 35 — capped at outstanding, not double-booked |
| Invoice settled | SQL | `paid_amount=105.00`, `amount_due=0.00`, status `validated` |
| Cancel invoiced SO | `POST /erp/sales-orders/cancel` | refused: `status:false`, "Sales order cannot be cancelled", `writes:0` |

## Corroborating rows observed

- `epc_erp_gl_journals`: `GV-2026-00153` source_type `sales_invoice` ref `SI-2026-00015`; `GV-2026-00154`/`00155` source_type `cash` ref `RV-2026-00009/10`.
- `epc_einvoice_documents` 49: `paid_amount` 40.00 → 105.00, `amount_due` 65.00 → 0.00.
- `epc_erp_cash_bank_entries` 20/21: receipt rows bound to `sales_invoice_id=49`, `is_advance=0`.

## Notes

- Over-payment semantics: allocation capped at the invoice outstanding
  (65), residual 35 recorded as unallocated on the receipt row rather than
  over-paying the invoice — matches the allocation-cap contract.
- Zero residue: fixture rows 35/49, cash entries 20/21, journals
  GV-2026-00153..155 (and their gl lines, SO lines) deleted after rehearsal.
