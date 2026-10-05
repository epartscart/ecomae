# ERP delivery-note cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| order id guard | POST /erp/ajax/delivery-note-create `{}` | verbatim refusal `Order ID is required.` (writes 0) |
| fixture | `shop_orders` 712 | seeded minimal legacy order row |
| create + ship | POST /erp/ajax/delivery-note-create (carrier CYC, tracking CYC-TRK-1, markShipped) | ok — `Delivery note DN-1 created.` → `epc_erp_delivery_notes` 1 `shipped`, pdf_path assigned |

Cleanup: delivery note + shop order deleted — residue 0.
