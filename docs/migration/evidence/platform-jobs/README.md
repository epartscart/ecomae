# Platform Jobs Diagnostic Evidence

Tracked PHP source: `epc-platform-jobs-cron.php` + `content/general_pages/epc_platform_jobs.php`.

ASP.NET exposes a **migration diagnostic** only (no claim/complete):

- Route: `GET /migration/platform-jobs?limit=50`
- Reporter: `PlatformJobsSummaryReporter` (read-only)
- Table: `epc_platform_jobs`
- Nginx: keep under allowlisted `/migration/` diagnostics include

## Staging smoke

```bash
curl -sS "http://127.0.0.1:5100/migration/platform-jobs?limit=50" | python3 -m json.tool
```

PHP cron remains authoritative for claiming and completing jobs.
