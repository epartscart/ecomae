# Tenant safety evidence

**Law:** same-to-same / invisible migration — tenants must not feel PHP→ASP.NET. Digests/previews never replace product chrome.

Operator-run probe output lands here:

```bash
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh
# -> live-tenant-php-chrome.json
# -> same-to-same-verify.json (status=pass|fail, cutoverAllowed=false)

bash scripts/cloudpanel_probe_live_tenant_php_chrome.sh
# -> live-tenant-php-chrome.json only
```

Do not invent a pass file. Commit only real CloudPanel/probe results when attaching evidence. Never invent `RELEASE_OWNER_APPROVAL.md`.
