# Surface Dashboard Summary Evidence

Read-only migration summaries for session-gated CP/ERP/BOS shells.

## Routes

- `GET /cp/dashboard-summary` — users, admin sessions, portal tenants
- `GET /erp/dashboard-summary` — epc_erp_* cash position, supplier credit/debit/net, cash accounts, suppliers, purchases
- `GET /bos/fleet-summary` — portal tenants + admin sessions

Requires valid admin cookies with backend-group access (`users_groups_bind` ∩ `groups.for_backend=1`).

## Staging smoke

```bash
curl -sS -b "admin_session=...; admin_u_id=..." \
  "http://127.0.0.1:5100/cp/dashboard-summary" | python3 -m json.tool
curl -sS -b "admin_session=...; admin_u_id=..." \
  "http://127.0.0.1:5100/erp/dashboard-summary" | python3 -m json.tool
curl -sS -b "admin_session=...; admin_u_id=..." \
  "http://127.0.0.1:5100/bos/fleet-summary" | python3 -m json.tool
```

PHP dashboards remain authoritative. No writes.


## Additional digests

- `GET /cp/tenants?limit=100` — portal tenant list
- `GET /cp/users?limit=100` — users digest
- `GET /cp/groups?limit=100` — groups digest
- `GET /cp/modules?limit=200` — modules digest
- `GET /cp/config-items?limit=200` — config_items metadata only (no secret values)
- `GET /cp/menus?limit=200` — menu metadata (structure JSON omitted)
- `GET /cp/pages?limit=200` — content pages metadata (body omitted)
- `GET /cp/admin-sessions?limit=200` — admin session counts by user (no raw tokens)
- `GET /cp/storages?limit=200` — shop_storages digest
- `GET /cp/currencies?limit=200` — shop_currencies digest
- `GET /cp/api-clients?limit=200` — epc_api_clients metadata (no key hashes)
- `GET /bos/tenants?limit=100` — same list for BOS
- `GET /bos/fleet-health?limit=25` — fleet summary + sample tenants
- `GET /bos/fleet-readiness` — platform-DB readiness scoring (no per-tenant connects)
- `GET /bos/audit-log?limit=100&area=` — epc_boc_audit recent entries (meta omitted)
- `GET /erp/accounts-summary` — epc_erp_* cash/supplier KPIs
- `GET /erp/suppliers?limit=200` — active suppliers + balances
- `GET /erp/purchases?limit=200` — recent purchases
- `GET /erp/cash-accounts?limit=200` — cash/bank accounts with balances
- `GET /erp/cash-entries?limit=200&account_id=` — cash/bank entries
- `GET /erp/invoices?limit=150` — e-invoice documents
- `GET /erp/gl-journals?limit=200` — GL journals with debit totals
- `GET /erp/coa-accounts?limit=300` — chart of accounts
- `GET /erp/warehouses?limit=200` — ERP inventory warehouses
- `GET /erp/sales-orders?limit=200` — ERP sales orders
- `GET /erp/purchase-orders?limit=200` — ERP purchase orders
- `GET /erp/inventory-stock` — ERP inventory stock KPI summary
- `GET /storefront/orders?limit=25` — customer-gated recent shop_orders
- `GET /storefront/garage?limit=50` — customer-gated garage vehicles
- `GET /storefront/profile` — customer-gated users + users_profiles

All require appropriate admin/customer sessions. PHP remains authoritative.
