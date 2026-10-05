# ERP contracts cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| save | POST /erp/contracts/save | ok — `Contract saved` → `epc_erp_contracts` 1 (CYC-1, draft, v1) |
| status | POST /erp/ajax/ctr-status `{targetStatus:"sent"}` | ok — `Contract sent` |
| sign | POST /erp/contracts/sign `{contract_id, signer_name, signer_email}` | ok — `Signed — eceea68c…` → signature row (64-hex hash) + status `signed` |
| edit | POST /erp/contracts/save `{id:1, code:"CYC-1", …}` | ok — version 1→2, value 10000→12000 |
| ocr | POST /erp/contracts/ocr | ok — `OCR text saved` |

Verbatim refusals:
- `targetStatus:"bogus"` → `Invalid contract status` (allowed: draft/sent/signed/active/expired/terminated).
- re-save same `code` → `Contract code already exists.`
- update without `code` → `Code and title are required` (PHP also requires code on update).

Fix found by this rehearsal: `epc_erp_contracts`/`epc_erp_contract_signatures` are lazy-created by PHP's `epc_ctr_ensure_schema`; the ASP.NET writers had no ensure, so a tenant without the tables got HTTP 500. Added `ErpContractSchema.EnsureAsync` (verbatim PHP DDL) to save/status/sign/ocr — live run above is the proof.

Cleanup: contract + signature rows deleted — residue 0.
