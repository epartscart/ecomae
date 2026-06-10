# ECOM AE — Shared ERP-only access (www.ecomae.com)

ERP-only companies **do not get their own domain**. Each company has an isolated MySQL database and CP users, but **Super CP and client ERP now use separate URL paths**.

## URL map (May 2026 separation)

| Role | Login URL | Work area |
|------|-----------|-----------|
| **Super CP operator** | https://www.ecomae.com/cp/ | Tenant hub: `/cp/shop/tenant_hub/tenant_hub` — **not** shared ERP shell |
| **ASAP ERP client** | https://www.ecomae.com/cp/client-erp/asap/ | `/cp/client-erp/asap/shop/finance/erp?epc_erp_shell=1` |
| **Tenant domain ERP** (eParts Cart, etc.) | `https://{tenant}/cp/` | `/cp/shop/finance/erp?epc_erp_shell=1` |

### Deprecated (do not hand off to clients)

- ~~`https://www.ecomae.com/cp/shop/finance/erp?epc_erp_shell=1`~~ — legacy shared shell; platform operators are redirected to tenant hub; tenant sessions redirect to `/cp/client-erp/{site_key}/…`

## Architecture

| Item | Value |
|------|--------|
| Platform URL | https://www.ecomae.com |
| Super CP login | https://www.ecomae.com/cp/ |
| Client ERP login (ASAP) | https://www.ecomae.com/cp/client-erp/asap/ |
| Client ERP shell (ASAP) | https://www.ecomae.com/cp/client-erp/asap/shop/finance/erp?epc_erp_shell=1 |
| Tenant routing | **Client-erp path + session + cookie** — not hostname |
| Registry flags | `hosted_on=platform`, `erp_only_shared=1` |

### Auth separation

1. **Platform operator** (`ecomae` DB, group 808) — login at `/cp/` → tenant hub. Never auto-redirect to ERP shell. Never use client-erp URLs for daily work.
2. **Client ERP staff** — login **only** at `/cp/client-erp/{site_key}/`. Blocked from standard `/cp/` if email exists only in tenant DB.
3. Cookie `epc_erp_tenant=<site_key>` is set **only** on client-erp paths.

### Data isolation guarantee

Each ERP-only company on www.ecomae.com **must have its own MySQL database** (e.g. `asap`, `company2`). The platform **never** serves ERP data from `docpart` or `ecomae` to tenant users.

## ASAP (first shared ERP company)

| Field | Value |
|-------|--------|
| Company | ASAP |
| Site key | `asap` |
| Database | `asap` (dedicated — **not** shared `docpart`) |
| Login URL | https://www.ecomae.com/cp/client-erp/asap/ |
| ERP shell | https://www.ecomae.com/cp/client-erp/asap/shop/finance/erp?epc_erp_shell=1 |
| Admin email | `asap_admin@asap-ae.com` |
| Demo email | `asap_demo@asap-ae.com` |

**Credentials:** Run onboard script on server — temp passwords are printed once:

```
https://www.ecomae.com/epc-asap-erp-onboard.php?token=epartscart-deploy-2026&clp_pass=...&apply=1
```

Remove stale platform DB copies (user_ids 19/20) after deploy:

```
https://www.ecomae.com/epc-purge-stale-asap-platform-users.php?token=epartscart-deploy-2026&apply=1
```

## Super CP onboard (no domain)

1. Super CP → **Tenant hub → Onboard client**
2. Tick **ERP only** and **Hosted on ecomae.com (shared — no client domain)**
3. Enter **Trade name** (e.g. ASAP) and **Site key** (e.g. `asap`)
4. Create MySQL DB + credentials; set **Full ERP** modules
5. Set status **Live** → sync packs to tenant DB
6. Create CP users in tenant context — hand off **`https://www.ecomae.com/cp/client-erp/{site_key}/`**

## Operator guides (Super CP)

| Guide | URL |
|-------|-----|
| ERP-only onboarding | https://www.ecomae.com/cp/control/portal/epc_erp_only_onboard_guide |
| ERP operator (client staff) | https://www.ecomae.com/cp/client-erp/asap/shop/finance/erp/erp-only-operator-guide?epc_erp_shell=1 |

## Login flow (test)

### Super CP operator

1. Open https://www.ecomae.com/cp/
2. Login `taxofin2025@gmail.com` → lands on tenant hub (not ASAP ERP)
3. Logout → returns to `/cp/` login

### ASAP client

1. Open https://www.ecomae.com/cp/client-erp/asap/
2. Login `asap_admin@asap-ae.com` → ERP shell with ASAP data only
3. Logout → returns to client-erp login; can re-login
