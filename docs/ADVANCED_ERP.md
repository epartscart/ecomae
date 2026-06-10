# Advanced ERP — implementation, workflow, guide & testing

This document describes the **Advanced ERP** layer: an additive upgrade that makes
the ERP industry-agnostic (works for any business worldwide), wires it to the
existing worldwide tax toolkit, advances the CRM, and ships a full in-app user
guide. Everything here is **additive and reversible** — new files plus
`CREATE TABLE IF NOT EXISTS` / `INSERT ... ON DUPLICATE KEY UPDATE` only — so it is
safe to deploy on live tenants such as `epartscart.com`.

---

## 1. What was added

| File | Purpose |
|------|---------|
| `content/shop/finance/epc_erp_advanced.php` | Shared helpers: key/value settings store (on `epc_price_settings`), CP content-page registrar, formatting. |
| `content/shop/finance/epc_erp_industry.php` | Industry-agnostic product/inventory foundation. 15 industry blueprints that seed inventory custom fields, item type and default unit. |
| `content/shop/finance/epc_erp_crm_advanced.php` | CRM intelligence: lead scoring, weighted pipeline forecast, customer-360, tax-aware quote totals. |
| `epc-erp-advanced-setup.php` | Token-gated one-shot migration that ensures all schemas, installs worldwide tax kits, optionally applies an industry, and registers the guide. |
| `epc-register-erp-advanced-guide.php` | Standalone repair helper to (re)register the guide CP page. |
| `cp/content/shop/finance/erp/erp_advanced_guide_page.php` | CP page wrapper (session check + include). |
| `cp/content/shop/finance/erp/erp_advanced_guide.php` | The full in-app user guide (workflow + per-module, translation-friendly). |
| `tests/erp_advanced/run_tests.php` | CLI integration test harness (27 assertions). |

**Bug fix (in scope for "advance the CRM"):** `content/shop/finance/epc_crm_schema.php`
had a sample-seed `INSERT` into `epc_crm_quote_lines` with 2 placeholders but 4
bound values, which **crashed CRM setup on any fresh tenant**. Fixed to 4
placeholders.

## 2. Reuses what already exists (no duplication)

- **Worldwide tax** is the existing `epc_tax_toolkit.php` (243 country kits;
  per-tenant/per-customer profiles; VAT/GST/sales-tax/CIT). The advanced layer
  *installs and wires* it rather than rebuilding it.
- **Industries** align with the existing pricing/AI taxonomy
  (`epc_apai_industry_profiles`) and extend it into the ERP inventory layer.
- **CRM** is the existing `epc_crm_*` module; the advanced layer is read-mostly
  intelligence on top.
- **Multilingual** uses the platform's existing Google Translate layer — the guide
  and screens are plain translatable HTML.

## 3. End-to-end workflow

1. **Set your industry** → seeds the right product fields, units and item type.
2. **Configure tax profile** → worldwide tax engine applies correct VAT/GST/sales-tax.
3. **Add warehouses & products** → industry fields appear automatically.
4. **Record purchases** → stock, average cost, input tax and ledger update.
5. **Sell & invoice** → output tax, stock reduction and ledger posting together.
6. **Manage customers in CRM** → score leads, forecast pipeline, send tax-correct quotes, convert won deals to orders.
7. **Payroll & assets** → salaries and depreciation post to the ledger.
8. **Review reports** → P&L, balance sheet, tax due, inventory valuation, CRM forecast.

## 4. Industries supported (15)

`general`, `auto_parts`, `electronics`, `fashion`, `jewellery`, `food_perishable`,
`pharma`, `cosmetics_beauty`, `furniture_home`, `building_construction`,
`industrial_manufacturing`, `agriculture`, `books_media`, `hospitality_fnb`,
`services_professional`.

Each blueprint defines: default item type (`standard` / `perishable` / `serialized`),
expiry tracking, default + allowed units, recommended custom fields (text / number /
date / select), and a tax category hint. Applying an industry **only adds** field
definitions — it never deletes existing fields or data, and re-applying is idempotent.

## 5. Deploy (additive, safe for live tenants)

Run the token-gated setup on the tenant (browser or curl). Token is the standard
`EPC_DEPLOY_TOKEN` (defaults to `epartscart-deploy-2026`; prefer the env var in
production).

```
# Ensure all schema + install default tax kit + register the guide:
https://www.<tenant>.com/epc-erp-advanced-setup.php?token=<DEPLOY_TOKEN>

# Also apply an industry blueprint (example: auto parts):
https://www.<tenant>.com/epc-erp-advanced-setup.php?token=<DEPLOY_TOKEN>&industry=auto_parts

# Install ALL worldwide country tax kits instead of just defaults:
https://www.<tenant>.com/epc-erp-advanced-setup.php?token=<DEPLOY_TOKEN>&tax=all
```

The script returns JSON with a per-step status, the list of available industries,
and CP links (ERP, inventory, CRM, tax toolkit, **advanced guide**).

The guide then appears in the CP at: `/<backend_dir>/shop/finance/erp/advanced-guide`.

### Rollback

Nothing destructive is performed. To "disable" the guide page, unpublish the
`content` row whose `url = shop/finance/erp/advanced-guide`. New tables are inert
unless used.

## 6. Testing & evidence

Local integration tests run the new modules against a real MySQL/MariaDB:

```
DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
  php tests/erp_advanced/run_tests.php
```

Result: **27 passed, 0 failed**, covering:

- Industry schema + apply (auto parts, food/perishable), field seeding,
  select-option JSON, idempotency, persistence of current industry.
- Worldwide tax catalog resolution (AE 5% VAT, SA 15% VAT, GB 20% VAT, IN 18% GST)
  and tax math.
- Advanced CRM: lead scoring order (qualified > new), weighted pipeline forecast,
  win-rate, customer-360, and tax-aware quote totals.
- CP guide registration into the `content` table.

The full deploy chain (`epc_erp_full_ensure_schema` → inventory → CRM → tax toolkit
install → industry apply → guide registration) was also run end-to-end against a
fresh database: **76 ERP tables created, 243 tax kits available, 1 installed by
default.**

Every new PHP file passes `php -l` (syntax check).

> Note: the live UI walk-through must be run on a tenant (the CP requires the
> Docpart runtime + tenant DB). Use the setup URL above on a staging/test tenant
> first; do **not** run experiments against `epartscart.com` production.
