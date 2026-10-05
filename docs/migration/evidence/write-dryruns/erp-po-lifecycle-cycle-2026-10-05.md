# ERP purchase-order lifecycle cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

Same authenticated session, live routes only.

| Step | Route | Result |
|---|---|---|
| supplier | POST /erp/suppliers/create | ok — `epc_erp_suppliers` 16 |
| PO | POST /erp/ajax/po-save (4×50 ex-VAT line) | ok — PO-2026-00009 id 23 draft, amount 200, line 9 |
| approve | POST /erp/ajax/po-status `approved` | ok — draft→approved |
| approve guard | re-approve | verbatim refusal `Only draft purchase orders can be approved` |
| partial receive | POST /erp/ajax/po-receive-lines `{"9":2}` | ok — qty_received 2, status `partial` |
| full receive | `{"9":4}` | ok — qty_received 4, status `received` |
| convert | POST /erp/ajax/po-to-invoice | ok — purchase 12 `PI-2026-00003`, total 200, `confirmed`, GL journal `Purchase invoice #12` |

`receivedJson` contract is a `{lineId: cumulativeQty}` map (array form parses to empty map — recorded as the correct PHP contract).

Cleanup: GL lines+journals (incl. the receipt-voucher journal from the treasury cycle), receipts, lines, PO, purchase, supplier deleted — residue 0.
