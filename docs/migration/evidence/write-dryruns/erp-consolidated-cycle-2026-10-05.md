# ERP consolidated business-cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

One authenticated session drove a full order-to-cash + procure-to-pay cycle through the live ajax routes only (no direct SQL writes except fixture provisioning and cleanup).

## Cycle and SQL corroboration

| Step | Route | Result |
|---|---|---|
| customer-create | POST /erp/ajax/customer-create | ok — `users` 2 + `epc_erp_contacts` 25 (party_type=customer) |
| supplier create | POST /erp/suppliers/create | ok — `epc_erp_suppliers` 15 |
| sales order | POST /erp/ajax/so-save | ok — `epc_erp_sales_orders` 34 (2×500 = 1000 ex-VAT) |
| so→invoice | POST /erp/ajax/so-to-invoice | ok — `epc_einvoice_documents` 46 `SI-2026-00014` 1000+50 VAT = 1050 validated; GL journal 133 `sales_invoice` Dr 1050 / Cr 1050 (1100/4000/2100) |
| receipt | POST /erp/cash-entries/receipt-voucher (native form, `sales_invoice_id=46`) | 302 `?ok=Receipt voucher posted` — cash entry 14 (1050, gl_journal_id 134), invoice `paid_amount=1050`, cash journal 134 Dr 1050 / Cr 1050 |
| purchase | POST /erp/purchases/create | ok — `epc_erp_purchases` 11 (400, supplier 15, confirmed) + GL journal 132 `purchase` Dr 400 / Cr 400 |
| supplier payment | POST /erp/ajax/supplier-payment | ok — `PV-2026-00002`; purchase 11 → `paid`; cash journal 135 Dr 400 / Cr 400 |

## Guards exercised during the cycle

- `supplier-payment` amount 420 vs bill open balance 400 → verbatim refusal `Payment 420.00 exceeds open balance 400.00 of supplier bill 11`; retry at 400 succeeded.
- so-save with `{"item":"…","price":500}` line keys → verbatim `Customer, title, and amount (or lines) are required` (PHP key names `description`/`unit_price_ex_vat` required); corrected payload saved.

## Fixture note

The local `users` fixture lacked `reg_variant`/`time_registered`/`admin_created` and `AUTO_INCREMENT` on `user_id` (PHP creates these at install, not lazily — `epc_erp_phase8.php:448` uses the same columns). Provisioned additively on the throwaway DB only; no code change needed.

## Cleanup

All 10 cycle artifacts deleted (invoice, events, receipt, payment, purchase, SO, supplier, contact, user, GL journals/lines 132–135, settlement allocation): residue = 0.
