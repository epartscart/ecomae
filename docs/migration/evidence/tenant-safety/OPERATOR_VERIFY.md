# Operator verify — same-to-same tenant chrome

Run after every redeploy / presentation-shadow install:

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
cd /opt/ecomae-aspnet-source
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh
```

**Expect:** `status=pass`, `cutoverAllowed=false`.

**Fail means:** a tenant (or platform control host) is serving Blazor scaffold / digest JSON / cutover headers on product chrome paths. Roll back non-www shadows; keep PHP-FPM and PHP rewrites.

Blazor previews on www (`/cp/app`, `/erp/app`, …) are migration scaffolding only — not live tenant UX.