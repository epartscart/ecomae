# Tenant safety evidence

**Law:** same-to-same / invisible migration — tenants must not feel PHP→ASP.NET. Digests/previews never replace product chrome.

Operator-run probe output lands here:

```bash
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh
# -> live-tenant-php-chrome.json
# -> same-to-same-verify.json (status=pass|fail, cutoverAllowed=false)

bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh
# -> live-tenant-php-chrome.json only

# Industry showcase frontends (*.ecomae.com) — 28 hosts
bash scripts/run_industry_ecomae_frontend_parity.sh
ECOMAE_INDUSTRY_LIVE=1 bash scripts/run_industry_ecomae_frontend_parity.sh
# -> industry-ecomae-frontend-parity.json
# -> industry-ecomae-coverage-matrix.json
```

## Industry `*.ecomae.com`

- No `industry.ecomae.com` — use `[slug].ecomae.com` (trading → `wholesale`).
- Live chrome stays **PHP** (same look source) until www `/marketing/industries` dual-samples same-to-same.
- Do not install Blazor shadows on industry vhosts.
- ASP.NET catalog: `EcomaeIndustryShowcaseHosts` + `/marketing/industries` preview on www only.

Do not invent a pass file. Commit only real CloudPanel/probe results when attaching evidence. Never invent `RELEASE_OWNER_APPROVAL.md`.
