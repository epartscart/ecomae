# ERP RBAC (role → duty → privilege → user assignment) cycle — live rehearsal evidence (2026-10-05)

Scope: `/erp/ajax/rbac-role-save` → `rbac-duty-save` → `rbac-priv-save` → `rbac-duty-priv` → `rbac-role-duty` → `rbac-user-role` on the throwaway tenant MariaDB (`ecomae`), all six `epc_rbac_*` tables absent at start.

## Lazy-schema parity fix (same PR)

`epc_erp_rbac.php` `ensure_schema` creates `epc_rbac_role`, `epc_rbac_duty`, `epc_rbac_privilege`, `epc_rbac_role_duty`, `epc_rbac_duty_priv`, `epc_rbac_user_role` at entry; the six writers carried `not provisioned` fail-closed guards. Added `ErpLazySchema.EnsureRbacAsync` (six verbatim DDL statements) wired after `OpenAsync` in `ErpRbacRoleSaveWriteService`, `ErpRbacDutySaveWriteService`, `ErpRbacPrivSaveWriteService`, `ErpRbacRoleDutyWriteService`, `ErpRbacDutyPrivWriteService`, `ErpRbacUserRoleWriteService`.

## Steps (verified via HTTP + SQL)

1. `rbac-role-save` `{code:"CASHIER", name:"Cashier", confirmWrites:true}` → `{"ok":true,"id":1,"message":"Role saved"}` (tables lazily created on entry).
2. `rbac-duty-save` `{code:"PAY_ENTRY", name:"Enter payments"}` → id:1.
3. `rbac-priv-save` `{code:"PAY_POST", name:"Post payments", accessLevel:"write"}` → verbatim refusal `{"ok":false,"validation_code":"invalid","message":"Invalid access level"}`; with `accessLevel:"full"` → id:1 `access_level=full`.
4. `rbac-duty-priv` `{dutyId:1, privilegeId:1, attach:1}` → link row `1|1` (`Duty privileges updated`).
5. `rbac-role-duty` `{roleId:1, dutyId:1, attach:1}` → link row `1|1` (`Role duties updated`).
6. `rbac-user-role` `{userId:1, roleId:1, assign:1}` → link row `0|1|1` (`User role updated`).
7. Refusal parity: `rbac-role-save` with empty code → verbatim `Role code is required`.

## SQL corroboration

```
epc_rbac_role      : 1|0|CASHIER|Cashier
epc_rbac_duty      : 1|0|PAY_ENTRY|Enter payments
epc_rbac_privilege : 1|0|PAY_POST|Post payments|full
epc_rbac_duty_priv : duty_id=1 privilege_id=1
epc_rbac_role_duty : role_id=1 duty_id=1
epc_rbac_user_role : company_id=0 user_id=1 role_id=1
```

## Zero residue

`DROP TABLE` all six — `SHOW TABLES LIKE 'epc_rbac%'` empty.

## Tests

`dotnet test tests/EcomAE.Platform.Tests --filter Erp` → 2254/2254 pass.
