# Operator verify — same-to-same tenant chrome

Product chrome on tenant/platform hosts must remain PHP. Digests/Blazor previews on www are scaffolding only.

## Offline CI floor

```bash
bash scripts/cloudpanel_run_tenant_safety_operator.sh
```

Validates checked-in `live-tenant-php-chrome.json` + `same-to-same-verify.json`.
Expect `status=pass`, explicit `cutoverAllowed=false`, `readyForPhpRemoval=false`.

## Live CloudPanel verify

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
cd /opt/ecomae-aspnet-source
ECOMAE_TENANT_LIVE=1 bash scripts/cloudpanel_run_tenant_safety_operator.sh
# or directly:
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh
```

**Fail means:** a tenant (or platform control host) is serving Blazor scaffold / digest JSON / cutover headers on product chrome paths. Roll back non-www shadows; keep PHP-FPM and PHP rewrites.

Never invent `RELEASE_OWNER_APPROVAL.md`. Batch 6 stays blocked.
