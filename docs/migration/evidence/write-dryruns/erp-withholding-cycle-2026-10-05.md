# ERP withholding-tax cycle — live rehearsal evidence (2026-10-05)

Scope: `/erp/ajax/wht-code-save` → `wht-record` → `wht-settle` → `wht-certificate` on the throwaway tenant MariaDB (`ecomae`), with `epc_wht_code` and `epc_wht_txn` absent at start.

## Lazy-schema parity fix (same PR)

`epc_erp_withholding.php` `ensure_schema` creates `epc_wht_code` and `epc_wht_txn` at entry; the four ASP.NET writers carried `not provisioned` fail-closed guards. Added `ErpLazySchema.EnsureWithholdingAsync` (two verbatim DDL statements) wired after `OpenAsync` in `ErpWhtCodeSaveWriteService`, `ErpWhtRecordWriteService`, `ErpWhtCertificateWriteService`, and `ErpWhtSettleWriteService`.

## Steps (verified via HTTP + SQL)

1. `wht-code-save` `{code:"WHT5", name:"Professional services WHT", rate:5, account:"2300", confirmWrites:true}` → code row id:1.
2. `wht-record` `{codeId:1, vendor:"Supplier X", docRef:"BILL-CYC-1", txnDate:"2026-10-05", baseAmount:10000}` → accrued transaction id:1 with computed `wht_amount=500.00` (rate × base).
3. `wht-settle` `{id:1}` → `Withholding settled to authority`, transaction `status=settled`.
4. `wht-certificate` `{id:1, certificateNo:"CERT-CYC-1"}` → `Certificate issued: CERT-CYC-1`, transaction `certificate_no=CERT-CYC-1` (status remains `settled`, as PHP only stamps the certificate number).
5. Refusals verified verbatim from service validation: missing code/name → `Code and name are required`; missing transaction id → `A withholding transaction id is required.`

## SQL corroboration

```
epc_wht_code : id=1 code=WHT5 rate=5.0000 account=2300 active=1
epc_wht_txn  : id=1 code_id=1 vendor='Supplier X' doc_ref='BILL-CYC-1'
               base=10000.00 wht=500.00 certificate_no='CERT-CYC-1' status=settled
```

## Zero residue

`DROP TABLE epc_wht_code, epc_wht_txn` — `SHOW TABLES LIKE 'epc_wht%'` empty.

## Tests

`dotnet test tests/EcomAE.Platform.Tests --filter Erp` → 2254/2254 pass.
