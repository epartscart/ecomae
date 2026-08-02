# UMAPI Usage Diagnostic Evidence

Tracked PHP source: `api/umapi_proxy.php` `epc_umapi_usage_summary` / `usage_report`.

External catalog clients are forbidden from calling PHP `usage_report`. ASP.NET exposes a **migration diagnostic** only:

- Route: `GET /migration/umapi-usage?days=7`
- Reporter: `UmapiUsageSummaryReporter` (read-only)
- Table: `epc_umapi_usage_log`
- Daily limit: `Umapi:DailyLimit` config (default 1000)
- Nginx: keep under allowlisted `/migration/` diagnostics include

## Staging smoke

```bash
curl -sS "http://127.0.0.1:5100/migration/umapi-usage?days=7" | python3 -m json.tool
```
