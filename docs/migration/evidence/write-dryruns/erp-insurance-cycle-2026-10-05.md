# ERP insurance lifecycle cycle — live rehearsal evidence (2026-10-05)

Scope: `/erp/ajax/ins-save` → `ins-claim-add` → `ins-claim-status` → `ins-doc-add` → `ins-doc-delete` on the throwaway tenant MariaDB (`ecomae`), all three `epc_erp_ins_*` tables absent at start.

## Lazy-schema parity fix (same PR)

`epc_erp_insurance.php`'s `ensure_schema` runs `CREATE TABLE IF NOT EXISTS` for `epc_erp_ins_policies`, `epc_erp_ins_claims`, `epc_erp_ins_documents` at entry. The three remaining writers that had hard `… table is not provisioned` refusals — `ErpInsSaveWriteService`, `ErpInsDocAddWriteService`, `ErpInsDeleteWriteService` — diverged from PHP: PHP creates and proceeds, ASP.NET refused. Wired `ErpLazySchema.EnsureInsuranceAsync` after `OpenAsync` in all three; the `ColumnExists` guards stay as a belt-and-braces no-op.

## Steps (all verified via HTTP + SQL)

1. Pre-state: `SHOW TABLES LIKE 'epc_erp_ins%'` → only `_claims`, `_documents` (dropped to force absent state).
2. `POST /erp/ajax/ins-save` `{policyNo:"POL-CYC-1", title:"Marine cargo", insurer:"ADNIC", insuredName:"ACME", sumInsured:100000, premium:2500, currency:"AED", startDate:"2026-01-01", expiryDate:"2026-12-31", confirmWrites:true}` → `{"ok":true,"writes":1,"id":1,"message":"Policy saved"}` — lazily created `epc_erp_ins_policies` on entry (previously refused "Insurance policy table is not provisioned").
3. `POST /erp/ajax/ins-claim-add` `{policyId:1, claimNo:"CLM-CYC-1", lossDate:"2026-03-01", claimAmount:12000, status:"notified", confirmWrites:true}` → `{"ok":true,"writes":1,"id":1,"message":"Claim logged"}`.
4. `POST /erp/ajax/ins-claim-status` `{id:1, targetStatus:"assessed"}` → `{"ok":true,"message":"Claim status updated"}`.
5. Refusal parity: `ins-claim-status` `{targetStatus:"bogus"}` → `{"ok":false,"validation_code":"invalid","message":"Invalid claim status"}` (allow-list `notified,survey,docs,assessed,settled,rejected,closed`); `ins-doc-add` `{policyId:0}` → `{"ok":false,"message":"Select a policy"}`.
6. `POST /erp/ajax/ins-doc-add` `{policyId:1, docType:"claim_form", title:"survey report", filePath:"/docs/survey.pdf", confirmWrites:true}` → `{"ok":true,"id":1,"message":"Document added"}` (documents table lazily created here too).
7. `POST /erp/ajax/ins-doc-delete` `{id:1, confirmWrites:true}` → `{"ok":true,"message":"Document removed"}`.

## SQL corroboration

```
epc_erp_ins_policies : id=1 policy_no=POL-CYC-1 title='Marine cargo' insurer=ADNIC status=active
epc_erp_ins_claims   : id=1 policy_id=1 claim_no=CLM-CYC-1 claim_amount=12000.00 status=assessed
epc_erp_ins_documents: (0 rows — added then removed)
```

## Zero residue

`DROP TABLE epc_erp_ins_policies, epc_erp_ins_claims, epc_erp_ins_documents` — pre-rehearsal absent state restored; `SHOW TABLES LIKE 'epc_erp_ins%'` empty.

## Tests

`dotnet test tests/EcomAE.Platform.Tests --filter Erp` → 2254/2254 pass.
