# Legacy Session DB Validation Evidence

Tracked PHP gates: `cp/epc_cp_auth_gate.php` (admin) and storefront `DP_User::getUserId` (customer).

Admin:

```sql
SELECT COUNT(*) FROM `sessions`
WHERE `session` = ? AND `type` = 1 AND `user_id` = ?
```

Customer (no type filter):

```sql
SELECT COUNT(*) FROM `sessions`
WHERE `session` = ? AND `user_id` = ?
```

## ASP.NET implementation

- Validator: `DbBackedLegacySessionValidator`
- Store: `DbLegacySessionStore` (read-only)
- Admin cookies: `admin_session`, `admin_u_id`
- Customer cookies: `session`, `u_id`
- When TenantRegistry DB is configured: missing/invalid session → anonymous
- When DB is not configured: cookie-presence bridge remains (migration/diagnostics)
- Probe: `/auth/session/probe`
- Parity: `/auth/session/parity`

## Staging smoke

1. Log into CP / storefront in a browser to obtain valid cookies.
2. Call ASP.NET probe with those cookies against `127.0.0.1:5100`.
3. Confirm invalid/stale cookies return anonymous.

Keep PHP CP/ERP/BOS/storefront authoritative until staging smoke and role mapping are complete.
