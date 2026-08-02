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
- `GET /bos/tenants?limit=100` — same list for BOS
- `GET /bos/fleet-health?limit=25` — fleet summary + sample tenants
- `GET /erp/accounts-summary` — epc_erp_* cash/supplier KPIs
- `GET /erp/suppliers?limit=200` — active suppliers + balances
- `GET /erp/purchases?limit=200` — recent purchases
- `GET /storefront/orders?limit=25` — customer-gated recent shop_orders
- `GET /storefront/garage?limit=50` — customer-gated garage vehicles

All require appropriate admin/customer sessions. PHP remains authoritative.
