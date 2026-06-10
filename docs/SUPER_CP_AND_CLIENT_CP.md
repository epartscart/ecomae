# ECOM AE — Super CP & Client CP (Model C)

Professional workflow for platform operators and client tenants.

## Two control panels, one codebase

| | **Super CP** | **Client CP** |
|---|-------------|---------------|
| **URL** | https://www.ecomae.com/cp/ | https://www.client.com/cp/ or **shared ERP:** https://www.ecomae.com/cp/ |
| **Also** | https://cp.ecomae.com/cp/ | — |
| **Database** | Platform DB `ecomae` | Tenant DB (e.g. `docpart`) |
| **Who logs in** | You — platform operator / agency | Client staff — shop admin, warehouse, finance |
| **Purpose** | Onboard tenants, DNS, industry templates, deploy | Run day-to-day business on their domain |
| **Special modules** | Tenant hub, platform Industry settings + deploy | Industry packs only (no tenant hub, no deploy) |

Same PHP application and `/cp/` UI. **Hostname + DB routing** decide which world you are in.

```
                    ┌─────────────────────────────────────┐
                    │     ecomae nginx (one docroot)      │
                    │         31.97.216.247               │
                    └─────────────────────────────────────┘
                          │                    │
              www.ecomae.com/cp          www.epartscart.com/cp
                          │                    │
                    DB: ecomae              DB: docpart
                    Super CP                Client CP
                          │
              Tenant hub registers
              client hostname + DB creds
```

---

## Super CP — what you control

1. **Tenant hub** (`Platform → Tenant hub`)
   - Onboard client intro form (domain, industry, contacts, **Admin CP email**, tenant MySQL)
   - Tenant list, status (Draft → Awaiting DNS → Live)
   - Launch checklist (DNS, alias, SSL)
   - Go-live probes

2. **Industry settings** (Super CP only — full panel)
   - Platform branding
   - Enabled CP module packs (`super_platform` pack)
   - Deploy portal package to registered targets

3. **All standard CP modules** on platform host (for testing / demos)

4. **Users** (`Control → Users`) — **platform operator accounts only**
   - These users authenticate against the `ecomae` database
   - They must belong to a **backend administrator group**

---

## Client CP — what the client controls

1. **Their storefront** at `https://www.client.com/`
2. **Their CP** at `https://www.client.com/cp/` — connects to **their tenant DB**
3. **Industry settings** (client view) — branding, contact, module packs for their vertical **only** (no deploy table, no Super CP pack)
4. **Users** — client admin, staff, B2B accounts (separate from Super CP operators)

You do **not** share Super CP credentials with clients. Each client gets their own CP user(s) on their domain.

---

## Login & credentials

### Super CP (platform operator)

| Field | Value |
|-------|--------|
| URL | https://www.ecomae.com/cp/ |
| Email | `taxofin2025@gmail.com` |
| Password | `12345678` |

After login, `/cp/` redirects to **Tenant hub → Onboard**.

**Security:** Every Super CP page requires an active admin session. Without login you see the CP sign-in form only — not tenant data or sidebar operator tools.

Setup / reset on server:

```
https://www.ecomae.com/ecomae-ensure-platform-admin.php?token=epartscart-deploy-2026&apply=1
```

### Client CP (per tenant)

Created **on the client domain** after the tenant is Live:

1. Super CP → Tenant hub → note **Admin CP email** from intro form
2. Open `https://www.client.com/cp/` → **Control → Users → Add user**
3. Assign backend groups (Administrator / shop roles)
4. Hand credentials to the client securely

**Managing client users from Super CP:** use Tenant hub to record the intended admin email and open the client CP in a new tab to create or reset users there. Client DB is isolated — Super CP does not auto-create client passwords (by design, for security).

Optional: CP **Users → key icon** (auth as user) on client CP when logged in as platform operator with a client CP account.

---

## End-to-end workflow

### Phase 1 — Platform (you)

1. Log in to **Super CP**
2. **Tenant hub → Onboard client** — fill trade name, `www.client.com`, industry, contacts, admin CP email, tenant DB credentials
3. Create tenant MySQL in CloudPanel if not exists; import seed (e.g. clone `docpart` for auto parts)
4. Client adds GoDaddy **A records** → `31.97.216.247`
5. Add domain alias on **www.ecomae.com** site (same docroot — do not create new CloudPanel site)
6. SSL on alias
7. Tenant hub → set status **Live**

### Phase 2 — Client handoff

1. Open `https://www.client.com/` — storefront with industry theme
2. Open `https://www.client.com/cp/` — create client admin user (email from intro form)
3. Client logs in and manages catalogue, orders, finance modules enabled for their industry pack
4. Client **Industry settings** — adjust branding; cannot see deploy or other tenants

### Phase 3 — Ongoing operations

| Task | Where |
|------|--------|
| Add new client | Super CP → Tenant hub |
| Client orders / stock | Client CP |
| Change client module packs | Super CP (tenant industry) or Client CP Industry settings |
| Platform marketing site | https://www.ecomae.com/ |
| Code deploy to platform | Super CP deploy tools / `push_one.py` |

---

## What clients must never see

- Tenant hub and other tenants
- Platform deploy targets (`epartscart`, `taxofinca` zip deploy)
- `super_platform` CP pack
- Platform DB credentials

These are stripped automatically when `epc_portal_is_client_hostname()` is true.

---

## URLs quick reference

| Resource | URL |
|----------|-----|
| Marketing home | https://www.ecomae.com/ |
| Industries | https://www.ecomae.com/platform/industries |
| Super CP login | https://www.ecomae.com/cp/ |
| Tenant hub | https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard |
| Failover & splash guide | https://www.ecomae.com/cp/control/portal/epc_platform_failover_guide |
| Splash preview | https://www.ecomae.com/epc-platform-splash.html?epc_splash_preview=1&mode=backup_active |
| CRM (platform or tenant) | `/cp/shop/crm/crm` — run `epc-crm-cp-setup.php` on **ecomae.com** (Super CP) or each **client.com** |
| Example client CP | https://www.epartscart.com/cp/ |
| Example client CRM | https://www.epartscart.com/cp/shop/crm/crm |
| Example client Industry settings | https://www.epartscart.com/cp/control/portal/industry_settings |
| Shared ERP-only (ASAP, etc.) | https://www.ecomae.com/cp/ — see `docs/ECOM-ERP-SHARED-ACCESS.md` |
| ERP-only onboard guide | https://www.ecomae.com/cp/control/portal/epc_erp_only_onboard_guide |

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Tenant hub open without login | Deploy latest CP guard files; run `ecomae-ensure-platform-admin.php?apply=1` |
| Empty Super CP sidebar | Run `ecomae-super-cp-setup.php?token=...` |
| Client sees deploy / Super CP options | Deploy client vs platform Industry settings split |
| Cannot log in Super CP | Run ensure-platform-admin; verify user in backend groups |
