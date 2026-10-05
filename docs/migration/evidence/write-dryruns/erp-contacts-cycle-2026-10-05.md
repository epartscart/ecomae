# ERP contacts sync cycle rehearsal — 2026-10-05 (throwaway tenant MariaDB)

| Step | Route | Result |
|---|---|---|
| supplier | POST /erp/suppliers/create | ok — `epc_erp_suppliers` 17 "CYC Sync Supplier" |
| sync | POST /erp/ajax/sync-contacts | ok — `Synced 1 contact(s)`, `created:1` → `epc_erp_contacts` 27 party_type `supplier` |
| sync idempotent | same | ok — `Synced 0 contact(s)` (already-linked supplier skipped, as PHP) |
| manual contact | POST /erp/ajax/save-contact (customer) | ok — `Contact saved` → `epc_erp_contacts` 28 party_type `customer`, email persisted |

Fixture note: no `shop_orders` rows with unlinked users existed, so only the supplier leg of PHP `epc_erp_sync_contacts` produced a row — matching the PHP LIMIT-500 skip-if-linked rule.

Cleanup: contacts 27/28 and supplier 17 deleted — residue 0.
