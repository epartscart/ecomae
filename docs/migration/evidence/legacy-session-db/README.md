# Legacy Admin Session DB Validation Evidence

Tracked PHP gate: `cp/epc_cp_auth_gate.php` / portal session checks.

```sql
SELECT COUNT(*) FROM `sessions`
WHERE `session` = ? AND `type` = 1 AND `user_id` = ?
```

## ASP.NET implementation

- Validator: `DbBackedLegacySessionValidator`
- Store: `DbLegacySessionStore` (read-only)
- Cookies: `admin_session`, `admin_u_id`
- When TenantRegistry DB is configured: missing/invalid admin session → anonymous
- When DB is not configured: cookie-presence bridge remains (migration/diagnostics)
- Probe: `/auth/session/probe`
- Parity: `/auth/session/parity`

## Staging smoke

1. Log into CP in a browser to obtain valid admin cookies.
2. Call ASP.NET probe with those cookies against `127.0.0.1:5100`.
3. Confirm invalid/stale cookies return anonymous.

Keep PHP CP/ERP/BOS authoritative until staging smoke and role mapping are complete.
