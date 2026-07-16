# Power BI — what is possible on ECOM AE

Honest capability map for Microsoft Power BI against this multi-tenant platform.

## Available now (no Azure AD required)

| Capability | How |
|------------|-----|
| **Desktop / Service refresh** | Power BI **Web** connector → `/epc-api/v1/powerbi/*` |
| **Auth** | Tenant `X-API-Key` with scope `read:bi` (also accepts `read:erp` / `read:*`) |
| **Formats** | JSON (default) or `?format=csv` |
| **Isolation** | Each key is bound to one `site_key` → that tenant’s DB only |
| **Workspace config** | CP → Portal → **Power BI** stores workspace / report / dataset IDs |
| **URL embed** | Optional iframe when you paste a `https://*.powerbi.com` share / publish-to-web link |
| **Native ERP dashboards** | Already live in CP ERP (no Power BI needed) |
| **Metabase (parallel)** | JWT embed POC in `epc_metabase_embed.php` if you run Metabase |

### Datasets

| ID | Path | Contents |
|----|------|----------|
| catalog | `/epc-api/v1/powerbi/catalog` | Endpoint list + capabilities |
| kpis | `/epc-api/v1/powerbi/kpis` | Flat ERP KPI rows |
| orders | `/epc-api/v1/powerbi/orders` | Recent shop orders |
| sales | `/epc-api/v1/powerbi/sales` | Sales register (`from`/`to`) |
| stock | `/epc-api/v1/powerbi/stock` | Inventory on-hand |
| gl | `/epc-api/v1/powerbi/gl` | Trial balance |
| metrics | `/epc-api/v1/powerbi/metrics` | BI snapshot table (when computed) |

### Connect in Power BI Desktop

1. Issue a key (Super CP API documentation guide / `epc-api-keys-setup.php`).
2. **Get data → Web → Advanced**.
3. URL example: `https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv`
4. HTTP header: `X-API-Key` = your tenant key.
5. Publish to Power BI Service and schedule refresh with the same header.

```bash
curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXX" \
  "https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv"
```

## Needs your Microsoft credentials (not built-in)

| Capability | Why blocked |
|------------|-------------|
| Azure AD app registration | Customer’s Microsoft tenant |
| Secure embed tokens (Power BI Embedded) | Requires Pro / Premium / Embedded capacity + app secret |
| Admin REST (workspaces, clone reports) | Same Azure app + admin consent |
| Row-level security via Azure | Azure AD groups + Power BI RLS |

**What we already store for that day:** workspace ID, Azure tenant ID, report/dataset IDs, embed mode=`azure`. Token minting ships when you provide the app credentials.

## Deploy

```
https://www.ecomae.com/epc-power-bi-setup.php?token=…&apply not required
```

Registers CP route `/cp/control/portal/epc_power_bi` and creates `epc_power_bi_config` / `epc_power_bi_reports`.

## Files

| Role | Path |
|------|------|
| Library | `content/general_pages/epc_power_bi.php` |
| API routes | `content/general_pages/epc_api_v1.php` (`powerbi/*`) |
| CP UI | `cp/content/control/portal/epc_power_bi.php` |
| Setup | `epc-power-bi-setup.php` |
| Tests | `tests/erp_advanced/run_power_bi_tests.php` |

## Out of scope for this phase

- Shipping Microsoft client secrets in our repo
- Replacing native ERP dashboards
- Using `tech_key` / deploy tokens as Power BI credentials
