# ERP cash-treasury (forecast + bank instrument) cycle — live rehearsal evidence (2026-10-05)

Scope: `/erp/ajax/cft-forecast-save` → `cft-line-add`, `cft-instrument-save` → `cft-instrument-status` on the throwaway tenant MariaDB (`ecomae`), all four `epc_cft_*` tables absent at start.

## Lazy-schema parity fix (same PR)

`epc_erp_cash_treasury.php` `ensure_schema` creates `epc_cft_forecast`, `epc_cft_line`, `epc_cft_instrument`, `epc_cft_instr_event` at entry; the four writers carried `not provisioned` fail-closed guards. Added `ErpLazySchema.EnsureCashTreasuryAsync` (four verbatim DDL statements) wired after `OpenAsync` in `ErpCftForecastSaveWriteService`, `ErpCftLineAddWriteService`, `ErpCftInstrumentSaveWriteService`, `ErpCftInstrumentStatusWriteService`.

## Steps (verified via HTTP + SQL)

1. `cft-forecast-save` `{name:"Q4 cash plan", openingBalance:50000, currency:"AED", confirmWrites:true}` → `{"ok":true,"id":1,"message":"Forecast saved"}` (tables lazily created on entry).
2. `cft-line-add` `{forecastId:1, dueDate:"2026-10-15", direction:"in", amount:12500, category:"receivable"}` → `{"ok":true,"id":1}`.
3. `cft-instrument-save` `{ref:"LC-CYC-1", type:"lc", beneficiary:"Supplier X", applicant:"ACME", bank:"ENBD", amount:80000, currency:"AED"}` → `{"ok":true,"id":1}` + `epc_cft_instr_event` row `created`/`Instrument drafted`.
4. `cft-instrument-status` `{id:1, targetStatus:"issued", detail:"issued by ENBD"}` → instrument `status=issued` + event row `issued`/`issued by ENBD`.
5. Refusal parity: `cft-instrument-status` `{targetStatus:"bogus"}` → `{"ok":false,"validation_code":"invalid","message":"Cannot move instrument from issued to bogus"}`.

## SQL corroboration

```
epc_cft_forecast   : id=1 'Q4 cash plan' opening_balance=50000.00 AED
epc_cft_line       : id=1 forecast_id=1 dir=in amount=12500.00 receivable
epc_cft_instrument : id=1 ref=LC-CYC-1 lc 'Supplier X' amount=80000.00 status=issued
epc_cft_instr_event: 2 rows — created/'Instrument drafted', issued/'issued by ENBD'
```

## Zero residue

`DROP TABLE epc_cft_forecast, epc_cft_line, epc_cft_instrument, epc_cft_instr_event` — empty.

## Tests

`dotnet test tests/EcomAE.Platform.Tests --filter Erp` → 2254/2254 pass.
