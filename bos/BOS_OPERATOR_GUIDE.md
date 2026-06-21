# BOS — Business Operating System
## Operator Guide v1.0

### What is BOS?

BOS is the unified control center for the ECOM AE platform. One login, one dashboard — manage all tenants, CP modules, ERP systems, and platform operations from a single place.

**Entry Point:** `https://www.ecomae.com/bos/`

---

### Login Credentials

| Role | Email | Access Level |
|------|-------|-------------|
| **Provider (Super Admin)** | ecomaedxb@gmail.com | All tenants, all modules, fleet command, tenant switcher |
| **Tenant Admin** | (tenant-specific email) | Their own tenant only — CP + ERP modules allocated to them |
| **ERP-Only Client** | (client-specific email) | Their own ERP only — no commerce modules |

---

### BOS Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  TOP BAR                                                         │
│  [=] BOS    [Tenant Switcher ▼]    [AE AED AR] [user] [logout]  │
├──────────┬───────────────────────────────────────────────────────┤
│ SIDEBAR  │ MAIN CONTENT                                          │
│          │                                                        │
│ Fleet    │ Dashboard / Module content                             │
│ Command  │                                                        │
│          │ Shows:                                                  │
│ Tenant   │ - Fleet dashboard (no tenant selected)                │
│ Ops      │ - Tenant home (tenant selected, no module)            │
│          │ - Module content (tenant + module selected)            │
│ Commerce │                                                        │
│ Catalogue│                                                        │
│ ERP      │                                                        │
│ ...      │                                                        │
│          │                                                        │
│ Platform │                                                        │
└──────────┴───────────────────────────────────────────────────────┘
```

---

### How to Use

#### 1. Provider Login
1. Go to `https://www.ecomae.com/bos/`
2. Enter provider email + password
3. You see the **Fleet Dashboard** — all tenants listed with stats

#### 2. Select a Tenant
- **Top bar:** Click the tenant switcher dropdown → search or filter by type → click a tenant
- **Dashboard:** Click any tenant card on the fleet dashboard
- The sidebar rebuilds to show ONLY the modules allocated to that tenant's industry

#### 3. Navigate Modules
- Click any module in the sidebar to open it
- Sidebar sections collapse/expand (click section headers)
- Sidebar collapses to icons (click hamburger menu or Ctrl+B)

#### 4. Tenant Type Access

| Tenant Type | What They See |
|-------------|---------------|
| **Commerce (e.g., epartscart.com)** | Full sidebar: Commerce + Catalogue + Logistics + Marketing + Professional + ERP + industry-specific modules |
| **ERP Only (e.g., ASAP)** | ERP modules only: General Ledger, AP, AR, Cash & Bank, Tax, HR, Payroll, etc. No commerce sections. |
| **Demo** | Full sandbox based on their demo industry preset. Auto-expires after 3 days. |
| **Platform** | Provider-only areas: Fleet Command, Tenant Operations, etc. |

#### 5. Country Compliance
When you select a tenant, BOS auto-shows their country compliance:
- **Country flag** in top bar
- **Currency, Tax rate, Language** displayed
- **Compliance bar** under breadcrumb shows: Country, Currency, Tax type+rate, Language/Direction, E-invoice scheme

This auto-resolves from the tenant's REGISTRATION_COUNTRY. Zero manual config.

---

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+K` | Open tenant switcher |
| `Escape` | Close tenant switcher |

---

### Security Model

| Layer | How It Works |
|-------|-------------|
| **Session isolation** | PHP session on ecomae.com domain. Provider session cannot be hijacked from tenant domains. |
| **DB isolation** | Each tenant has own MySQL database + credentials. PDO connects to one DB at a time. No cross-DB queries. |
| **RBAC** | Provider: `boc.*` (all areas). Tenant: own site_key only. BOC areas hidden from tenant users. |
| **Audit log** | Every login and tenant switch logged to `epc_boc_audit` with timestamp, user, action, IP. |
| **SEO protection** | `X-Robots-Tag: noindex, nofollow` on all BOS pages. `robots.txt` blocks `/bos/`. |
| **CSP** | Content-Security-Policy header blocks external scripts, frames. |
| **X-Frame-Options** | DENY — BOS cannot be embedded in iframes. |
| **CSRF** | POST-only mutations. Session-bound tenant context validated per request. |

---

### Industry × Module Matrix

| Industry | Packs | Sidebar Sections |
|----------|-------|-----------------|
| auto_parts | core, commerce, auto_parts, logistics, erp, professional, marketing | Commerce + Catalogue + Logistics + Marketing + Professional + ERP + Auto Parts (Crosses, APAI, VIN) |
| tax_advisory | core, commerce, professional, tax_advisory, erp, logistics, marketing | Commerce + Professional + ERP + Tax & Advisory |
| fashion | core, commerce, catalogue | Commerce + Catalogue only |
| electronics | core, commerce, catalogue | Commerce + Catalogue only |
| jewellery | core, commerce, catalogue | Commerce + Catalogue only |
| medical | core, commerce, catalogue, professional, erp | Commerce + Catalogue + Professional + ERP |
| health | core, commerce, catalogue | Commerce + Catalogue only |
| consultancy | core, professional, erp, commerce | Professional + ERP + Commerce |
| erp_standalone | core, erp, professional, logistics | ERP + Professional only (no commerce) |
| rental | core, commerce, catalogue | Commerce + Catalogue only |
| platform_host | ALL packs + super_platform | Everything + BOC Fleet Command |

---

### Future Work

#### Adding a New Tenant
1. BOS → Tenant Operations → Tenant Hub → "Onboard new tenant"
2. Select industry, enter hostname, country, trade name
3. System auto-creates: MySQL DB, tenant row, site settings, packs
4. Tenant appears in BOS switcher immediately

#### Adding a New ERP Customer
1. Same flow — check "ERP Only" during onboarding
2. Industry auto-set to `erp_standalone`
3. No storefront, no commerce modules — ERP sidebar only

#### Upgrading a Tenant (e.g., fashion → fashion + ERP)
1. BOS → Tenant Operations → Industry / ERP Packs
2. Add `erp` and `professional` packs to the tenant
3. System syncs packs to tenant DB
4. ERP modules appear in their sidebar

#### Adding a New Industry
1. Add entry to `epc_portal_industries()` in `epc_portal.php`
2. Define: code, name, ecosystem, icon, theme, cp_packs
3. Optionally create storefront template
4. All BOS infrastructure works automatically

---

### Files Reference

| File | Purpose |
|------|---------|
| `bos/index.php` | BOS shell — login + main layout + dashboard |
| `bos/ajax_epc_bos.php` | AJAX handler — login, tenant switch, module load |
| `bos/epc_bos_shell.css` | Enterprise design system |
| `bos/epc_bos_shell.js` | Client-side controller |
| `bos/.htaccess` | URL routing + security headers |
| `content/general_pages/epc_bos_unified.php` | BOS kernel — session bridge, sidebar engine, tenant resolver |

---

### Architecture

```
ecomae.com/bos/ (BOS Entry)
    ├── Login → epc_bos_ajax_login()
    │   ├── Authenticate against platform DB
    │   ├── Set session: role (provider/tenant), user_id, email
    │   └── Audit log: bos.login
    │
    ├── Fleet Dashboard (no tenant selected)
    │   ├── Stat cards: total, commerce, ERP, demo counts
    │   ├── Tenant grid: clickable cards
    │   └── Industry coverage: which industries have tenants
    │
    ├── Tenant Selected (?t=epartscart)
    │   ├── Load tenant settings from epc_portal_tenants
    │   ├── Resolve industry → cp_packs → sidebar sections
    │   ├── Connect to tenant DB → read country → compliance profile
    │   ├── Render: compliance bar + module grid
    │   └── Sidebar shows: BOC (provider) + CP modules + ERP modules
    │
    └── Module Selected (?t=epartscart&m=orders)
        ├── Sidebar highlights active module
        └── Main content loads module via CP path
```
