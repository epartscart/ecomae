# ERP e-invoice credit-note cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

Same authenticated session, live routes only.

| Step | Route | Result |
|---|---|---|
| invoice | SQL seed `epc_einvoice_documents` CYC-SI-1 (1000+50 VAT=1050, type 388, validated) + 1 line | doc 47 |
| no-lines guard | POST /erp/ajax/einvoice-credit-note before line seed | verbatim refusal `Original invoice has no lines.` |
| credit note | POST /erp/ajax/einvoice-credit-note `originalDocumentId=47` | ok — `CN-CYC-SI-1-001` doc 48, `doc_category=tax_credit_note`, type 381, mirrored totals 1000/50/1050 + mirrored line |
| cumulative cap | same POST again | verbatim refusal `Credit notes for CYC-SI-1 would exceed the original invoice total (1050.00 of 1050.00 already credited).` |

Cleanup: lines, events, both documents deleted — residue 0.
