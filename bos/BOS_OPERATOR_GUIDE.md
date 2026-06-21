# BOS — Business Operating System
## Operator Guide v2.0

### What is BOS?

BOS is the unified control center for the ECOM AE platform. One login, one dashboard — manage all tenants, CP modules, ERP systems, and platform operations from a single place.

**Entry Point:** `https://www.ecomae.com/bos/`

---

### Login Credentials

| Role | Email | Access Level |
|------|-------|-------------|
| **Provider (Super Admin)** | Your CP admin email (same credentials as CP login) | All tenants, all modules, fleet command, tenant switcher |
| **Tenant Admin** | (tenant-specific email) | Their own tenant only — CP + ERP modules allocated to them |
| **ERP-Only Client** | (client-specific email) | Their own ERP only — no commerce modules |

**Note:** BOS uses the same credentials as your existing CP/ERP login. The password is verified using the same hash method as the Control Panel.

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
2. Enter your CP admin email + password (same credentials as Control Panel)
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

This auto-resolves from the tenant's REGISTRATION_COUNTRY via `epc_country_profile()`. Zero manual config.

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

## ONBOARDING — New Tenant Setup

### A. Onboarding a Commerce + ERP Tenant (e.g., spare parts)

**Steps:**

1. **Create MySQL database** on the server for the new tenant:
   ```sql
   CREATE DATABASE epc_tenant_newclient CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   GRANT ALL PRIVILEGES ON epc_tenant_newclient.* TO 'tenant_user'@'localhost';
   FLUSH PRIVILEGES;
   ```

2. **Register tenant in platform DB** — add row to `epc_portal_tenants`:
   ```sql
   INSERT INTO epc_portal_tenants
     (site_key, hostname, industry_code, status, trade_name, hub_name,
      hosted_on, is_active, db_name)
   VALUES
     ('newclient', 'newclient.com', 'auto_parts', 'live', 'New Client Trading',
      'New Client', 'ecomae', 1, 'epc_tenant_newclient');
   ```

3. **Initialize tenant DB** — run the dp_core schema setup on the new DB. This creates tables for users, orders, products, settings, etc.

4. **Set company profile** — in the tenant's DB:
   ```sql
   INSERT INTO epc_co_profile (field, value) VALUES
     ('country', 'AE'),        -- registration country (drives all compliance)
     ('company_name', 'New Client Trading LLC'),
     ('currency', 'AED'),
     ('tax_rate', '5'),
     ('vat_trn', 'TRN123456789');
   ```

5. **Set CP packs** — packs determine what modules appear in BOS sidebar:
   ```sql
   UPDATE epc_portal_tenants
   SET enabled_packs = 'core,commerce,auto_parts,logistics,erp,professional,marketing'
   WHERE site_key = 'newclient';
   ```

6. **Create admin user** in tenant's `users` table:
   ```sql
   INSERT INTO users (email, password, email_confirmed, unlocked)
   VALUES ('admin@newclient.com', MD5(CONCAT('initialpass', '<secret_succession>')), 1, 1);
   ```

7. **Verify in BOS** — log in as provider, the new tenant appears in the fleet dashboard and tenant switcher.

### B. Onboarding an ERP-Only Tenant

Same as above but:
- Set `industry_code = 'erp_standalone'`
- Set `enabled_packs = 'core,erp,professional,logistics'`
- No storefront setup needed
- No commerce/catalogue tables needed
- Sidebar will show ONLY ERP modules (GL, AP, AR, HR, Payroll, etc.)

### C. Onboarding a Demo Tenant

1. Use the existing demo provisioning:
   - BOS → Tenant Operations → Demo Management
   - Or via `epc_portal_demo_provision()` function
2. Demo tenants auto-expire after 3 days (TTL-based)
3. Demo DB is recycled from a pool of pre-created databases
4. Industry preset determines which modules appear

### D. Onboarding a Free Tools User

- No database or tenant needed
- User registers on the platform (shared platform DB)
- They access 14 free tools at `ecomae.com/free-tools`
- Zero overhead — just a user account row

### E. Bulk Onboarding (50+ tenants)

Prepare a CSV file:
```csv
site_key,hostname,industry_code,trade_name,country,enabled_packs
client1,client1.com,auto_parts,Client One,AE,"core,commerce,auto_parts,erp"
client2,client2.com,fashion,Fashion Store,GB,"core,commerce,catalogue"
client3,,erp_standalone,Consulting Firm,PK,"core,erp,professional"
```

Loop through CSV and call `epc_portal_onboard_client()` for each row. The function handles DB creation, schema init, company profile, and pack assignment.

---

## EXISTING TENANT IMPROVEMENTS

### Upgrading a Tenant's Module Access

**Add ERP to a commerce-only tenant (e.g., fashion → fashion + ERP):**
1. BOS → Tenant Operations → Industry / ERP Packs
2. Add `erp` and `professional` to the tenant's `enabled_packs`
3. System syncs packs to tenant DB via `epc_portal_sync_tenant_packs_to_client_db()`
4. ERP modules (GL, AP, AR, Tax, HR, Payroll, etc.) appear in their sidebar immediately

**Enable AI features for a tenant:**
1. Set feature flag: `UPDATE epc_tenant_feature_flags SET value = 'enabled' WHERE site_key = 'client' AND feature_key = 'auto_price_ai'`
2. APAI (Auto Price AI) module appears in sidebar under their industry section

**Enable POS for a tenant:**
1. Add `pos` to `enabled_packs`
2. POS modules appear in Commerce section of sidebar

### Changing a Tenant's Country/Compliance

1. Update company profile in tenant's DB:
   ```sql
   UPDATE epc_co_profile SET value = 'SA' WHERE field = 'country';
   UPDATE epc_co_profile SET value = 'SAR' WHERE field = 'currency';
   UPDATE epc_co_profile SET value = '15' WHERE field = 'tax_rate';
   ```
2. All compliance auto-resolves: tax calculations, e-invoice scheme (ZATCA for Saudi), labour law (HRSD), currency formatting, RTL if Arabic.
3. No code changes needed — `epc_country_profile()` handles everything based on the country code.

### Managing Tenant Users

**Add a new staff member to a tenant:**
1. Select the tenant in BOS
2. Navigate to Platform → Users & Roles
3. Add user with email, assign to backend group (admin/warehouse/finance/counter)
4. User can now log in to BOS as a tenant user (sees only their tenant)

**Reset a user's password:**
```sql
-- In the tenant's database
UPDATE users SET password = MD5(CONCAT('newpassword', '<secret_succession>'))
WHERE email = 'staff@client.com';
```

### Tenant Health Monitoring

1. BOS → Fleet Command → Health Check
2. `epc_platform_health_checkup` runs batch probes:
   - DB connectivity check per tenant
   - Table integrity verification
   - Last order/activity timestamp
   - Disk usage per DB
3. Red/amber/green status shown in fleet dashboard

---

## NEW DEVELOPMENT PROCESS

### Adding a New Industry

1. **Define industry profile** in `content/general_pages/epc_portal.php`:
   ```php
   // In epc_portal_industries()
   'agriculture' => array(
       'code'      => 'agriculture',
       'name'      => 'Agriculture & Farming',
       'ecosystem' => 'commerce_erp',
       'icon'      => 'fa-leaf',
       'theme'     => '#22c55e',
       'cp_packs'  => array('core', 'commerce', 'catalogue', 'logistics', 'erp', 'professional'),
   ),
   ```

2. **Add sidebar items** (if industry-specific modules exist) in `epc_bos_unified.php`:
   ```php
   // In epc_bos_sidebar_sections()
   array(
       'id'    => 'crop_tracking',
       'label' => 'Crop Tracking',
       'icon'  => 'fa-pagelines',
       'path'  => 'shop/agriculture/crop_tracking',
       'requires_pack' => 'agriculture',
   ),
   ```

3. **Create module PHP file** at `cp/content/shop/agriculture/crop_tracking.php`

4. **Test:** Onboard a test tenant with `industry_code = 'agriculture'`, verify sidebar shows correctly.

### Adding a New ERP Module

1. **Create the tab file** at `content/shop/finance/erp_tabs_newmodule.php`
2. **Register in ERP nav** in `content/shop/finance/erp_nav_areas.php`:
   ```php
   array('id' => 'newmodule', 'label' => 'New Module', 'icon' => 'fa-cog', 'area_group' => 'operations'),
   ```
3. **Add to BOS sidebar** in `epc_bos_unified.php` under the ERP section
4. **Test:** Select a tenant with ERP packs → verify module appears and loads

### Adding a New Country Compliance Profile

Follow the hard rule: ALL worldwide features must be driven by the tenant's REGISTRATION_COUNTRY.

1. **Extend country profile** in `content/shop/finance/epc_erp_localization.php`:
   ```php
   // In epc_country_profile()
   'NG' => array(
       'name'        => 'Nigeria',
       'currency'    => 'NGN',
       'symbol'      => '₦',
       'tax_type'    => 'VAT',
       'tax_rate'    => 7.5,
       'direction'   => 'ltr',
       'language'    => 'en',
       'einvoice'    => 'FIRS',
       'date_format' => 'd/m/Y',
   ),
   ```

2. **Extend labour law** in `content/shop/finance/epc_erp_hr_law.php`:
   ```php
   // In epc_hr_law_profile()
   'NG' => array(
       'authority'     => 'Federal Ministry of Labour and Employment',
       'authority_url' => 'https://labour.gov.ng/',
       'eos_method'    => 'nigerian_labour_act',
       'currency'      => 'NGN',
       // ...statutory rates, leave, pension, etc.
   ),
   ```

3. **No code changes needed in BOS, sidebar, or tenants.** When a tenant registers with `country = 'NG'`, everything auto-resolves.

### Adding a New CP Module

1. **Create PHP file** at `cp/content/shop/<section>/<module_name>.php`
2. **Register in CP menu** in `content/general_pages/epc_portal_cp_menu.php`
3. **Add to BOS sidebar** in `epc_bos_unified.php` with appropriate `requires_pack` filter
4. **Whitelist in epc-static.php** if the module needs static assets from a new directory

### Adding a New BOC (Platform Operations) Area

1. **Register in BOC kernel** in `content/general_pages/epc_boc_kernel.php`:
   ```php
   'new_area' => $a('New Area', 'operations', 'fa-wrench', 'control/portal/epc_new_area', 'ops', 'boc.operations.manage', $ALL, 'Description here'),
   ```
2. **Create PHP file** at `cp/control/portal/epc_new_area.php`
3. **BOS sidebar auto-includes BOC areas** for provider users

### Code Deployment Process

1. **Develop** on a feature branch:
   ```bash
   git checkout -b feature/new-module
   # make changes
   git add <files>
   git commit -m "feat: add new module"
   git push origin feature/new-module
   ```

2. **Create PR** on GitHub → review → merge to `main`

3. **Deploy to production:**
   ```bash
   cd /home/ecomae/htdocs/www.ecomae.com
   git pull origin main
   ```
   If `git pull` fails with "aborting" due to untracked files:
   ```bash
   git fetch origin main
   git reset --hard origin/main
   ```

4. **Verify** — test the change on `https://www.ecomae.com/bos/` or relevant URL

---

### Files Reference

| File | Purpose |
|------|---------|
| `bos/index.php` | BOS shell — login + main layout + dashboard |
| `bos/ajax_epc_bos.php` | AJAX handler — login, tenant switch, module load |
| `bos/epc_bos_shell.css` | Enterprise design system |
| `bos/epc_bos_shell.js` | Client-side controller |
| `content/general_pages/epc_bos_unified.php` | BOS kernel — session bridge, sidebar engine, tenant resolver |
| `content/general_pages/epc_portal.php` | Platform core — industries, tenant registry, DB connections |
| `content/general_pages/epc_portal_tenant_control.php` | Tenant CRUD, onboarding, pack sync |
| `content/general_pages/epc_boc_kernel.php` | BOC (Business Operations Center) — 31 operator areas |
| `content/shop/finance/epc_erp_localization.php` | Country profiles — currency, tax, language, e-invoice |
| `content/shop/finance/epc_erp_hr_law.php` | Labour law engine — per-country statutory compliance |
| `content/general_pages/epc_ecomae_platform_router.php` | Marketing site router + BOS intercept |
| `epc-static.php` | Static asset gateway (CSS, JS, images) for nginx |

---

### Architecture

```
ecomae.com/bos/ (BOS Entry)
    ├── Login → epc_bos_ajax_login()
    │   ├── Authenticate against main site DB (same as CP login)
    │   ├── Password: bcrypt OR md5(password + secret_succession)
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

### Tenant Scale Reference

| Tenant Type | Typical Count | DB per Tenant? | Sidebar |
|-------------|---------------|----------------|---------|
| Spare parts (commerce+ERP) | 20 | Yes | Full (Commerce + ERP + Auto Parts) |
| Other industries (commerce) | 50 | Yes | Commerce + Catalogue (+optional ERP) |
| ERP-only (multi-industry) | 35 | Yes | ERP only (no commerce) |
| Demo (sandboxed) | 20 | Yes (pooled, recycled) | Full per industry preset |
| Free tools (no DB) | 100 | No — shared platform | N/A — free tools only |
| **Total** | **225+** | **125 DBs** | |

---

### Troubleshooting

| Problem | Solution |
|---------|----------|
| **Login says "Invalid credentials"** | Use the same email+password as your CP login. If still fails, verify the user exists in the `users` table of the main site DB with `email_confirmed = 1` and `unlocked = 1`. |
| **BOS shows marketing page** | The server needs `git pull origin main`. BOS routing works via PHP intercept, not .htaccess (nginx ignores .htaccess). |
| **CSS not loading (unstyled page)** | CSS is served via `/epc-static.php?f=bos/epc_bos_shell.css`. Check that `bos` is whitelisted in `epc-static.php` line 17. |
| **Tenant not appearing in switcher** | Check `epc_portal_tenants` table — verify `site_key` is not empty and `is_active = 1`. |
| **ERP modules missing from sidebar** | Verify the tenant's `enabled_packs` includes `erp` and `professional`. |
| **Country compliance not showing** | Set the `country` field in tenant's `epc_co_profile` table. |
| **git pull aborting on server** | Run `git fetch origin main && git reset --hard origin/main` to force-sync. |

---

### Key URLs

| URL | Purpose |
|-----|---------|
| `https://www.ecomae.com/bos/` | BOS login & dashboard |
| `https://www.ecomae.com/` | Marketing homepage |
| `https://www.ecomae.com/platform` | Platform overview (marketing) |
| `https://www.ecomae.com/industries` | Industries list (marketing) |
| `https://www.ecomae.com/free-tools` | Free tools (VAT, Payroll, etc.) |
| `https://www.ecomae.com/pricing` | Pricing page (marketing) |
| `https://www.ecomae.com/cp/` | Control Panel (per-tenant) |

---

*Last updated: June 2026 — BOS v1.0.0*
