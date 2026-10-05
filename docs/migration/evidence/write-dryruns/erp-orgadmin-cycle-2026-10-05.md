# ERP office-administration (party book + calendar) cycle — live rehearsal evidence (2026-10-05)

Scope: `/erp/ajax/oa-party-save` → `oa-contact-save` → `oa-address-save` → `oa-calendar-save` → `oa-holiday-add` on the throwaway tenant MariaDB (`ecomae`), all five `epc_oa_*` tables absent at start.

## Lazy-schema parity fix (same PR)

`epc_erp_orgadmin.php` `ensure_schema` runs `CREATE TABLE IF NOT EXISTS` for `epc_oa_party`, `epc_oa_address`, `epc_oa_contact`, `epc_oa_calendar`, `epc_oa_holiday` at entry. The five writers carried hard `… is not provisioned` refusals — PHP creates the table and proceeds. Added `ErpLazySchema.EnsureOrgAdminAsync` (five verbatim DDL statements) wired after `OpenAsync` in `ErpOaPartySaveWriteService`, `ErpOaAddressSaveWriteService`, `ErpOaContactSaveWriteService`, `ErpOaCalendarSaveWriteService`, `ErpOaHolidayAddWriteService`.

This tranche came out of a second sweep that explicitly looked for `not provisioned` fail-closed guards (the earlier sweep missed services that refuse instead of crash). ~65 further guarded services remain for follow-up tranches.

## Steps (verified via HTTP + SQL)

1. Pre-state: `epc_oa_*` absent (`DROP TABLE IF EXISTS` issued first).
2. `oa-party-save` `{name:"Cycle Party LLC", partyType:"organization", confirmWrites:true}` → `{"ok":true,"id":1,"message":"Party saved"}`.
3. `oa-contact-save` `{partyId:1, contactType:"email", value:"ops@cycle.example", isPrimary:1}` → `{"ok":true,"id":1}`.
4. `oa-address-save` `{partyId:1, purpose:"business", line1:"12 Bay St", city:"Dubai", country:"AE", isPrimary:1}` → `{"ok":true,"id":1}`.
5. `oa-calendar-save` `{code:"WK6", name:"6-day week", workingDays:"0,1,2,3,4,5"}` → `{"ok":true,"id":1}`.
6. `oa-holiday-add` `{calendarId:1, holidayDate:"2026-12-02", name:"National Day"}` → `{"ok":true,"id":1}`.
7. Refusal parity: `oa-party-save` `{name:""}` → `{"ok":false,"validation_code":"invalid","message":"Party name is required"}`.
8. Parity note: `oa-holiday-add` with `calendarId:999` is ACCEPTED — PHP `epc_oa_holiday_add` performs no FK check (bare INSERT … ON DUPLICATE KEY UPDATE); ASP.NET matches.

## SQL corroboration

```
epc_oa_party   : id=1 party_type=organization name='Cycle Party LLC'
epc_oa_contact : id=1 party_id=1 type=email value=ops@cycle.example
epc_oa_address : id=1 party_id=1 purpose=business city=Dubai country=AE
epc_oa_calendar: id=1 code=WK6 name='6-day week' working_days=0,1,2,3,4,5
epc_oa_holiday : id=1 calendar_id=1 2026-12-02 'National Day' (+ id=2 parity row for cal 999, dropped)
```

## Zero residue

`DROP TABLE epc_oa_party, epc_oa_address, epc_oa_contact, epc_oa_calendar, epc_oa_holiday` — `SHOW TABLES LIKE 'epc_oa%'` empty.

## Tests

`dotnet test tests/EcomAE.Platform.Tests --filter Erp` → 2254/2254 pass.
