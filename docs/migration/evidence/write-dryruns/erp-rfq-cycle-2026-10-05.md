# ERP RFQ response→award cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| supplier | POST /erp/suppliers/create | ok — `epc_erp_suppliers` 18 |
| RFQ header | POST /erp/ajax/save-rfq | ok — `epc_erp_rfq` 3 `RFQ-1` draft, `RFQ saved.` (header-only; SCM lines live in `epc_scm_rfq_lines`) |
| fixture | seeded `epc_scm_rfq` 2 `sent` + `epc_scm_rfq_lines` 2 (steel 10) | SCM RFQ pipeline uses the `epc_scm_*` tables |
| response | POST /erp/ajax/save-rfq-response (45, 7d) | ok — `Supplier response saved.` → `epc_scm_rfq_responses` 4 |
| response guard | wrong line id | verbatim refusal `RFQ line was not found.` |
| award | POST /erp/ajax/award-rfq | ok — `RFQ awarded and draft purchase order created.`; rfq `awarded`, `awarded_supplier_id`=18, `awarded_po_id`=24; PO-2026-00010 draft 450.00 = 10×45 quoted price |
| re-award | same | verbatim refusal `Only an open RFQ can be awarded.` |

Cleanup: SCM rfq/line/responses, PO+lines, epc_erp_rfq, supplier deleted — residue 0.
